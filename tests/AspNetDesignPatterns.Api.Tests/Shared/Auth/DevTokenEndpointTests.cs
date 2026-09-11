using AspNetDesignPatterns.Api.Shared.Auth;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace AspNetDesignPatterns.Api.Tests.Shared.Auth;

/// <summary>
/// <see cref="DevTokenEndpoint"/> maps itself only in Development — it reads
/// <c>IHostEnvironment</c> at map time rather than gating with <c>RequireHost</c> or similar,
/// so the only way to prove the gate holds is to map it against a real (unstarted)
/// <see cref="WebApplication"/> per environment and inspect what got registered.
/// </summary>
[TestFixture]
public class DevTokenEndpointTests
{
    private const string ROUTE = "/auth/token";

    private static IReadOnlyCollection<string?> MappedRoutePatterns(string environmentName)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = environmentName,
        });

        // DevTokenIssuer is a request-delegate parameter; Minimal API metadata inference needs
        // it resolvable as a service even though we never actually invoke the endpoint.
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton(Options.Create(new JwtOptions
        {
            SigningKey = new string('k', 32),
            Issuer = "test-issuer",
            Audience = "test-audience",
        }));
        builder.Services.AddScoped<DevTokenIssuer>();

        using WebApplication app = builder.Build();
        new DevTokenEndpoint().MapEndpoint(app);

        return [.. ((IEndpointRouteBuilder) app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.RawText)];
    }

    [Test]
    public void Maps_the_dev_token_route_in_Development() =>
        MappedRoutePatterns(Environments.Development).Should().Contain(ROUTE);

    [TestCase("Production")]
    [TestCase("Staging")]
    public void Does_not_map_the_dev_token_route_outside_Development(string environmentName) =>
        MappedRoutePatterns(environmentName).Should().NotContain(ROUTE);
}
