using System.Security.Claims;

using KAM.Common.Authorization;
using KAM.Common.Authorization.Requirements;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace KAM.Common.Tests.Authorization;

[TestFixture]
public class AuthorizationExtensionsTests
{
    private static ServiceProvider BuildProvider(AuthorizationSettings? settings = null)
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton(Microsoft.Extensions.Options.Options.Create(settings ?? new AuthorizationSettings()));
        services.AddPolicyDrivenAuthorization();
        return services.BuildServiceProvider();
    }

    private static Task<bool> Satisfies(AssertionRequirement assertion, string scopeClaimValue)
    {
        ClaimsPrincipal user = new(new ClaimsIdentity([new Claim("scope", scopeClaimValue)], "test"));
        AuthorizationHandlerContext context = new([assertion], user, resource: null);
        return assertion.Handler(context);
    }

    [Test]
    public async Task Default_policy_denies_anonymous_users_by_default()
    {
        // Arrange
        using ServiceProvider provider = BuildProvider();
        IAuthorizationPolicyProvider policyProvider = provider.GetRequiredService<IAuthorizationPolicyProvider>();

        // Act
        AuthorizationPolicy policy = await policyProvider.GetDefaultPolicyAsync();

        // Assert
        policy.Requirements.Should().Contain(r => r is DenyAnonymousAuthorizationRequirement);
    }

    [Test]
    public async Task A_named_policy_accepts_either_scope_claim_shape()
    {
        // Arrange — a RequireClaim("scope", "weather:read") policy only matches a claim whose
        // value is exactly "weather:read"; a real IdP sends one space-delimited scope claim,
        // which that check rejects. This proves the config-driven path uses ScopeClaims instead.
        AuthorizationSettings settings = new()
        {
            Policies = new Dictionary<string, PolicySettings>
            {
                ["WeatherRead"] = new() { RequiredScopes = ["weather:read"] },
            },
        };
        using ServiceProvider provider = BuildProvider(settings);
        IAuthorizationPolicyProvider policyProvider = provider.GetRequiredService<IAuthorizationPolicyProvider>();
        AuthorizationPolicy? policy = await policyProvider.GetPolicyAsync("WeatherRead");
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

    [Test]
    public async Task A_named_policy_can_require_roles_and_claims()
    {
        // Arrange
        AuthorizationSettings settings = new()
        {
            Policies = new Dictionary<string, PolicySettings>
            {
                ["AdminOnly"] = new()
                {
                    RequiredRoles = ["Administrator"],
                    RequiredClaims = new Dictionary<string, string> { ["department"] = "IT" },
                },
            },
        };
        using ServiceProvider provider = BuildProvider(settings);
        IAuthorizationPolicyProvider policyProvider = provider.GetRequiredService<IAuthorizationPolicyProvider>();

        // Act
        AuthorizationPolicy? policy = await policyProvider.GetPolicyAsync("AdminOnly");

        // Assert
        using (new AssertionScope())
        {
            policy!.Requirements.OfType<RolesAuthorizationRequirement>()
                .Should().Contain(role => role.AllowedRoles.Contains("Administrator"));
            policy.Requirements.OfType<ClaimsAuthorizationRequirement>()
                .Should().Contain(claim => claim.ClaimType == "department");
        }
    }

    [Test]
    public async Task A_named_policy_adds_ValidClientIdRequirement_when_client_ids_are_configured()
    {
        // Arrange
        AuthorizationSettings settings = new()
        {
            Policies = new Dictionary<string, PolicySettings>
            {
                ["ValidClientId"] = new() { CustomRequirements = [nameof(ValidClientIdRequirement)] },
            },
            AllowedClientIds = ["web-client-01"],
        };
        using ServiceProvider provider = BuildProvider(settings);
        IAuthorizationPolicyProvider policyProvider = provider.GetRequiredService<IAuthorizationPolicyProvider>();

        // Act
        AuthorizationPolicy? policy = await policyProvider.GetPolicyAsync("ValidClientId");

        // Assert
        policy!.Requirements.Should().ContainSingle(r => r is ValidClientIdRequirement);
    }

    [Test]
    public async Task A_ValidClientIdRequirement_with_no_allowed_ids_configured_is_skipped_not_thrown()
    {
        // Arrange — matches this system's "available but unused" convention: a policy that
        // names a custom requirement it can't yet satisfy is a config warning, not a crash.
        AuthorizationSettings settings = new()
        {
            Policies = new Dictionary<string, PolicySettings>
            {
                ["ValidClientId"] = new() { CustomRequirements = [nameof(ValidClientIdRequirement)] },
            },
            AllowedClientIds = [],
        };
        using ServiceProvider provider = BuildProvider(settings);
        IAuthorizationPolicyProvider policyProvider = provider.GetRequiredService<IAuthorizationPolicyProvider>();

        // Act
        AuthorizationPolicy? policy = await policyProvider.GetPolicyAsync("ValidClientId");

        // Assert
        policy!.Requirements.Should().NotContain(r => r is ValidClientIdRequirement);
    }

    [Test]
    public async Task An_unknown_custom_requirement_is_skipped_without_throwing()
    {
        // Arrange
        AuthorizationSettings settings = new()
        {
            Policies = new Dictionary<string, PolicySettings>
            {
                ["Mystery"] = new() { CustomRequirements = ["SomethingThatDoesNotExist"] },
            },
        };
        using ServiceProvider provider = BuildProvider(settings);
        IAuthorizationPolicyProvider policyProvider = provider.GetRequiredService<IAuthorizationPolicyProvider>();

        // Act & Assert
        Func<Task> act = async () => await policyProvider.GetPolicyAsync("Mystery");
        await act.Should().NotThrowAsync();
    }

    [Test]
    public async Task Fallback_policy_is_set_from_the_default_policy_when_enabled()
    {
        // Arrange
        using ServiceProvider provider = BuildProvider(new AuthorizationSettings { EnableFallbackPolicy = true });
        IAuthorizationPolicyProvider policyProvider = provider.GetRequiredService<IAuthorizationPolicyProvider>();

        // Act
        AuthorizationPolicy? fallback = await policyProvider.GetFallbackPolicyAsync();

        // Assert
        using (new AssertionScope())
        {
            fallback.Should().NotBeNull();
            fallback!.Requirements.Should().Contain(r => r is DenyAnonymousAuthorizationRequirement);
        }
    }

    [Test]
    public async Task Fallback_policy_is_absent_when_disabled()
    {
        // Arrange
        using ServiceProvider provider = BuildProvider(new AuthorizationSettings { EnableFallbackPolicy = false });
        IAuthorizationPolicyProvider policyProvider = provider.GetRequiredService<IAuthorizationPolicyProvider>();

        // Act
        AuthorizationPolicy? fallback = await policyProvider.GetFallbackPolicyAsync();

        // Assert
        fallback.Should().BeNull();
    }

    [Test]
    public void Registers_ValidClientIdHandler_as_an_authorization_handler()
    {
        // Arrange & Act
        using ServiceProvider provider = BuildProvider();

        // Assert
        provider.GetServices<IAuthorizationHandler>().Should().Contain(h => h is ValidClientIdHandler);
    }
}
