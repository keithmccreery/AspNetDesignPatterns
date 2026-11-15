using System.ComponentModel.DataAnnotations;

using KAM.Common.DependencyInjection;

namespace KAM.Common.Authentication;

/// <summary>
/// JWT bearer settings, bound from the "Jwt" configuration section. The signing key is a
/// secret and is expected to come from the <c>.env</c> file (<c>Jwt__SigningKey</c>), never
/// from a committed <c>appsettings.json</c>.
/// </summary>
/// <remarks>
/// This class has <em>no</em> FluentValidation validator, so <see cref="SettingsBase{T}"/>
/// validates it with the DataAnnotations attributes below — the fallback path.
/// </remarks>
public sealed class JwtOptions : SettingsBase<JwtOptions>, ISettingsSection
{
    public static string Section => "Jwt";

    [Required]
    [MinLength(32, ErrorMessage = "The JWT signing key must be at least 32 characters.")]
    public string SigningKey { get; init; } = string.Empty;

    [Required]
    public string Issuer { get; init; } = string.Empty;

    [Required]
    public string Audience { get; init; } = string.Empty;

    [Range(1, 1440)]
    public int AccessTokenMinutes { get; init; } = 60;

    /// <summary>
    /// Whether to validate the token's <c>iss</c> claim against <see cref="Issuer"/>. Defaults
    /// to <see langword="true"/> for a reason — nothing here warns if a Production
    /// <c>appsettings.json</c> turns this (or <see cref="ValidateAudience"/> /
    /// <see cref="ValidateLifetime"/>) off, so treat flipping one outside Development as a
    /// deliberate, reviewed decision, not a quick unblock.
    /// </summary>
    public bool ValidateIssuer { get; init; } = true;

    /// <summary>Whether to validate the token's <c>aud</c> claim against <see cref="Audience"/>. See <see cref="ValidateIssuer"/>.</summary>
    public bool ValidateAudience { get; init; } = true;

    /// <summary>Whether to validate the token's <c>exp</c>/<c>nbf</c> claims. See <see cref="ValidateIssuer"/>.</summary>
    public bool ValidateLifetime { get; init; } = true;

    /// <summary>Clock skew tolerance, in seconds, applied when validating lifetime claims.</summary>
    [Range(0, 3600)]
    public int ClockSkewSeconds { get; init; } = 30;

    /// <summary>Whether to save the raw token in <c>HttpContext.Items</c> via <c>AuthenticationProperties</c>.</summary>
    public bool SaveToken { get; init; }

    /// <summary>
    /// The OAuth2 scopes the Development-only dev-token endpoint grants, as one space-delimited
    /// <c>scope</c> claim. Empty by default (no <c>scope</c> claim at all): which scopes exist is
    /// the consuming app's business, so set it in that app's <c>appsettings.Development.json</c>.
    /// </summary>
    public List<string> DevTokenScopes { get; init; } = [];

    /// <summary>
    /// The claim type checked by <c>RequireRole(...)</c> when <see cref="RoleClaimType"/> is unset —
    /// the raw JWT claim Entra ID and most identity providers emit.
    /// </summary>
    public const string DefaultRoleClaimType = "roles";

    /// <summary>
    /// Overrides the claim type mapped to <c>ClaimsIdentity.Name</c>; defaults to the raw JWT
    /// <c>name</c> claim when unset (inbound claim mapping is off, so the token's own claim
    /// names are what the identity sees).
    /// </summary>
    public string? NameClaimType { get; init; }

    /// <summary>Overrides the claim type checked by <c>RequireRole(...)</c>; defaults to <see cref="DefaultRoleClaimType"/> when unset.</summary>
    public string? RoleClaimType { get; init; }
}
