using KAM.Common.DependencyInjection;
using KAM.Common.HealthChecks;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace AspNetDesignPatterns.Api.Features.Weather;

/// <summary>
/// Weather-feature DI: the domain service, the typed <see cref="IWeatherClient"/>, and the
/// feature's readiness health check. Discovered and invoked by the composition root via
/// <see cref="IDependency"/>.
/// </summary>
/// <remarks>
/// The Polly resilience pipeline is applied globally in <c>Program.cs</c> via
/// <c>ConfigureHttpClientDefaults(... AddStandardResilienceHandler())</c>, so every typed
/// client — including <see cref="IWeatherClient"/> — gets retry / circuit-breaker / timeout
/// for free. The health-check client is the exception: it calls
/// <c>RemoveAllResilienceHandlers()</c> so a probe is one fast request.
/// </remarks>
internal sealed class WeatherDependencies : IDependency
{
    public void RegisterServices(IServiceCollection services)
    {
        services.AddScoped<WeatherService>();

        services.AddHttpClient<IWeatherClient, OpenMeteoWeatherClient>((provider, client) =>
        {
            WeatherOptions options = provider.GetRequiredService<IOptions<WeatherOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseAddress);
            client.Timeout = options.RequestTimeout;
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
