using KAM.Common.DependencyInjection;
using KAM.Common.HealthChecks;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

namespace AspNetDesignPatterns.Api.Features.Weather;

/// <summary>
/// Weather-feature DI: the service (<see cref="IWeatherService"/>), the typed <see cref="IWeatherClient"/>, and the
/// feature's readiness health check. Discovered and invoked by the composition root via
/// <see cref="IDependency"/>.
/// </summary>
/// <remarks>
/// The Polly resilience pipeline is applied globally in <c>Program.cs</c> via
/// <c>ConfigureHttpClientDefaults(... AddStandardResilienceHandler())</c>, so every typed
/// client gets retry / circuit-breaker / timeout for free. Two clients here replace it:
/// <see cref="IWeatherClient"/> swaps in a standard handler whose attempt/total timeouts come
/// from <see cref="WeatherOptions"/> (with <c>HttpClient.Timeout</c> infinite, so Polly alone
/// owns timing), and the health-check client removes it entirely so a probe is one fast request.
/// </remarks>
internal sealed class WeatherDependencies : IDependency
{
    public void RegisterServices(IServiceCollection services)
    {
        services.AddScoped<IWeatherService, WeatherService>();

        services.AddHttpClient<IWeatherClient, OpenMeteoWeatherClient>((provider, client) =>
            {
                WeatherOptions options = provider.GetRequiredService<IOptions<WeatherOptions>>().Value;
                client.BaseAddress = new Uri(options.BaseAddress);

                // HttpClient.Timeout wraps the WHOLE handler chain, retries included, so any
                // finite value here races Polly's own timeouts (at 10s it cancelled the request
                // exactly when the first attempt timed out -- no retry ever ran). Polly owns
                // timing: per-attempt and total timeouts are configured on the handler below.
                client.Timeout = Timeout.InfiniteTimeSpan;
            })
            // Replace the global default handler with one whose timeouts come from WeatherOptions.
            .RemoveAllResilienceHandlers()
            .AddStandardResilienceHandler()
            .Configure((resilience, provider) =>
            {
                WeatherOptions options = provider.GetRequiredService<IOptions<WeatherOptions>>().Value;
                resilience.AttemptTimeout.Timeout = options.AttemptTimeout;
                resilience.TotalRequestTimeout.Timeout = options.TotalRequestTimeout;

                // Polly validates SamplingDuration >= 2 x AttemptTimeout; keep the default (30s)
                // unless a longer attempt timeout needs a wider window.
                TimeSpan minimumSampling = options.AttemptTimeout * 2;
                if (resilience.CircuitBreaker.SamplingDuration < minimumSampling)
                {
                    resilience.CircuitBreaker.SamplingDuration = minimumSampling;
                }
            });

        services.AddHttpClient<WeatherProviderHealthCheck>((provider, client) =>
        {
            WeatherOptions options = provider.GetRequiredService<IOptions<WeatherOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseAddress);
            client.Timeout = TimeSpan.FromSeconds(5);
        })
        .RemoveAllResilienceHandlers();

        services.AddHealthChecks().AddCheck<WeatherProviderHealthCheck>(
            "weather-provider",
            failureStatus: HealthStatus.Degraded,
            tags: [HealthCheckTag.Ready]);
    }
}
