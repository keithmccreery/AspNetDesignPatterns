using System.Net;

using AspNetDesignPatterns.Api.Features.Weather;
using AspNetDesignPatterns.Api.Tests.TestSupport;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AspNetDesignPatterns.Api.Tests.Features.Weather;

[TestFixture]
public class WeatherProviderHealthCheckTests
{
    private static readonly HealthCheckContext Context = new()
    {
        Registration = new HealthCheckRegistration(
            "weather-provider",
            _ => throw new InvalidOperationException("not used"),
            HealthStatus.Degraded,
            tags: null),
    };

    private static WeatherProviderHealthCheck CreateCheck(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://weather.test") });

    [Test]
    public async Task Reports_Healthy_when_the_provider_returns_a_success_status()
    {
        // Arrange
        WeatherProviderHealthCheck check = CreateCheck(new StubHttpMessageHandler(HttpStatusCode.OK));

        // Act
        HealthCheckResult result = await check.CheckHealthAsync(Context, CancellationToken.None);

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Test]
    public async Task Reports_Degraded_when_the_provider_returns_an_error_status()
    {
        // Arrange
        WeatherProviderHealthCheck check = CreateCheck(new StubHttpMessageHandler(HttpStatusCode.ServiceUnavailable));

        // Act
        HealthCheckResult result = await check.CheckHealthAsync(Context, CancellationToken.None);

        // Assert
        using (new AssertionScope())
        {
            result.Status.Should().Be(HealthStatus.Degraded);
            result.Description.Should().Contain("ServiceUnavailable");
        }
    }

    [Test]
    public async Task Reports_Degraded_when_the_provider_is_unreachable()
    {
        // Arrange
        WeatherProviderHealthCheck check = CreateCheck(
            new StubHttpMessageHandler(new HttpRequestException("no such host")));

        // Act
        HealthCheckResult result = await check.CheckHealthAsync(Context, CancellationToken.None);

        // Assert
        using (new AssertionScope())
        {
            result.Status.Should().Be(HealthStatus.Degraded);
            result.Exception.Should().BeOfType<HttpRequestException>();
        }
    }
}
