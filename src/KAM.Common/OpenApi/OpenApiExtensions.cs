using Asp.Versioning.OpenApi;

using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

using Scalar.AspNetCore;

namespace KAM.Common.OpenApi;

public static class OpenApiExtensions
{
    /// <summary>
    /// Serves one OpenAPI JSON per API version (<c>/openapi/v1.json</c>, …) outside Production,
    /// and the Scalar UI at <c>/scalar</c> in Development only. The document generator itself
    /// is registered by <c>AddApiVersioning().AddOpenApi(...)</c> in <c>Program.cs</c>.
    /// </summary>
    /// <remarks>
    /// The document is anonymous and lists every route plus the auth scheme description — fine
    /// for a reference app, but the conservative default for a real API is to not publish your
    /// full surface to the internet. Remove the <c>IsProduction</c> guard if your API doc is
    /// meant to be public.
    /// </remarks>
    public static WebApplication MapApiReference(this WebApplication app)
    {
        if (!app.Environment.IsProduction())
        {
            app.MapOpenApi().WithDocumentPerVersion().AllowAnonymous();
        }

        if (app.Environment.IsDevelopment())
        {
            app.MapScalarApiReference(options => options
                    .WithTitle("AspNetDesignPatterns API")
                    .WithTheme(ScalarTheme.Mars))
                .AllowAnonymous();
        }

        return app;
    }

    /// <summary>
    /// Adds the JWT bearer security scheme to the document these options configure, so Scalar
    /// shows an "Authorize" box. Wrapped as an extension so <c>Program.cs</c> never needs
    /// visibility into the (internal) transformer type itself.
    /// </summary>
    public static VersionedOpenApiOptions AddBearerSecurityScheme(this VersionedOpenApiOptions options)
    {
        options.Document.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
        return options;
    }
}

/// <summary>Adds a JWT bearer security scheme to the OpenAPI document so Scalar shows an "Authorize" box.</summary>
internal sealed class BearerSecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        OpenApiSecurityScheme scheme = new()
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            In = ParameterLocation.Header,
            BearerFormat = "JSON Web Token",
            Description = "Paste a token from POST /api/v1/auth/token (Development only).",
        };

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>(StringComparer.Ordinal);
        document.Components.SecuritySchemes["Bearer"] = scheme;

        document.Security ??= [];
        document.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", document)] = [],
        });

        return Task.CompletedTask;
    }
}
