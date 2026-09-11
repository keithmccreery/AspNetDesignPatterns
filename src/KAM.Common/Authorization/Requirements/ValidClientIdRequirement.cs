using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;

namespace KAM.Common.Authorization.Requirements;

/// <summary>
/// Authorization requirement checking a token's client-identifying claim against an allowlist —
/// a worked example of a custom <see cref="IAuthorizationRequirement"/>, wired up through the
/// data-driven policy config (<see cref="AuthorizationSettings"/>) rather than hardcoded onto
/// one endpoint. See this folder's README for why this exists and how to add another one.
/// </summary>
public sealed class ValidClientIdRequirement : IAuthorizationRequirement
{
    /// <summary>The allowlist of client IDs a caller's token must match one of.</summary>
    public IReadOnlyList<string> AllowedClientIds { get; }

    /// <summary>The claim type carrying the client ID.</summary>
    public string ClientIdClaimType { get; }

    /// <param name="allowedClientIds">The whitelist to check the claim's value against.</param>
    /// <param name="clientIdClaimType">The claim type carrying the client ID (defaults to <c>"client_id"</c>).</param>
    public ValidClientIdRequirement(IEnumerable<string> allowedClientIds, string clientIdClaimType = "client_id")
    {
        AllowedClientIds = allowedClientIds?.ToList().AsReadOnly()
            ?? throw new ArgumentNullException(nameof(allowedClientIds));
        ClientIdClaimType = clientIdClaimType ?? throw new ArgumentNullException(nameof(clientIdClaimType));
    }
}

/// <summary>Evaluates a <see cref="ValidClientIdRequirement"/> against the current user.</summary>
public sealed class ValidClientIdHandler : AuthorizationHandler<ValidClientIdRequirement>
{
    private readonly ILogger<ValidClientIdHandler> _logger;

    public ValidClientIdHandler(ILogger<ValidClientIdHandler> logger) => _logger = logger;

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ValidClientIdRequirement requirement)
    {
        Claim? clientIdClaim = context.User.FindFirst(requirement.ClientIdClaimType);

        if (clientIdClaim is null || string.IsNullOrWhiteSpace(clientIdClaim.Value))
        {
            _logger.LogWarning("Authorization failed: no {ClaimType} claim found in token", requirement.ClientIdClaimType);
            return Task.CompletedTask;
        }

        string clientId = clientIdClaim.Value;

        if (requirement.AllowedClientIds.Contains(clientId, StringComparer.OrdinalIgnoreCase))
        {
            _logger.LogDebug("Authorization succeeded: client ID '{ClientId}' is allowed", clientId);
            context.Succeed(requirement);
        }
        else
        {
            _logger.LogWarning(
                "Authorization failed: client ID '{ClientId}' is not in the allowed list. Allowed IDs: {AllowedClientIds}",
                clientId, string.Join(", ", requirement.AllowedClientIds));
        }

        return Task.CompletedTask;
    }
}
