using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using AspNetDesignPatterns.Api.Features.Weather;
using AspNetDesignPatterns.Api.Tests.TestSupport;

using KAM.Common.Authentication;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using NSubstitute;

namespace AspNetDesignPatterns.Api.Tests.Integration;

/// <summary>
/// Boots the real application in memory with three seams: deterministic JWT settings, a
/// substitute <see cref="IWeatherClient"/> so tests never touch the network, and a private
/// in-memory SQLite database for the Library slice (migrated and seeded by the app's own
/// startup, exactly as in production) so tests never touch the disk.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private const string API_V1 = "/api/v1";

    private const string SIGNING_KEY = "integration-tests-signing-key-0123456789";
    private const string ISSUER = "https://tests/aspnetdesignpatterns";
    private const string AUDIENCE = "aspnetdesignpatterns.tests";

    // A shared-cache in-memory database exists only while a connection to it is open: this one
    // keeps it alive for the factory's lifetime, across every request's own connection.
    private readonly string _libraryConnectionString = $"Data Source=library-tests-{Guid.NewGuid():N};Mode=Memory;Cache=Shared;Foreign Keys=True";
    private readonly SqliteConnection _libraryKeepAlive;

    public ApiFactory()
    {
        _libraryKeepAlive = new SqliteConnection(_libraryConnectionString);
        _libraryKeepAlive.Open();
    }

    public IWeatherClient WeatherClient { get; } = Substitute.For<IWeatherClient>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:SigningKey"] = SIGNING_KEY,
            ["Jwt:Issuer"] = ISSUER,
            ["Jwt:Audience"] = AUDIENCE,
            ["Weather:BaseAddress"] = "https://weather.test",
            ["Library:ConnectionString"] = _libraryConnectionString,
        }));

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IWeatherClient>();
            services.AddSingleton(WeatherClient);

            // Keep the weather readiness probe (WeatherProviderHealthCheck) off the network and
            // deterministic — it answers 200 so /health/ready is reproducibly Healthy.
            services.AddHttpClient<WeatherProviderHealthCheck>()
                .ConfigurePrimaryHttpMessageHandler(() => new StubHttpMessageHandler(HttpStatusCode.OK));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _libraryKeepAlive.Dispose();
        }
    }

    /// <summary>Creates a client carrying a valid bearer token minted by the dev-token endpoint.</summary>
    public async Task<HttpClient> CreateAuthenticatedClientAsync(string subject = "test-user")
    {
        HttpClient client = CreateClient();
        HttpResponseMessage response = await client.PostAsJsonAsync($"{API_V1}/auth/token", new TokenRequest(subject));
        response.EnsureSuccessStatusCode();

        TokenResponse? token = await response.Content.ReadFromJsonAsync<TokenResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token!.AccessToken);
        return client;
    }
}
