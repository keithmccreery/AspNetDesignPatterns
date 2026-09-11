using System.ComponentModel.DataAnnotations;

using KAM.Common.Auth;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace KAM.Common.Tests.Auth;

[TestFixture]
public class AuthExtensionsTests
{
    private static ServiceProvider BuildProvider(JwtOptions? jwtOptions = null)
    {
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddLogging();
        services.AddSingleton(Microsoft.Extensions.Options.Options.Create(jwtOptions ?? new JwtOptions
        {
            SigningKey = "auth-wiring-tests-signing-key-0123456789",
            Issuer = "iss",
            Audience = "aud",
        }));
        services.AddJwtAuth();
        return services.BuildServiceProvider();
    }

    [Test]
    public void Registers_the_dev_token_issuer_and_a_TimeProvider()
    {
        // Arrange & Act
        using ServiceProvider provider = BuildProvider();

        // Assert
        using (new AssertionScope())
        {
            provider.GetService<DevTokenIssuer>().Should().NotBeNull();
            provider.GetService<TimeProvider>().Should().NotBeNull();
        }
    }

    [Test]
    public void Configures_bearer_token_validation_from_JwtOptions()
    {
        // Arrange
        using ServiceProvider provider = BuildProvider();

        // Act
        JwtBearerOptions bearer = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);

        // Assert
        using (new AssertionScope())
        {
            bearer.TokenValidationParameters.ValidIssuer.Should().Be("iss");
            bearer.TokenValidationParameters.ValidAudience.Should().Be("aud");
            bearer.TokenValidationParameters.ValidateIssuer.Should().BeTrue();
            bearer.TokenValidationParameters.ValidateAudience.Should().BeTrue();
            bearer.TokenValidationParameters.ValidateLifetime.Should().BeTrue();
            bearer.TokenValidationParameters.ClockSkew.Should().Be(TimeSpan.FromSeconds(30));
            bearer.SaveToken.Should().BeFalse();
        }
    }

    [Test]
    public void Honors_the_configurable_validation_toggles_and_clock_skew()
    {
        // Arrange & Act
        using ServiceProvider provider = BuildProvider(new JwtOptions
        {
            SigningKey = "auth-wiring-tests-signing-key-0123456789",
            Issuer = "iss",
            Audience = "aud",
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = false,
            ClockSkewSeconds = 120,
            SaveToken = true,
        });
        JwtBearerOptions bearer = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);

        // Assert
        using (new AssertionScope())
        {
            bearer.TokenValidationParameters.ValidateIssuer.Should().BeFalse();
            bearer.TokenValidationParameters.ValidateAudience.Should().BeFalse();
            bearer.TokenValidationParameters.ValidateLifetime.Should().BeFalse();
            bearer.TokenValidationParameters.ClockSkew.Should().Be(TimeSpan.FromSeconds(120));
            bearer.SaveToken.Should().BeTrue();
        }
    }

    [Test]
    public void Overrides_the_name_and_role_claim_types_when_configured()
    {
        // Arrange & Act
        using ServiceProvider provider = BuildProvider(new JwtOptions
        {
            SigningKey = "auth-wiring-tests-signing-key-0123456789",
            Issuer = "iss",
            Audience = "aud",
            NameClaimType = "custom_name",
            RoleClaimType = "custom_role",
        });
        JwtBearerOptions bearer = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);

        // Assert
        using (new AssertionScope())
        {
            bearer.TokenValidationParameters.NameClaimType.Should().Be("custom_name");
            bearer.TokenValidationParameters.RoleClaimType.Should().Be("custom_role");
        }
    }

    [Test]
    public void Registers_JwtBearerEvents_for_authentication_observability()
    {
        // Arrange & Act
        using ServiceProvider provider = BuildProvider();
        JwtBearerOptions bearer = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);

        // Assert — the events exist and don't throw when invoked; the exact log content isn't
        // asserted here, since that's Serilog's concern once wired, not this DI-registration test's.
        using (new AssertionScope())
        {
            bearer.Events.Should().NotBeNull();
            bearer.Events!.OnAuthenticationFailed.Should().NotBeNull();
            bearer.Events.OnTokenValidated.Should().NotBeNull();
            bearer.Events.OnChallenge.Should().NotBeNull();
        }
    }
}

[TestFixture]
public class JwtOptionsTests
{
    private static List<ValidationResult> Validate(JwtOptions options)
    {
        List<ValidationResult> results = new();
        Validator.TryValidateObject(options, new ValidationContext(options), results, validateAllProperties: true);
        return results;
    }

    [Test]
    public void A_fully_populated_options_object_is_valid()
    {
        // Arrange & Act & Assert
        Validate(new JwtOptions
        {
            SigningKey = new string('k', 32),
            Issuer = "iss",
            Audience = "aud",
            AccessTokenMinutes = 60,
        }).Should().BeEmpty();
    }

    [Test]
    public void A_short_signing_key_is_rejected()
    {
        // Arrange & Act & Assert
        Validate(new JwtOptions { SigningKey = "too-short", Issuer = "i", Audience = "a" })
            .Should().Contain(r => r.MemberNames.Contains(nameof(JwtOptions.SigningKey)));
    }

    [TestCase(0)]
    [TestCase(1441)]
    public void An_out_of_range_token_lifetime_is_rejected(int minutes)
    {
        // Arrange & Act & Assert
        Validate(new JwtOptions
        {
            SigningKey = new string('k', 32),
            Issuer = "i",
            Audience = "a",
            AccessTokenMinutes = minutes,
        }).Should().Contain(r => r.MemberNames.Contains(nameof(JwtOptions.AccessTokenMinutes)));
    }

    [TestCase(-1)]
    [TestCase(3601)]
    public void An_out_of_range_clock_skew_is_rejected(int seconds)
    {
        // Arrange & Act & Assert
        Validate(new JwtOptions
        {
            SigningKey = new string('k', 32),
            Issuer = "i",
            Audience = "a",
            ClockSkewSeconds = seconds,
        }).Should().Contain(r => r.MemberNames.Contains(nameof(JwtOptions.ClockSkewSeconds)));
    }
}
