using System.Text.Json;
using System.Text.Json.Serialization;

using AspNetDesignPatterns.Api.Shared.HealthChecks;
using AspNetDesignPatterns.Api.Shared.Tests.TestSupport;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace AspNetDesignPatterns.Api.Shared.Tests.HealthChecks;

[TestFixture]
public class HealthCheckResponseWriterTests
{
    private static HealthReport Report()
    {
        Dictionary<string, HealthReportEntry> entries = new(StringComparer.Ordinal)
        {
            ["weather-provider"] = new HealthReportEntry(
                status: HealthStatus.Degraded,
                description: "open-meteo is unreachable.",
                duration: TimeSpan.FromMilliseconds(42),
                exception: new HttpRequestException("no such host"),
                data: null,
                tags: ["ready"]),
        };

        return new HealthReport(entries, HealthStatus.Degraded, TimeSpan.FromMilliseconds(55));
    }

    private static async Task<JsonElement> WriteAndParseAsync(HealthReport report, string environmentName = "Development")
    {
        ServiceCollection services = new();
        services.AddSingleton(new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, // matches the app's real shared options
        });
        services.AddSingleton<IHostEnvironment>(new FakeHostEnvironment(environmentName));

        DefaultHttpContext context = new() { RequestServices = services.BuildServiceProvider() };
        context.Response.Body = new MemoryStream();

        await HealthCheckResponseWriter.WriteAsync(context, report);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using StreamReader reader = new(context.Response.Body);
        return JsonDocument.Parse(await reader.ReadToEndAsync(context.RequestAborted)).RootElement;
    }

    [Test]
    public async Task Writes_the_overall_status_and_total_duration()
    {
        // Arrange & Act
        JsonElement json = await WriteAndParseAsync(Report());

        // Assert
        using (new AssertionScope())
        {
            json.GetProperty("status").GetString().Should().Be("Degraded");
            json.GetProperty("totalDurationMs").GetDouble().Should().BeApproximately(55, 0.001);
        }
    }

    [Test]
    public async Task Writes_one_entry_per_check_with_status_duration_and_tags()
    {
        // Arrange & Act
        JsonElement json = await WriteAndParseAsync(Report());

        // Assert
        JsonElement entry = json.GetProperty("entries").GetProperty("weather-provider");

        using (new AssertionScope())
        {
            entry.GetProperty("status").GetString().Should().Be("Degraded");
            entry.GetProperty("durationMs").GetDouble().Should().BeApproximately(42, 0.001);
            entry.GetProperty("tags").EnumerateArray().Select(t => t.GetString()).Should().Equal("ready");
        }
    }

    [TestCase("Development")]
    [TestCase("Staging")]
    public async Task Includes_the_description_and_exception_message_outside_Production(string environmentName)
    {
        // Arrange & Act
        JsonElement json = await WriteAndParseAsync(Report(), environmentName);

        // Assert
        JsonElement entry = json.GetProperty("entries").GetProperty("weather-provider");

        using (new AssertionScope())
        {
            entry.GetProperty("description").GetString().Should().Be("open-meteo is unreachable.");
            entry.GetProperty("error").GetString().Should().Be("no such host");
        }
    }

    [Test]
    public async Task Omits_the_description_and_exception_message_in_Production()
    {
        // Arrange & Act — /health/ready is anonymous; Exception.Message can carry a hostname,
        // port, or connection-string fragment for a check this file didn't anticipate.
        JsonElement json = await WriteAndParseAsync(Report(), "Production");

        // Assert
        JsonElement entry = json.GetProperty("entries").GetProperty("weather-provider");

        using (new AssertionScope())
        {
            entry.TryGetProperty("description", out _).Should().BeFalse();
            entry.TryGetProperty("error", out _).Should().BeFalse();
        }
    }

    [Test]
    public async Task Sets_a_json_content_type()
    {
        // Arrange
        ServiceCollection services = new();
        services.AddSingleton(new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, // matches the app's real shared options
        });
        services.AddSingleton<IHostEnvironment>(new FakeHostEnvironment("Development"));
        DefaultHttpContext context = new() { RequestServices = services.BuildServiceProvider() };
        context.Response.Body = new MemoryStream();

        // Act
        await HealthCheckResponseWriter.WriteAsync(context, Report());

        // Assert
        context.Response.ContentType.Should().StartWith("application/json");
    }
}
