using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

using Asp.Versioning;
using Asp.Versioning.Builder;
using Asp.Versioning.OpenApi;

using DotNetEnv;

using FluentValidation;

using KAM.Common.Authentication;
using KAM.Common.Authorization;
using KAM.Common.Configuration;
using KAM.Common.Cors;
using KAM.Common.DependencyInjection;
using KAM.Common.HealthChecks;
using KAM.Common.Logging;
using KAM.Common.OpenApi;
using KAM.Common.Telemetry;

using Serilog;

// Load .env into environment variables before configuration is built, so values like
// Jwt__SigningKey are picked up by the default environment-variables configuration source.
// (No-op when no .env file is found.)
Env.TraversePath().Load();

// ─────────────────────────────  Builder  ─────────────────────────────
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Fail fast on a mis-wired DI graph and on captive-dependency mistakes.
builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateOnBuild = true;
    options.ValidateScopes = true;
});

// "Where and how am I running" — built once, used for Kestrel + Scalar gating, then shared.
AppEnvironment appEnvironment = new(builder.Environment);
builder.Services.AddSingleton(appEnvironment);

// This app's own identity for telemetry — shared by the Serilog sink and AddAppTelemetry()
// below so logs, traces, and metrics all show the same service in the collector/dashboard.
// A literal here, never a KAM.Common default: the library has no identity of its own to report.
const string SERVICE_NAME = "AspNetDesignPatterns.Api";

// Bound directly from configuration (not via IOptions<T>) because both the Serilog sink below
// and AddAppTelemetry() need it before the DI container is built. It's also a normal
// SettingsBase<T>, so AddSettings() further down registers IOptions<TelemetrySettings> too —
// this early bind is only for the two call sites that can't wait for that.
TelemetrySettings telemetry = builder.Configuration.GetSection(TelemetrySettings.Section).Get<TelemetrySettings>() ?? new();

// Global exception handling → RFC 9457 ProblemDetails (see GlobalExceptionHandler).
builder.Services.AddGlobalExceptionHandler();
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
{
    context.ProblemDetails.Instance ??=
        $"{context.HttpContext.Request.Method} {context.HttpContext.Request.Path}";
    context.ProblemDetails.Extensions["traceId"] =
        System.Diagnostics.Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
});

// One System.Text.Json configuration, shared by Minimal API (de)serialization and typed clients.
JsonSerializerOptions jsonOptions = CreateJsonSerializerOptions();
builder.Services.AddSingleton(jsonOptions);
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = jsonOptions.PropertyNamingPolicy;
    options.SerializerOptions.PropertyNameCaseInsensitive = jsonOptions.PropertyNameCaseInsensitive;
    options.SerializerOptions.DefaultIgnoreCondition = jsonOptions.DefaultIgnoreCondition;
    options.SerializerOptions.WriteIndented = jsonOptions.WriteIndented;
    foreach (JsonConverter converter in jsonOptions.Converters)
    {
        options.SerializerOptions.Converters.Add(converter);
    }
});

// Kestrel: listen on all interfaces when containerized (or anywhere but local dev).
builder.WebHost.ConfigureKestrel(options =>
{
    if (appEnvironment.IsContainerized || !appEnvironment.IsDevelopment)
    {
        options.ListenAnyIP(8080);
    }
});

// Serilog becomes the ILoggerFactory; the rest of the app depends only on ILogger<T>.
// The ControlCharacterSanitizingEnricher strips CR/LF from every event (CWE-117 log injection).
// The OpenTelemetry sink is the one fluent addition to an otherwise declarative
// (appsettings.json) Serilog config — every LogOnFailure(logger) call ships to the same OTLP
// collector as the traces/metrics below, correlated to the active trace automatically.
builder.Services.AddSerilog((services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.With<ControlCharacterSanitizingEnricher>();

    if (telemetry.Enabled)
    {
        configuration.WriteTo.OpenTelemetry(options =>
        {
            // Without this, logs report as "unknown_service:AspNetDesignPatterns.Api" instead
            // of matching the clean service name ConfigureResource(...) gives the traces/metrics
            // exported by AddAppTelemetry() below — this sink has its own, separate resource.
            options.ResourceAttributes["service.name"] = SERVICE_NAME;

            if (telemetry.OtlpEndpoint is not null)
            {
                options.Endpoint = telemetry.OtlpEndpoint;
            }
        });
    }
});

// Every typed HttpClient gets a User-Agent and the standard Polly resilience pipeline.
builder.Services.ConfigureHttpClientDefaults(http =>
{
    http.ConfigureHttpClient(client =>
        client.DefaultRequestHeaders.UserAgent.ParseAdd("AspNetDesignPatterns/1.0"));
    http.AddStandardResilienceHandler();
});

