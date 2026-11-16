using System.Security.Claims;

using AspNetDesignPatterns.Api.Features.Library;
using AspNetDesignPatterns.Api.Tests.TestSupport;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AspNetDesignPatterns.Api.Tests.Features.Library;

/// <summary><see cref="MemberId"/> binding, <see cref="SqliteConnectionFactory"/> path resolution, and the health check.</summary>
[TestFixture]
public class LibraryInfrastructureTests
{
    [Test]
    public async Task MemberId_binds_from_the_sub_claim()
    {
        // Arrange
        DefaultHttpContext context = new() { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "alice")], "test")) };

        // Act
        MemberId? member = await MemberId.BindAsync(context);

        // Assert
        member.Should().Be(new MemberId("alice"));
    }

    [Test]
    public async Task MemberId_does_not_bind_without_a_sub_claim()
    {
        // Arrange
        DefaultHttpContext context = new() { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("name", "alice")], "test")) };

        // Act
        MemberId? member = await MemberId.BindAsync(context);

        // Assert
        member.Should().BeNull();
    }

    [Test]
    public void A_relative_database_file_is_placed_under_the_content_root()
    {
        // Act
        string resolved = SqliteConnectionFactory.ResolveDataSource("Data Source=library.db;Foreign Keys=True", "/srv/app");

        // Assert
        resolved.Should().Contain("Data Source=/srv/app/library.db");
    }

    [TestCase("Data Source=/var/data/library.db")]
    [TestCase("Data Source=:memory:")]
    [TestCase("Data Source=shared;Mode=Memory;Cache=Shared")]
    public void Absolute_and_in_memory_data_sources_are_left_alone(string connectionString)
    {
        // Act
        string resolved = SqliteConnectionFactory.ResolveDataSource(connectionString, "/srv/app");

        // Assert
        resolved.Should().NotContain("/srv/app");
    }

    [Test]
    public async Task The_health_check_is_healthy_when_the_database_answers()
    {
        // Arrange
        using LibraryTestDatabase database = await LibraryTestDatabase.CreateAsync();

        // Act
        HealthCheckResult result = await new LibraryDatabaseHealthCheck(database).CheckHealthAsync(Context());

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Test]
    public async Task The_health_check_reports_its_failure_status_when_the_database_is_down()
    {
        // Act
        HealthCheckResult result = await new LibraryDatabaseHealthCheck(new UnavailableDatabase()).CheckHealthAsync(Context());

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Exception.Should().NotBeNull();
    }

    private static HealthCheckContext Context() => new()
    {
        Registration = new HealthCheckRegistration("library-database", _ => null!, HealthStatus.Unhealthy, tags: null),
    };
}
