using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace AspNetDesignPatterns.Api.Tests.Integration;

/// <summary>
/// The app's fallback authorization policy is deliberately <see langword="null"/> (fail-open:
/// an endpoint is anonymous unless it says otherwise), and nothing at the type level can see
/// whether a given <c>MapEndpoint</c> called <c>.RequireAuthorization(...)</c> or
/// <c>.AllowAnonymous()</c> — that only shows up in the endpoint metadata built at startup.
/// This walks the real, running app's endpoints and requires every one of them to declare its
/// auth intent explicitly, so a future slice that forgets the line fails a test instead of
/// shipping a silently public endpoint.
/// </summary>
[TestFixture]
public class EndpointAuthorizationTests
{
    [Test]
    public void Every_mapped_endpoint_declares_an_explicit_authorization_or_anonymous_intent()
    {
        // Arrange
        IReadOnlyList<RouteEndpoint> endpoints = [.. GlobalTestSetup.Factory.Services
            .GetRequiredService<EndpointDataSource>()
            .Endpoints
            .OfType<RouteEndpoint>()];

        // Act
        IEnumerable<string?> undeclared = endpoints
            .Where(endpoint => endpoint.Metadata.GetMetadata<IAuthorizeData>() is null
                             && endpoint.Metadata.GetMetadata<IAllowAnonymous>() is null)
            .Select(endpoint => endpoint.RoutePattern.RawText);

        // Assert
        endpoints.Should().NotBeEmpty("otherwise this test is checking nothing");
        undeclared.Should().BeEmpty("every endpoint must call .RequireAuthorization(...) or .AllowAnonymous() explicitly");
    }
}
