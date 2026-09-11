using AspNetDesignPatterns.Api.Features.Weather;

using KAM.Common.Results;

using NSubstitute;
using NSubstitute.ClearExtensions;
using NSubstitute.ExceptionExtensions;

namespace AspNetDesignPatterns.Api.Tests.Features.Weather;

[TestFixture]
public class WeatherServiceTests
{
    private readonly IWeatherClient _client = Substitute.For<IWeatherClient>();

    private WeatherService CreateService() => new(_client);

    private static readonly GetForecastRequest Request = new(52.5, 13.4, 2);

    [SetUp]
    public void Reset() => _client.ClearSubstitute();

    [Test]
    public async Task Maps_the_upstream_payload_to_the_api_contract()
    {
        // Arrange
        _client.GetForecastAsync(52.5, 13.4, 2, Arg.Any<CancellationToken>()).Returns(new OpenMeteoForecast(
            52.5, 13.4, "UTC",
            new OpenMeteoDaily(
                [new DateOnly(2026, 9, 9), new DateOnly(2026, 9, 10)],
                [20.1, 21.2],
                [10.4, 11.9],
                [0.0, 2.5])));

        // Act
        Result<ForecastResponse> result = await CreateService().GetForecastAsync(Request, CancellationToken.None);

        // Assert
        using (new AssertionScope())
        {
            result.IsSuccess.Should().BeTrue();
            result.Value.Days.Should().HaveCount(2);
            result.Value.Days[0].TemperatureMaxC.Should().Be(20.1);
            result.Value.Days[1].PrecipitationMm.Should().Be(2.5);
        }
    }

    [Test]
    public async Task Returns_NotFound_when_the_provider_has_no_data()
    {
        // Arrange
        _client.GetForecastAsync(default, default, default, default).ReturnsForAnyArgs((OpenMeteoForecast?) null);

        // Act
        Result<ForecastResponse> result = await CreateService().GetForecastAsync(Request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Test]
    public async Task Converts_a_client_exception_into_an_upstream_error()
    {
        // Arrange
        _client.GetForecastAsync(default, default, default, default)
            .ThrowsAsyncForAnyArgs(new WeatherClientException("boom", new HttpRequestException()));

        // Act
        Result<ForecastResponse> result = await CreateService().GetForecastAsync(Request, CancellationToken.None);

        // Assert
        using (new AssertionScope())
        {
            result.IsFailure.Should().BeTrue();
            result.Error.Type.Should().Be(ErrorType.Upstream);
            result.Error.Code.Should().Be("Weather.ProviderUnavailable");
            result.Error.Exception.Should().BeOfType<WeatherClientException>();
            result.Error.Context.Should().NotBeNull();
        }
    }
}
