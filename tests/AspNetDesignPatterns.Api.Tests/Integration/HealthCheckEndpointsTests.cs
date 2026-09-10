using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace AspNetDesignPatterns.Api.Tests.Integration;

[TestFixture]
public class HealthCheckEndpointsTests
{
    private static WeatherApiFactory Factory => GlobalTestSetup.Factory;

    private static async Task<(HttpResponseMessage Response, JsonElement Body)> GetAsync(string path)
    {
        HttpResponseMessage response = await Factory.CreateClient().GetAsync(path);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return (response, body);
    }

    [Test]
    public async Task Liveness_probe_is_anonymous_and_runs_no_checks()
    {
        // Arrange & Act
        (HttpResponseMessage response, JsonElement body) = await GetAsync("/health/live");

        // Assert
        using (new AssertionScope())
        {
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
            body.GetProperty("status").GetString().Should().Be("Healthy");
            body.GetProperty("entries").EnumerateObject().Should().BeEmpty();
        }
    }

    [Test]
    public async Task Readiness_probe_runs_the_ready_tagged_checks()
    {
        // Arrange & Act
        (HttpResponseMessage response, JsonElement body) = await GetAsync("/health/ready");

        // Assert
        using (new AssertionScope())
        {
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            body.GetProperty("status").GetString().Should().Be("Healthy");
            body.GetProperty("entries").GetProperty("weather-provider")
                .GetProperty("status").GetString().Should().Be("Healthy");
        }
    }

    [Test]
    public async Task Health_endpoints_are_absent_from_the_openapi_document()
    {
        // Arrange & Act
        JsonElement document = await Factory.CreateClient().GetFromJsonAsync<JsonElement>("/openapi/v1.json");

        // Assert
        document.GetProperty("paths").EnumerateObject().Select(p => p.Name)
            .Should().NotContain(path => path.StartsWith("/health", StringComparison.Ordinal));
    }
}
