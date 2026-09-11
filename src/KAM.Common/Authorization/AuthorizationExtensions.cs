using KAM.Common.Authorization.Requirements;

using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace KAM.Common.Authorization;

/// <summary>
/// Registers authorization policies built from <see cref="AuthorizationSettings"/> — the
/// default policy, every named entry in <see cref="AuthorizationSettings.Policies"/>, and
/// (optionally) the deny-by-default fallback policy. Kept separate from
/// <c>AddJwtAuth()</c> (authentication) on purpose: authentication says who the caller is,
/// authorization says what they're allowed to do, and this repo's config-driven policy model
/// is independent of which authentication scheme populates the claims it inspects.
/// </summary>
public static class AuthorizationExtensions
{
    /// <summary>
    /// Registers <see cref="ValidClientIdHandler"/> and configures <see cref="AuthorizationOptions"/>
    /// lazily from <see cref="AuthorizationSettings"/>, so options validation (<c>ValidateOnStart</c>)
    /// still governs whether the app is allowed to start.
    /// </summary>
    public static IServiceCollection AddPolicyDrivenAuthorization(this IServiceCollection services)
    {
        services.AddScoped<IAuthorizationHandler, ValidClientIdHandler>();

        // AddAuthorization() registers IAuthorizationService et al.; the lazy Configure<>()
        // below is what actually builds the policies from AuthorizationSettings.
        services.AddAuthorization();

        services.AddOptions<AuthorizationOptions>()
            .Configure<IOptions<AuthorizationSettings>, ILogger<AuthorizationSettings>>((options, settingsAccessor, logger) =>
            {
                AuthorizationSettings settings = settingsAccessor.Value;

                ConfigureDefaultPolicy(options, settings.DefaultPolicy, logger);
                ConfigureNamedPolicies(options, settings.Policies, settings.AllowedClientIds, logger);

                if (settings.EnableFallbackPolicy)
                {
                    options.FallbackPolicy = options.DefaultPolicy;
                    logger.LogInformation("Fallback policy enabled — an endpoint with no declared auth intent requires the default policy");
                }

                logger.LogInformation("Authorization configured with {PolicyCount} named policies", settings.Policies.Count);
            });

        return services;
    }

    private static void ConfigureDefaultPolicy(AuthorizationOptions options, DefaultPolicySettings defaultPolicy, ILogger logger)
    {
        options.DefaultPolicy = new AuthorizationPolicyBuilder()
            .ApplyPolicySettings(
                "DefaultPolicy",
                defaultPolicy.RequireAuthenticatedUser,
                defaultPolicy.RequiredScopes,
                defaultPolicy.RequiredRoles,
                defaultPolicy.RequiredClaims,
                customRequirements: [],
                allowedClientIds: [],
                logger)
            .Build();
    }

    private static void ConfigureNamedPolicies(
        AuthorizationOptions options,
        Dictionary<string, PolicySettings> policies,
        IReadOnlyList<string> allowedClientIds,
        ILogger logger)
    {
        foreach ((string policyName, PolicySettings policySettings) in policies)
        {
            try
            {
                options.AddPolicy(policyName, policy => policy.ApplyPolicySettings(
                    policyName,
                    policySettings.RequireAuthenticatedUser,
                    policySettings.RequiredScopes,
                    policySettings.RequiredRoles,
                    policySettings.RequiredClaims,
                    policySettings.CustomRequirements,
                    allowedClientIds,
                    logger));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to configure policy '{PolicyName}'", policyName);
                throw;
            }
        }
    }
}
