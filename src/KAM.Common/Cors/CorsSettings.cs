using System.ComponentModel.DataAnnotations;

using KAM.Common.DependencyInjection;

namespace KAM.Common.Cors;

/// <summary>
/// Cross-Origin Resource Sharing (CORS) settings, bound from the "Cors" configuration section.
/// See <see cref="CorsExtensions"/> for how these become a registered policy, and this
/// folder's README for the "*" special-casing and the wildcard-origin + credentials pitfall
/// <see cref="CorsSettingsValidator"/> guards against.
/// </summary>
/// <remarks>
/// This class <em>has</em> a FluentValidation validator (<see cref="CorsSettingsValidator"/>),
/// so <see cref="SettingsBase{T}"/> validates it with FluentValidation, not DataAnnotations —
/// the primary path. <see cref="AllowedOrigins"/> still carries <c>[Required]</c> so an empty
/// list is caught even if the validator is ever removed.
/// </remarks>
public sealed class CorsSettings : SettingsBase<CorsSettings>, ISettingsSection
{
    public static string Section => "Cors";

    /// <summary>Origins allowed to make cross-origin requests. <c>"*"</c> means any origin.</summary>
    [Required]
    public List<string> AllowedOrigins { get; init; } = [];

    // AllowedHeaders/AllowedMethods default to EMPTY, with the real defaults applied by
    // CorsExtensions when nothing is configured. The configuration binder *appends* to a
    // collection that already has items, so a non-empty initializer (e.g. ["*"]) could never
    // be narrowed from appsettings.json -- ["GET"] would bind as the defaults plus "GET".

    /// <summary>Request headers allowed. Empty (the default) or <c>["*"]</c> means any header.</summary>
    public List<string> AllowedHeaders { get; init; } = [];

    /// <summary>
    /// HTTP methods allowed. Empty (the default) means <see cref="DefaultAllowedMethods"/>;
    /// <c>["*"]</c> means any method.
    /// </summary>
    public List<string> AllowedMethods { get; init; } = [];

    /// <summary>The methods allowed when <see cref="AllowedMethods"/> is not configured.</summary>
    public static IReadOnlyList<string> DefaultAllowedMethods { get; } = ["GET", "POST", "PUT", "DELETE", "OPTIONS", "PATCH"];

    /// <summary>
    /// Whether browsers may send cookies, HTTP auth or client certificates on cross-origin
    /// requests. <see langword="false"/> by default: a bearer token is set explicitly by the
    /// caller's code and doesn't need it, and turning it on lets every allowed origin make
    /// cookie-carrying requests on a signed-in user's behalf. Enable it deliberately, alongside
    /// cookie authentication. Incompatible with a <c>"*"</c> origin — see <see cref="CorsSettingsValidator"/>.
    /// </summary>
    public bool AllowCredentials { get; init; }

    /// <summary>How long, in seconds, a browser may cache a preflight response.</summary>
    [Range(0, int.MaxValue)]
    public int MaxAgeSeconds { get; init; } = 3600;
}