// URL-segment API versioning: /api/v{version}/... — with one OpenAPI document per version.
// The versioning-aware .AddOpenApi() supersedes a bare services.AddOpenApi() (analyzer AV0029).
builder.Services
    .AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1);
        options.AssumeDefaultVersionWhenUnspecified = true;
        options.ReportApiVersions = true;
        options.ApiVersionReader = new UrlSegmentApiVersionReader();
    })
    .AddApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'V";
        options.SubstituteApiVersionInUrl = true;
    })
    .AddOpenApi(options => options.AddBearerSecurityScheme());

// CORS, then authentication ("who is the caller"), then authorization ("what can they do") —
// AddPolicyDrivenAuthorization() builds every named policy (and the fallback) from the
// "Authorization" config section; see its README for why that's separate from AddJwtAuth().
builder.Services.AddCorsPolicy();
builder.Services.AddJwtAuth();
builder.Services.AddPolicyDrivenAuthorization();
builder.Services.AddHttpContextAccessor();

// Traces (ASP.NET Core + HttpClient + one span per Pipeline<TContext> step) and metrics
// (+ .NET runtime counters), exported via OTLP alongside the Serilog sink above. This app's
// own name is passed in — KAM.Common has no identity of its own to report as the service.
builder.Services.AddAppTelemetry(SERVICE_NAME, telemetry);

// Health-check services. Individual checks self-register from the slice that owns them
// (e.g. Features/Weather/WeatherDependencies); the two probes are mapped by MapAppHealthChecks().
// AddMemoryCache() backs the short-lived caching a check may do (e.g. WeatherProviderHealthCheck)
// so the anonymous /health/ready route can't be used to drive unlimited outbound calls.
builder.Services.AddHealthChecks();
builder.Services.AddMemoryCache();

// Both assemblies can define endpoints, settings, dependency modules, handlers, validators,
// and pipeline steps -- Features/ here, and KAM.Common's own JwtOptions + DevTokenEndpoint +
// TokenRequestValidator. Every scan below is given both explicitly; the zero-argument default
// (the calling assembly, i.e. this one) would silently miss KAM.Common's half.
Assembly[] appAssemblies = [typeof(Program).Assembly, typeof(IEndpoint).Assembly];

// FluentValidation validators — MUST be registered before AddSettings() so SettingsBase<T>
// can detect them and choose the FluentValidation path over DataAnnotations.
builder.Services.AddValidatorsFromAssemblies(appAssemblies, includeInternalTypes: true);

// ── Convention-based registration (one assembly scan each) ──
builder.Services.AddSettings(appAssemblies);          // ISettings  → bind + validate settings
builder.Services.AddDependencies(appAssemblies);      // IDependency → feature-owned DI (typed clients, …)
builder.Services.AddEndpoints(appAssemblies);         // IEndpoint   → Minimal API endpoints
builder.Services.AddRequestHandlers(appAssemblies);   // IRequestHandler<,>
builder.Services.AddPipeline();                       // IPipelineFactory
builder.Services.AddPipelineSteps(appAssemblies);     // IPipelineStep<>

// ────────────────────────────────  App  ──────────────────────────────
WebApplication app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseSerilogRequestLogging();

app.UseCors(CorsExtensions.PolicyName);
app.UseAuthentication();
app.UseAuthorization();

// OpenAPI document at /openapi/v1.json; Scalar UI at /scalar (Development only). Title/theme
// are this app's own branding, passed in rather than baked into KAM.Common — the library has
// no identity of its own to lend the UI.
app.MapApiReference(title: "AspNetDesignPatterns API", theme: "Mars");

// Liveness + readiness probes at /health/live and /health/ready (root, unversioned, anonymous).
app.MapAppHealthChecks();

// All feature endpoints live under the versioned group: /api/v1/...
ApiVersionSet apiVersionSet = app
    .NewApiVersionSet()
    .HasApiVersion(new ApiVersion(1))
    .ReportApiVersions()
    .Build();

RouteGroupBuilder versionedApi = app
    .MapGroup("api/v{version:apiVersion}")
    .WithApiVersionSet(apiVersionSet);

app.MapEndpoints(versionedApi);

app.Run();

// ───────────────────────────────  Methods  ───────────────────────────

// The single source of truth for JSON serialization across the app.
static JsonSerializerOptions CreateJsonSerializerOptions()
{
    JsonSerializerOptions options = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };
    options.Converters.Add(new JsonStringEnumConverter());
    return options;
}

/// <summary>Exposed so the test project's <c>WebApplicationFactory&lt;Program&gt;</c> can boot the app.</summary>
public partial class Program;
