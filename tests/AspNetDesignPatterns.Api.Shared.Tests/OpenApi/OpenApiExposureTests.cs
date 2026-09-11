using Asp.Versioning;

using AspNetDesignPatterns.Api.Shared.OpenApi;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace AspNetDesignPatterns.Api.Shared.Tests.OpenApi;

/// <summary>
/// <see cref="OpenApiExtensions.MapApiReference"/> is Production-gated for the OpenAPI JSON
/// document (anonymous, lists every route) and Development-gated for the Scalar UI. Proven
/// against a real (unstarted) <see cref="WebApplication"/> per environment, mirroring the
/// versioned-OpenAPI registration in <c>Program.cs</c>.
/// </summary>
[TestFixture]
public class OpenApiExposureTests
{
    private static IReadOnlyCollection<string?> MappedRoutePatterns(string environmentName)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = environmentName,
        });

        builder.Services
            .AddApiVersioning(options => options.DefaultApiVersion = new ApiVersion(1))
            .AddApiExplorer(options => options.GroupNameFormat = "'v'V")
            .AddOpenApi();

        using WebApplication app = builder.Build();
        app.MapApiReference();

        return [.. ((IEndpointRouteBuilder) app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.RawText)];
    }

    [TestCase("Development")]
    [TestCase("Staging")]
    public void Serves_the_OpenAPI_document_outside_Production(string environmentName) =>
        MappedRoutePatterns(environmentName).Should().Contain(p => p != null && p.Contains("openapi", StringComparison.Ordinal));

    [Test]
    public void Does_not_serve_the_OpenAPI_document_in_Production() =>
        MappedRoutePatterns("Production").Should().NotContain(p => p != null && p.Contains("openapi", StringComparison.Ordinal));

    [Test]
    public void Serves_the_Scalar_UI_only_in_Development()
    {
        using (new AssertionScope())
        {
            MappedRoutePatterns("Development").Should().Contain(p => p != null && p.Contains("scalar", StringComparison.Ordinal));
            MappedRoutePatterns("Staging").Should().NotContain(p => p != null && p.Contains("scalar", StringComparison.Ordinal));
            MappedRoutePatterns("Production").Should().NotContain(p => p != null && p.Contains("scalar", StringComparison.Ordinal));
        }
    }
}
