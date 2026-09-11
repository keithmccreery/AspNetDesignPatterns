using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Security.Claims;

using AspNetDesignPatterns.Api.Shared.Auth;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace AspNetDesignPatterns.Api.Tests.Shared.Auth;

[TestFixture]
public class AuthExtensionsTests
{
    private static ServiceProvider BuildProvider()
    {
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddLogging();
        services.AddSingleton(Microsoft.Extensions.Options.Options.Create(new JwtOptions
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
    public async Task Registers_the_weather_read_policy_denying_anonymous_users()
    {
        // Arrange
        using ServiceProvider provider = BuildProvider();
        IAuthorizationPolicyProvider policyProvider = provider.GetRequiredService<IAuthorizationPolicyProvider>();

        // Act
        AuthorizationPolicy? policy = await policyProvider.GetPolicyAsync(AuthorizationPolicies.WeatherRead);

        // Assert
        policy!.Requirements.Should().Contain(r => r is DenyAnonymousAuthorizationRequirement);
    }

    [Test]
    public async Task Registers_the_weather_read_policy_accepting_either_scope_claim_shape()
    {
        // Arrange — a RequireClaim("scope", "weather:read") policy (what this used to be) only
        // matches a claim whose value is exactly "weather:read"; a real IdP sends one
        // space-delimited scope claim, which that check rejects. This proves the fix.
        using ServiceProvider provider = BuildProvider();
        IAuthorizationPolicyProvider policyProvider = provider.GetRequiredService<IAuthorizationPolicyProvider>();
        AuthorizationPolicy? policy = await policyProvider.GetPolicyAsync(AuthorizationPolicies.WeatherRead);
        AssertionRequirement assertion = policy!.Requirements.OfType<AssertionRequirement>().Single();

        // Act & Assert
        using (new AssertionScope())
        {
            (await Satisfies(assertion, "weather:read")).Should().BeTrue("an exact single scope claim must match");
            (await Satisfies(assertion, "weather:read openid profile")).Should().BeTrue(
                "a standard space-delimited scope claim (what a real IdP sends) must match");
            (await Satisfies(assertion, "openid profile")).Should().BeFalse("the required scope is absent");
        }
    }

    private static Task<bool> Satisfies(AssertionRequirement assertion, string scopeClaimValue)
    {
        ClaimsPrincipal user = new(new ClaimsIdentity([new Claim("scope", scopeClaimValue)], "test"));
        AuthorizationHandlerContext context = new([assertion], user, resource: null);
        return assertion.Handler(context);
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
            bearer.TokenValidationParameters.ValidateLifetime.Should().BeTrue();
        }
    }

    [Test]
    public async Task An_endpoint_that_declares_no_auth_intent_requires_authentication_by_default()
    {
        // Arrange — a full pipeline, not just DI registration: the fallback policy is a
        // middleware-level effect. One endpoint deliberately calls neither
        // .RequireAuthorization(...) nor .AllowAnonymous(), the mistake EndpointAuthorizationTests
        // (Integration/) catches at the type level; this proves the runtime backstop for it.
        using IHost host = await new HostBuilder()
            .ConfigureWebHost(web => web
                .UseTestServer()
                .ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddLogging();
                    services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
                    services.AddSingleton(Microsoft.Extensions.Options.Options.Create(new JwtOptions
                    {
                        SigningKey = "fallback-policy-test-signing-key-0123456789",
                        Issuer = "iss",
                        Audience = "aud",
                    }));
                    services.AddJwtAuth();
                })
                .Configure(app => app
                    .UseRouting()
                    .UseAuthentication()
                    .UseAuthorization()
                    .UseEndpoints(endpoints => endpoints.MapGet("/undeclared", () => "should not be reachable anonymously"))))
            .StartAsync();

        // Act
        HttpResponseMessage response = await host.GetTestClient().GetAsync("/undeclared");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
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
}
