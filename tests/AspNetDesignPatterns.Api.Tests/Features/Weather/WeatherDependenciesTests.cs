using AspNetDesignPatterns.Api.Features.Weather;

using Microsoft.Extensions.DependencyInjection;

namespace AspNetDesignPatterns.Api.Tests.Features.Weather;

/// <summary>
/// <see cref="WeatherDependencies"/>'s <c>AddHttpClient&lt;IWeatherClient, OpenMeteoWeatherClient&gt;</c>
/// configuration delegate never runs under any other test — <c>WeatherApiFactory</c> substitutes
/// <see cref="IWeatherClient"/> outright, so nothing else proves <c>BaseAddress</c>/<c>Timeout</c>
/// are actually wired from <see cref="WeatherOptions"/>.
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
            RequestTimeout = TimeSpan.FromSeconds(7),
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
            client.Timeout.Should().Be(TimeSpan.FromSeconds(7));
        }
    }
}
