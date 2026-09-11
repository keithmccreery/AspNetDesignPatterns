using KAM.Common.DependencyInjection;

namespace KAM.Common.Authorization;

/// <summary>
/// Data-driven authorization policy configuration, bound from the "Authorization" section.
/// A named policy — the default one, or any entry in <see cref="Policies"/> — is expressed
/// entirely as configuration (required scopes/roles/claims, plus named custom requirements),
/// so adding or tightening a policy is an appsettings change, not a code change and redeploy.
/// See <see cref="AuthorizationExtensions"/> for how this is turned into real policies, and
/// this folder's README for the "why" and a worked example.
/// </summary>
public sealed class AuthorizationSettings : SettingsBase<AuthorizationSettings>
{
    public static string Section => "Authorization";

    /// <summary>The policy applied when an endpoint calls <c>.RequireAuthorization()</c> with no name.</summary>
    public DefaultPolicySettings DefaultPolicy { get; init; } = new();

    /// <summary>Named policies, keyed by the name endpoints pass to <c>.RequireAuthorization("name")</c>.</summary>
    public Dictionary<string, PolicySettings> Policies { get; init; } = [];

    /// <summary>
    /// The allowlist <see cref="Requirements.ValidClientIdRequirement"/> checks a token's
    /// <c>client_id</c> claim against, for any policy whose <see cref="PolicySettings.CustomRequirements"/>
    /// names it.
    /// </summary>
    public List<string> AllowedClientIds { get; init; } = [];

    /// <summary>
    /// Whether an endpoint that calls neither <c>.RequireAuthorization(...)</c> nor
    /// <c>.AllowAnonymous()</c> falls back to <see cref="DefaultPolicy"/> (deny-by-default) —
    /// see <see cref="AuthorizationExtensions"/> for why this is a backstop, not the primary
    /// guardrail.
    /// </summary>
    public bool EnableFallbackPolicy { get; init; } = true;
}

/// <summary>The default authorization policy — see <see cref="AuthorizationSettings.DefaultPolicy"/>.</summary>
public sealed class DefaultPolicySettings
{
    public bool RequireAuthenticatedUser { get; init; } = true;

    public List<string> RequiredScopes { get; init; } = [];

    public List<string> RequiredRoles { get; init; } = [];

    public Dictionary<string, string> RequiredClaims { get; init; } = [];
}

/// <summary>One named policy — see <see cref="AuthorizationSettings.Policies"/>.</summary>
public sealed class PolicySettings
{
    public bool RequireAuthenticatedUser { get; init; } = true;

    /// <summary>
    /// OAuth2/OIDC scopes the caller's token must all carry. Checked via
    /// <see cref="KAM.Common.Auth.ScopeClaims"/>, not <c>RequireClaim("scope", ...)</c> — a real
    /// IdP sends one space-delimited <c>scope</c> claim, which <c>RequireClaim</c> only matches
    /// when the claim's value is <em>exactly</em> the required scope.
    /// </summary>
    public List<string> RequiredScopes { get; init; } = [];

    public List<string> RequiredRoles { get; init; } = [];

    public Dictionary<string, string> RequiredClaims { get; init; } = [];

    /// <summary>
    /// Names of extra <see cref="Microsoft.AspNetCore.Authorization.IAuthorizationRequirement"/>
    /// types to add, resolved by <see cref="AuthorizationPolicyBuilderExtensions"/>. An unknown
    /// name is logged and skipped, not a startup failure — this list is configuration, and
    /// configuration can name a requirement that doesn't (yet) exist in this build.
    /// </summary>
    public List<string> CustomRequirements { get; init; } = [];
}
