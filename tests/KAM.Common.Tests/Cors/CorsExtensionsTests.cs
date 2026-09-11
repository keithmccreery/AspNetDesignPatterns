using KAM.Common.Cors;

using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace KAM.Common.Tests.Cors;

[TestFixture]
public class CorsExtensionsTests
{
    private static CorsPolicy BuildPolicy(CorsSettings settings)
    {
        ServiceCollection services = new();
        services.AddSingleton(Microsoft.Extensions.Options.Options.Create(settings));
        services.AddCorsPolicy();
        using ServiceProvider provider = services.BuildServiceProvider();

        CorsOptions options = provider.GetRequiredService<IOptions<CorsOptions>>().Value;
        return options.GetPolicy(CorsExtensions.PolicyName)!;
    }

    [Test]
    public void Configures_explicit_origins_headers_and_methods()
    {
        // Arrange & Act
        CorsPolicy policy = BuildPolicy(new CorsSettings
        {
            AllowedOrigins = ["https://app.example.com"],
            AllowedHeaders = ["content-type", "authorization"],
            AllowedMethods = ["GET", "POST"],
            AllowCredentials = false,
        });

        // Assert
        using (new AssertionScope())
        {
            policy.Origins.Should().BeEquivalentTo("https://app.example.com");
            policy.Headers.Should().BeEquivalentTo("content-type", "authorization");
            policy.Methods.Should().BeEquivalentTo("GET", "POST");
            policy.SupportsCredentials.Should().BeFalse();
        }
    }

    [Test]
    public void Treats_a_wildcard_origin_as_AllowAnyOrigin_not_a_literal_origin()
    {
        // Arrange & Act
        CorsPolicy policy = BuildPolicy(new CorsSettings { AllowedOrigins = ["*"], AllowCredentials = false });

        // Assert
        policy.AllowAnyOrigin.Should().BeTrue();
    }

    [Test]
    public void Treats_a_wildcard_header_as_AllowAnyHeader_not_a_literal_header()
    {
        // Arrange — WithHeaders("*") would treat "*" as a literal header name, not a wildcard.
        CorsPolicy policy = BuildPolicy(new CorsSettings
        {
            AllowedOrigins = ["https://app.example.com"],
            AllowedHeaders = ["*"],
        });

        // Act & Assert
        policy.AllowAnyHeader.Should().BeTrue();
    }

    [Test]
    public void Treats_a_wildcard_method_as_AllowAnyMethod_not_a_literal_method()
    {
        // Arrange & Act
        CorsPolicy policy = BuildPolicy(new CorsSettings
        {
            AllowedOrigins = ["https://app.example.com"],
            AllowedMethods = ["*"],
        });

        // Assert
        policy.AllowAnyMethod.Should().BeTrue();
    }

    [Test]
    public void Applies_credentials_and_preflight_max_age()
    {
        // Arrange & Act
        CorsPolicy policy = BuildPolicy(new CorsSettings
        {
            AllowedOrigins = ["https://app.example.com"],
            AllowCredentials = true,
            MaxAgeSeconds = 120,
        });

        // Assert
        using (new AssertionScope())
        {
            policy.SupportsCredentials.Should().BeTrue();
            policy.PreflightMaxAge.Should().Be(TimeSpan.FromSeconds(120));
        }
    }
}
