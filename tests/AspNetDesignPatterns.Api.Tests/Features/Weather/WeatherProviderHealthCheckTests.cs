using System.Net;

using AspNetDesignPatterns.Api.Features.Weather;
using AspNetDesignPatterns.Api.Tests.TestSupport;

using Microsoft.Extensions.Caching.Memory;
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

    private static WeatherProviderHealthCheck CreateCheck(HttpMessageHandler handler, IMemoryCache? cache = null) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://weather.test") }, cache ?? new MemoryCache(new MemoryCacheOptions()));

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

    [Test]
    public async Task Caches_the_result_so_a_second_probe_does_not_call_the_provider_again()
    {
        // Arrange — the readiness route is anonymous; without a cache, every unauthenticated
        // probe would drive one outbound call to open-meteo.
        StubHttpMessageHandler handler = new(HttpStatusCode.OK);
        IMemoryCache cache = new MemoryCache(new MemoryCacheOptions());
        WeatherProviderHealthCheck check = CreateCheck(handler, cache);

        // Act
        HealthCheckResult first = await check.CheckHealthAsync(Context, CancellationToken.None);
        HealthCheckResult second = await check.CheckHealthAsync(Context, CancellationToken.None);

        // Assert
        using (new AssertionScope())
        {
            handler.CallCount.Should().Be(1);
            second.Status.Should().Be(first.Status);
        }
    }

    [Test]
    public async Task A_fresh_probe_can_still_call_the_provider_once_the_cache_entry_is_gone()
    {
        // Arrange — two independently-scoped checks (the typed client is transient) sharing no
        // cache each probe once; this is the "cache expired" case, simulated without a clock seam.
        StubHttpMessageHandler firstHandler = new(HttpStatusCode.OK);
        StubHttpMessageHandler secondHandler = new(HttpStatusCode.OK);

        // Act
        await CreateCheck(firstHandler).CheckHealthAsync(Context, CancellationToken.None);
        await CreateCheck(secondHandler).CheckHealthAsync(Context, CancellationToken.None);

        // Assert
        using (new AssertionScope())
        {
            firstHandler.CallCount.Should().Be(1);
            secondHandler.CallCount.Should().Be(1);
        }
    }
}
