using System.Security.Claims;
using System.Text;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace KAM.Common.Auth;

/// <summary>
/// Registers JWT bearer authentication. The bearer options are configured from
/// <see cref="JwtOptions"/> lazily, so options validation (<c>ValidateOnStart</c>) still
/// governs whether the app is allowed to start. This is authentication only — "who is the
/// caller" — never authorization ("what can they do"): see <see cref="KAM.Common.Authorization.AuthorizationExtensions"/>
/// for the named policies and the deny-by-default fallback.
/// </summary>
public static class AuthExtensions
{
    public static IServiceCollection AddJwtAuth(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<DevTokenIssuer>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        services
            .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>, ILogger<JwtBearerOptions>>((bearer, jwtOptionsAccessor, logger) =>
            {
                JwtOptions jwt = jwtOptionsAccessor.Value;

                bearer.MapInboundClaims = false;
                bearer.SaveToken = jwt.SaveToken;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = jwt.ValidateIssuer,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = jwt.ValidateAudience,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                    ValidateLifetime = jwt.ValidateLifetime,
                    ClockSkew = TimeSpan.FromSeconds(jwt.ClockSkewSeconds),
                    NameClaimType = jwt.NameClaimType ?? ClaimTypes.Name,
                    RoleClaimType = jwt.RoleClaimType ?? ClaimTypes.Role,
                };

                // Observability: these fire on every request, so they log at Debug/Warning, not
                // Information — a busy endpoint would otherwise flood the log at the default level.
                bearer.Events = new JwtBearerEvents
                {
                    OnAuthenticationFailed = context =>
                    {
                        logger.LogWarning("JWT authentication failed: {Exception}", context.Exception.Message);
                        return Task.CompletedTask;
                    },
                    OnTokenValidated = context =>
                    {
                        logger.LogDebug("JWT token validated successfully for user: {User}", context.Principal?.Identity?.Name ?? "Unknown");
                        return Task.CompletedTask;
                    },
                    OnChallenge = context =>
                    {
                        logger.LogDebug("JWT authentication challenge triggered: {Error}", context.Error ?? "Unknown");
                        return Task.CompletedTask;
                    },
                };
            });

        return services;
    }
}
