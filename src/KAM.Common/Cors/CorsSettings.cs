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
public sealed class CorsSettings : SettingsBase<CorsSettings>
{
    public static string Section => "Cors";

    /// <summary>Origins allowed to make cross-origin requests. <c>"*"</c> means any origin.</summary>
    [Required]
    public List<string> AllowedOrigins { get; init; } = [];

    /// <summary>Request headers allowed. <c>["*"]</c> (the default) means any header.</summary>
    public List<string> AllowedHeaders { get; init; } = ["*"];

    /// <summary>HTTP methods allowed. <c>["*"]</c> means any method.</summary>
    public List<string> AllowedMethods { get; init; } = ["GET", "POST", "PUT", "DELETE", "OPTIONS", "PATCH"];

    /// <summary>Whether to allow cookies/credentials on cross-origin requests. Incompatible with a <c>"*"</c> origin — see <see cref="CorsSettingsValidator"/>.</summary>
    public bool AllowCredentials { get; init; } = true;

    /// <summary>How long, in seconds, a browser may cache a preflight response.</summary>
    [Range(0, int.MaxValue)]
    public int MaxAgeSeconds { get; init; } = 3600;
}
