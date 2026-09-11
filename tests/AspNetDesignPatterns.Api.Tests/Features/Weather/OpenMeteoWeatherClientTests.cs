using System.Net;
using System.Text.Json;

using AspNetDesignPatterns.Api.Features.Weather;
using AspNetDesignPatterns.Api.Tests.TestSupport;

using Microsoft.Extensions.Logging.Abstractions;

namespace AspNetDesignPatterns.Api.Tests.Features.Weather;

[TestFixture]
public class OpenMeteoWeatherClientTests
{
    private const string WELL_FORMED_BODY = """
        {
          "latitude": 52.5,
          "longitude": 13.4,
          "timezone": "UTC",
          "daily": {
            "time": ["2026-09-09", "2026-09-10"],
            "temperature_2m_max": [20.1, 21.2],
            "temperature_2m_min": [10.4, 11.9],
            "precipitation_sum": [0.0, 2.5]
          }
        }
        """;

    private static (OpenMeteoWeatherClient Client, StubHttpMessageHandler Handler) CreateClient(StubHttpMessageHandler handler)
    {
        HttpClient httpClient = new(handler) { BaseAddress = new Uri("https://weather.test") };
        JsonSerializerOptions jsonOptions = new() { PropertyNameCaseInsensitive = true };

        return (new OpenMeteoWeatherClient(httpClient, jsonOptions, NullLogger<OpenMeteoWeatherClient>.Instance), handler);
    }

    [Test]
    public async Task Builds_a_culture_invariant_URL_encoded_query_string()
    {
        // Arrange
        (OpenMeteoWeatherClient client, StubHttpMessageHandler handler) =
            CreateClient(new StubHttpMessageHandler(HttpStatusCode.OK, WELL_FORMED_BODY));

        // Act — a comma-formatting culture would break this if the ToString calls were removed.
        using (new SwedishCulture())
        {
            await client.GetForecastAsync(52.5, 13.4, 3, CancellationToken.None);
        }

        // Assert
        string query = handler.LastRequest!.RequestUri!.Query;

        using (new AssertionScope())
        {
            // A culture-sensitive ToString would render these as "52,5" / "13,4" under sv-SE.
            query.Should().Contain("latitude=52.5").And.Contain("longitude=13.4");
            query.Should().Contain("forecast_days=3");
        }
    }

    [Test]
    public async Task Returns_the_deserialized_forecast_on_success()
    {
        // Arrange
        (OpenMeteoWeatherClient client, _) = CreateClient(new StubHttpMessageHandler(HttpStatusCode.OK, WELL_FORMED_BODY));

        // Act
        OpenMeteoForecast? forecast = await client.GetForecastAsync(52.5, 13.4, 2, CancellationToken.None);

        // Assert
        using (new AssertionScope())
        {
            forecast.Should().NotBeNull();
            forecast!.Daily!.Time.Should().HaveCount(2);
            forecast.Daily.TemperatureMax[0].Should().Be(20.1);
        }
    }

    [Test]
    public async Task Returns_null_when_the_provider_rejects_the_request_as_a_bad_request()
    {
        // Arrange
        (OpenMeteoWeatherClient client, _) = CreateClient(new StubHttpMessageHandler(HttpStatusCode.BadRequest));

        // Act
        OpenMeteoForecast? forecast = await client.GetForecastAsync(999, 0, 1, CancellationToken.None);

        // Assert
        forecast.Should().BeNull();
    }

    [Test]
    public Task Throws_WeatherClientException_when_the_response_is_a_server_error()
    {
        // Arrange
        (OpenMeteoWeatherClient client, _) = CreateClient(new StubHttpMessageHandler(HttpStatusCode.ServiceUnavailable));

        // Act
        Func<Task> act = () => client.GetForecastAsync(0, 0, 1, CancellationToken.None);

        // Assert
        return act.Should().ThrowAsync<WeatherClientException>();
    }

    [Test]
    public async Task Throws_WeatherClientException_when_the_underlying_request_fails()
    {
        // Arrange
        (OpenMeteoWeatherClient client, _) = CreateClient(new StubHttpMessageHandler(new HttpRequestException("no route")));

        // Act
        Func<Task> act = () => client.GetForecastAsync(0, 0, 1, CancellationToken.None);

        // Assert
        (await act.Should().ThrowAsync<WeatherClientException>()).WithInnerException<HttpRequestException>();
    }

    [Test]
    public Task Throws_WeatherClientException_when_the_response_body_is_not_valid_json()
    {
        // Arrange
        (OpenMeteoWeatherClient client, _) = CreateClient(new StubHttpMessageHandler(HttpStatusCode.OK, "not json"));

        // Act
        Func<Task> act = () => client.GetForecastAsync(0, 0, 1, CancellationToken.None);

        // Assert
        return act.Should().ThrowAsync<WeatherClientException>();
    }

    [Test]
    public Task Wraps_a_provider_side_timeout_as_a_WeatherClientException()
    {
        // Arrange — HttpClient reports its own configured timeout the same way it reports
        // caller cancellation: TaskCanceledException. CancellationToken.None distinguishes it.
        (OpenMeteoWeatherClient client, _) = CreateClient(new StubHttpMessageHandler(new TaskCanceledException("timed out")));

        // Act
        Func<Task> act = () => client.GetForecastAsync(0, 0, 1, CancellationToken.None);

        // Assert
        return act.Should().ThrowAsync<WeatherClientException>();
    }

    [Test]
    public async Task Propagates_caller_cancellation_without_wrapping_it()
    {
        // Arrange
        (OpenMeteoWeatherClient client, _) = CreateClient(new StubHttpMessageHandler(HttpStatusCode.OK, WELL_FORMED_BODY));
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        // Act
        Func<Task> act = () => client.GetForecastAsync(0, 0, 1, cts.Token);

        // Assert — TaskCanceledException, not WeatherClientException: a cancelled request is
        // not "the provider is unavailable".
        await act.Should().ThrowAsync<TaskCanceledException>();
    }

    [TestCase("""{"latitude":0,"longitude":0,"timezone":"UTC","daily":{"time":["2026-09-09","2026-09-10"],"temperature_2m_max":[20.1],"temperature_2m_min":[10.4,11.9],"precipitation_sum":[0.0,2.5]}}""")]
    [TestCase("""{"latitude":0,"longitude":0,"timezone":"UTC","daily":{"time":["2026-09-09"],"temperature_2m_max":null,"temperature_2m_min":[10.4],"precipitation_sum":[0.0]}}""")]
    public Task Throws_WeatherClientException_when_the_daily_arrays_are_missing_or_mismatched(string malformedBody)
    {
        // Arrange
        (OpenMeteoWeatherClient client, _) = CreateClient(new StubHttpMessageHandler(HttpStatusCode.OK, malformedBody));

        // Act
        Func<Task> act = () => client.GetForecastAsync(0, 0, 1, CancellationToken.None);

        // Assert — this is the shape that used to reach WeatherService.Map and throw an
        // uncaught NullReferenceException / IndexOutOfRangeException several layers up.
        return act.Should().ThrowAsync<WeatherClientException>();
    }

    /// <summary>Forces a culture whose number formatting would break an un-invariant query string.</summary>
    private sealed class SwedishCulture : IDisposable
    {
        private readonly System.Globalization.CultureInfo _original = System.Threading.Thread.CurrentThread.CurrentCulture;

        public SwedishCulture() =>
            System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("sv-SE");

        public void Dispose() => System.Threading.Thread.CurrentThread.CurrentCulture = _original;
    }
}
