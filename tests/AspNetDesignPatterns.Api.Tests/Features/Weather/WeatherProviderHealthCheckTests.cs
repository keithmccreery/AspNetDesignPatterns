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
    public async Task Concurrent_probes_racing_the_cache_share_a_single_outbound_call()
    {
        // Arrange — the cache holds the in-flight probe Task, so callers arriving while it's
        // still running share it. GatedHandler keeps the probe genuinely in flight (unlike
        // StubHttpMessageHandler, which completes synchronously) until every caller has started.
        GatedHandler handler = new();
        IMemoryCache cache = new MemoryCache(new MemoryCacheOptions());
        WeatherProviderHealthCheck check = CreateCheck(handler, cache);

        // Act
        Task<HealthCheckResult>[] probes = [.. Enumerable.Range(0, 20).Select(_ => check.CheckHealthAsync(Context, CancellationToken.None))];
        handler.Release(HttpStatusCode.OK);
        await Task.WhenAll(probes);

        // Assert
        handler.CallCount.Should().Be(1);
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

    [Test]
    public async Task A_caller_that_cancels_mid_probe_does_not_poison_the_cached_result()
    {
        // Arrange — the first caller's request is aborted while the probe is in flight.
        GatedHandler handler = new();
        WeatherProviderHealthCheck check = CreateCheck(handler);
        using CancellationTokenSource firstCaller = new();

        Task<HealthCheckResult> abandoned = check.CheckHealthAsync(Context, firstCaller.Token);
        await firstCaller.CancelAsync();

        // Act
        Func<Task> waitForAbandoned = () => abandoned;
        await waitForAbandoned.Should().ThrowAsync<OperationCanceledException>();
        handler.Release(HttpStatusCode.OK);
        HealthCheckResult next = await check.CheckHealthAsync(Context, CancellationToken.None);

        // Assert — the shared probe finished and cached the real answer, not "Degraded".
        using (new AssertionScope())
        {
            next.Status.Should().Be(HealthStatus.Healthy);
            handler.CallCount.Should().Be(1);
        }
    }

    /// <summary>Holds every request until <see cref="Release"/>, so a probe stays genuinely in flight.</summary>
    private sealed class GatedHandler : HttpMessageHandler
    {
        private readonly TaskCompletionSource<HttpStatusCode> _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _callCount;

        public int CallCount => _callCount;

        public void Release(HttpStatusCode status) => _gate.SetResult(status);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _callCount);
            HttpStatusCode status = await _gate.Task.WaitAsync(cancellationToken);
            return new HttpResponseMessage(status) { RequestMessage = request };
        }
    }
}
