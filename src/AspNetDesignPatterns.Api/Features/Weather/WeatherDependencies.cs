using AspNetDesignPatterns.Api.DependencyInjection;

using Microsoft.Extensions.Options;

namespace AspNetDesignPatterns.Api.Features.Weather;

/// <summary>
/// Weather-feature DI: the domain service plus the typed <see cref="IWeatherClient"/>.
/// Discovered and invoked by the composition root via <see cref="IDependency"/>.
/// </summary>
/// <remarks>
/// The Polly resilience pipeline is applied globally in <c>Program.cs</c> via
/// <c>ConfigureHttpClientDefaults(... AddStandardResilienceHandler())</c>, so every typed
/// client — including this one — gets retry / circuit-breaker / timeout for free. A client
/// that needs different behaviour can call <c>RemoveAllResilienceHandlers()</c> and add its own.
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
    }
}
