using AspNetDesignPatterns.Api.Features.Weather;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

namespace AspNetDesignPatterns.Api.Tests.Features.Weather;

/// <summary>
/// <see cref="WeatherDependencies"/>'s <c>AddHttpClient&lt;IWeatherClient, OpenMeteoWeatherClient&gt;</c>
/// configuration delegate never runs under any other test — <c>WeatherApiFactory</c> substitutes
/// <see cref="IWeatherClient"/> outright, so nothing else proves <c>BaseAddress</c>/<c>Timeout</c>
/// are actually wired from <see cref="WeatherOptions"/>, nor that the resilience timeouts are.
/// </summary>
[TestFixture]
public class WeatherDependenciesTests
{
    [Test]
    public void RegisterServices_configures_the_typed_HttpClient_from_WeatherOptions()
    {
        // Arrange
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton(Microsoft.Extensions.Options.Options.Create(new WeatherOptions
        {
            BaseAddress = "https://weather.test",
            AttemptTimeout = TimeSpan.FromSeconds(20),
            TotalRequestTimeout = TimeSpan.FromSeconds(50),
        }));

        new WeatherDependencies().RegisterServices(services);

        using ServiceProvider provider = services.BuildServiceProvider();

        // Act — resolving IWeatherClient itself would need a real GetForecastAsync round-trip to
        // observe HttpClient state; going through the named client HttpClientFactory builds for
        // the typed client (named after TClient) lets this check BaseAddress/Timeout directly.
        using HttpClient client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(IWeatherClient));

        // Assert
        using (new AssertionScope())
        {
            client.BaseAddress.Should().Be(new Uri("https://weather.test"));
            client.Timeout.Should().Be(Timeout.InfiniteTimeSpan, "Polly owns timing; a finite HttpClient.Timeout would cut retries short");
        }
    }

    [Test]
    public void RegisterServices_drives_the_resilience_timeouts_from_WeatherOptions()
    {
        // Arrange
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton(Microsoft.Extensions.Options.Options.Create(new WeatherOptions
        {
            AttemptTimeout = TimeSpan.FromSeconds(20),
            TotalRequestTimeout = TimeSpan.FromSeconds(50),
        }));

        new WeatherDependencies().RegisterServices(services);

        using ServiceProvider provider = services.BuildServiceProvider();

        // Act — the standard handler's options are named "<client name>-standard".
        HttpStandardResilienceOptions resilience = provider
            .GetRequiredService<IOptionsMonitor<HttpStandardResilienceOptions>>()
            .Get($"{nameof(IWeatherClient)}-standard");

        // Assert
        using (new AssertionScope())
        {
            resilience.AttemptTimeout.Timeout.Should().Be(TimeSpan.FromSeconds(20));
            resilience.TotalRequestTimeout.Timeout.Should().Be(TimeSpan.FromSeconds(50));
            resilience.CircuitBreaker.SamplingDuration.Should().Be(TimeSpan.FromSeconds(40), "Polly requires at least 2 x the attempt timeout");
        }
    }
}
