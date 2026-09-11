using KAM.Common.Authentication;
using KAM.Common.Authorization.Requirements;

using Microsoft.AspNetCore.Authorization;

namespace KAM.Common.Authorization;

/// <summary>
/// Turns a <see cref="DefaultPolicySettings"/> or <see cref="PolicySettings"/> config object
/// into calls on an <see cref="AuthorizationPolicyBuilder"/>. The one non-obvious piece: scope
/// checking goes through <see cref="ScopeClaims"/> via <c>RequireAssertion</c>, not
/// <c>RequireClaim("scope", value)</c> — <c>RequireClaim</c> only matches a claim whose value is
/// <em>exactly</em> the required scope, and a real identity provider sends one space-delimited
/// <c>scope</c> claim per the OAuth2 spec (e.g. <c>"weather:read openid profile"</c>), which
/// that check rejects outright.
/// </summary>
internal static class AuthorizationPolicyBuilderExtensions
{
    /// <summary>Applies the given policy settings to <paramref name="builder"/>.</summary>
    /// <param name="builder">The policy builder being configured.</param>
    /// <param name="policyName">The policy's name, used only for logging.</param>
    /// <param name="requireAuthenticatedUser">Whether the caller must be authenticated.</param>
    /// <param name="requiredScopes">OAuth2/OIDC scopes the caller's token must all carry.</param>
    /// <param name="requiredRoles">Roles the caller must hold at least one of.</param>
    /// <param name="requiredClaims">Claim type/value pairs the caller must carry.</param>
    /// <param name="customRequirements">Names of extra requirements to add — see <see cref="AuthorizationSettings.AllowedClientIds"/>.</param>
    /// <param name="allowedClientIds">The allowlist a <c>"ValidClientIdRequirement"</c> entry checks against.</param>
    /// <param name="logger">Logger for the debug/warning trail below.</param>
    public static AuthorizationPolicyBuilder ApplyPolicySettings(
        this AuthorizationPolicyBuilder builder,
        string policyName,
        bool requireAuthenticatedUser,
        IReadOnlyList<string> requiredScopes,
        IReadOnlyList<string> requiredRoles,
        IReadOnlyDictionary<string, string> requiredClaims,
        IReadOnlyList<string> customRequirements,
        IReadOnlyList<string> allowedClientIds,
        ILogger logger)
    {
        if (requireAuthenticatedUser)
        {
            builder.RequireAuthenticatedUser();
        }

        if (requiredScopes.Count > 0)
        {
            builder.RequireAssertion(context => requiredScopes.All(scope => ScopeClaims.Has(context.User, scope)));
            logger.LogDebug("Policy '{PolicyName}' requires scopes: {Scopes}", policyName, string.Join(", ", requiredScopes));
        }

        if (requiredRoles.Count > 0)
        {
            builder.RequireRole(requiredRoles);
            logger.LogDebug("Policy '{PolicyName}' requires roles: {Roles}", policyName, string.Join(", ", requiredRoles));
        }

        foreach ((string claimType, string claimValue) in requiredClaims)
        {
            builder.RequireClaim(claimType, claimValue);
            logger.LogDebug("Policy '{PolicyName}' requires claim {ClaimType}={ClaimValue}", policyName, claimType, claimValue);
        }

        foreach (string customRequirement in customRequirements)
        {
            switch (customRequirement)
            {
                case nameof(ValidClientIdRequirement):
                    if (allowedClientIds.Count > 0)
                    {
                        builder.AddRequirements(new ValidClientIdRequirement(allowedClientIds));
                        logger.LogDebug(
                            "Policy '{PolicyName}' requires a valid client ID from: {AllowedClientIds}",
                            policyName, string.Join(", ", allowedClientIds));
                    }
                    else
                    {
                        logger.LogWarning(
                            "'{CustomRequirement}' specified for policy '{PolicyName}' but no allowed client IDs are configured",
                            customRequirement, policyName);
                    }

                    break;

                default:
                    logger.LogWarning("Unknown custom requirement '{CustomRequirement}' for policy '{PolicyName}'", customRequirement, policyName);
                    break;
            }
        }

        return builder;
    }
}
