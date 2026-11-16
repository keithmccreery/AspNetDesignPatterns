using System.Security.Claims;

using Microsoft.IdentityModel.JsonWebTokens;

namespace AspNetDesignPatterns.Api.Features.Library;

/// <summary>
/// The library member making the request: the access token's <c>sub</c> claim. A type of its
/// own, rather than a <c>string</c>, so a member id can't be mixed up with any other string and
/// so Minimal APIs can bind it straight into a request record via <see cref="BindAsync"/> —
/// handlers receive a member, never a <see cref="ClaimsPrincipal"/>.
/// </summary>
internal sealed record MemberId(string Value)
{
    /// <summary>
    /// Minimal API binding hook. Returns <see langword="null"/> — which the framework turns into a
    /// 400 — only for an authenticated token with no <c>sub</c>, a misconfigured identity provider.
    /// The endpoints' authorization policies reject unauthenticated callers before this runs.
    /// </summary>
    public static ValueTask<MemberId?> BindAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        string? subject = context.User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return ValueTask.FromResult(string.IsNullOrWhiteSpace(subject) ? null : new MemberId(subject));
    }

    public override string ToString() => Value;
}
