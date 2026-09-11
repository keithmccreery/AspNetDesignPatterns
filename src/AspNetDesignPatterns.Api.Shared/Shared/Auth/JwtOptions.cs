using System.ComponentModel.DataAnnotations;

using AspNetDesignPatterns.Api.DependencyInjection;

namespace AspNetDesignPatterns.Api.Shared.Auth;

/// <summary>
/// JWT bearer settings, bound from the "Jwt" configuration section. The signing key is a
/// secret and is expected to come from the <c>.env</c> file (<c>Jwt__SigningKey</c>), never
/// from a committed <c>appsettings.json</c>.
/// </summary>
/// <remarks>
/// This class has <em>no</em> FluentValidation validator, so <see cref="SettingsBase{T}"/>
/// validates it with the DataAnnotations attributes below — the fallback path.
/// </remarks>
public sealed class JwtOptions : SettingsBase<JwtOptions>
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
}
