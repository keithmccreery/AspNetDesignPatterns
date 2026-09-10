using AspNetDesignPatterns.Api.Features.Weather;
using AspNetDesignPatterns.Api.Shared.Pipeline;
using AspNetDesignPatterns.Api.Shared.Results;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using NSubstitute;
using NSubstitute.ClearExtensions;

namespace AspNetDesignPatterns.Api.Tests.Features.Weather;

[TestFixture]
public class GetForecastHandlerTests
{
    private readonly IWeatherClient _client = Substitute.For<IWeatherClient>();

    [SetUp]
    public void Reset() => _client.ClearSubstitute();

    private GetForecastHandler CreateHandler(int maxForecastDays = 16)
    {
        ServiceCollection services = new();
        services.AddSingleton(Options.Create(new WeatherOptions { MaxForecastDays = maxForecastDays }));
        services.AddSingleton(_client);
        services.AddScoped<WeatherService>();
        services.AddScoped<ClampForecastWindowStep>();
        services.AddScoped<FetchForecastStep>();
        services.AddScoped<RoundTemperaturesStep>();
        services.AddSingleton<IPipelineFactory, PipelineFactory>();

        return new GetForecastHandler(services.BuildServiceProvider().GetRequiredService<IPipelineFactory>());
    }

    [Test]
    public async Task Runs_the_pipeline_and_rounds_temperatures()
    {
        // Arrange
        _client.GetForecastAsync(default, default, default, default).ReturnsForAnyArgs(new OpenMeteoForecast(
            1, 2, "UTC",
            new OpenMeteoDaily([new DateOnly(2026, 9, 9)], [20.148], [10.052], [0.0])));

        // Act
        Result<ForecastResponse> result = await CreateHandler().HandleAsync(new GetForecastRequest(1, 2, 1), CancellationToken.None);

        // Assert
        using (new AssertionScope())
        {
            result.IsSuccess.Should().BeTrue();
            result.Value.Days[0].TemperatureMaxC.Should().Be(20.1);
            result.Value.Days[0].TemperatureMinC.Should().Be(10.1);
        }
    }

    [Test]
    public async Task Fails_validation_when_the_window_exceeds_the_configured_maximum()
    {
        // Arrange & Act
        Result<ForecastResponse> result = await CreateHandler(maxForecastDays: 3)
            .HandleAsync(new GetForecastRequest(1, 2, 10), CancellationToken.None);

        // Assert
        using (new AssertionScope())
        {
            result.IsFailure.Should().BeTrue();
            result.Error.Type.Should().Be(ErrorType.Validation);
            result.Error.Code.Should().Be("Weather.ForecastWindowTooLarge");
        }

        await _client.DidNotReceiveWithAnyArgs().GetForecastAsync(default, default, default, default);
    }
}
