using System.Security.Claims;
using System.Text;

using Microsoft.AspNetCore.Authentication.JwtBearer;
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

            options.FallbackPolicy = null;
        });

        return services;
    }
}
