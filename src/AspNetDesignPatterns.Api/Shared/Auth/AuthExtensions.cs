using System.Security.Claims;
using System.Text;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AspNetDesignPatterns.Api.Shared.Auth;

public static class AuthExtensions
{
    /// <summary>
    /// Registers JWT bearer authentication and the named authorization policies. The bearer
    /// options are configured from <see cref="JwtOptions"/> lazily, so options validation
    /// (<c>ValidateOnStart</c>) still governs whether the app is allowed to start.
    /// </summary>
    public static IServiceCollection AddJwtAuth(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<DevTokenIssuer>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        services
            .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwtOptionsAccessor) =>
            {
                JwtOptions jwt = jwtOptionsAccessor.Value;

                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = ClaimTypes.Name,
                    RoleClaimType = ClaimTypes.Role,
                };
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthorizationPolicies.WeatherRead, policy => policy
                .RequireAuthenticatedUser()
                .RequireAssertion(context => ScopeClaims.Has(context.User, Scopes.WeatherRead)));

            // Fail closed: an endpoint that maps without calling .RequireAuthorization(...) or
            // .AllowAnonymous() requires an authenticated user by default. EndpointAuthorizationTests
            // already fails the build if any endpoint declares neither explicitly; this is the
            // runtime backstop for the same rule — if that test is ever skipped, bypassed, or an
            // endpoint is registered through a path it doesn't walk, the failure mode is "401",
            // not "silently public".
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });

        return services;
    }
}
