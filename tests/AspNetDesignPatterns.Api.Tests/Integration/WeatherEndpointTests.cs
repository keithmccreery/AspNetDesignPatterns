using System.Net;
using System.Net.Http.Json;

using AspNetDesignPatterns.Api.Features.Weather;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using NSubstitute;
using NSubstitute.ClearExtensions;
using NSubstitute.ExceptionExtensions;

namespace AspNetDesignPatterns.Api.Tests.Integration;

[TestFixture]
public class WeatherEndpointTests
{
    private const string VALID_ROUTE = "/api/v1/weather/forecast?latitude=52.5&longitude=13.4&days=2";

    private static WeatherApiFactory Factory => GlobalTestSetup.Factory;

    [SetUp]
    public void ResetSubstitute() => Factory.WeatherClient.ClearSubstitute();

    [Test]
    public async Task Returns_401_without_a_token()
    {
        HttpResponseMessage response = await Factory.CreateClient().GetAsync(VALID_ROUTE);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Returns_the_mapped_forecast_with_a_valid_token()
    {
        Factory.WeatherClient.GetForecastAsync(default, default, default, default).ReturnsForAnyArgs(
            new OpenMeteoForecast(52.5, 13.4, "UTC", new OpenMeteoDaily(
                [new DateOnly(2026, 9, 9), new DateOnly(2026, 9, 10)],
                [20.44, 19.51],
                [11.2, 10.8],
                [0.0, 1.0])));

        HttpClient client = await Factory.CreateAuthenticatedClientAsync();
        ForecastResponse? forecast = await client.GetFromJsonAsync<ForecastResponse>(VALID_ROUTE);

        forecast.Should().NotBeNull();
        forecast!.Days.Should().HaveCount(2);
        forecast.Days[0].TemperatureMaxC.Should().Be(20.4); // rounded by the pipeline
    }

    [Test]
    public async Task Returns_400_problem_details_for_an_out_of_range_parameter()
    {
        HttpClient client = await Factory.CreateAuthenticatedClientAsync();

        HttpResponseMessage response = await client.GetAsync("/api/v1/weather/forecast?latitude=999&longitude=13.4&days=2");
        HttpValidationProblemDetails? problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem!.Errors.Should().ContainKey("Latitude");
    }

    [Test]
    public async Task Returns_502_problem_details_when_the_provider_throws()
    {
        Factory.WeatherClient.GetForecastAsync(default, default, default, default)
            .ThrowsAsyncForAnyArgs(new WeatherClientException("down", new HttpRequestException()));

        HttpClient client = await Factory.CreateAuthenticatedClientAsync();

        HttpResponseMessage response = await client.GetAsync(VALID_ROUTE);
        ProblemDetails? problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        problem!.Extensions.Should().ContainKey("errorCode");
    }
}
