using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Options;

namespace KAM.Common.Cors;

/// <summary>Registers a single named CORS policy, configured from <see cref="CorsSettings"/>.</summary>
public static class CorsExtensions
{
    /// <summary>The policy name passed to <c>app.UseCors(...)</c>.</summary>
    public const string PolicyName = "Default";

    /// <summary>
    /// Registers the <see cref="PolicyName"/> CORS policy, configured lazily from
    /// <see cref="CorsSettings"/> so options validation (<c>ValidateOnStart</c>) still governs
    /// whether the app is allowed to start. <c>"*"</c> in <see cref="CorsSettings.AllowedHeaders"/>
    /// or <see cref="CorsSettings.AllowedMethods"/> means "any" (<c>AllowAnyHeader()</c> /
    /// <c>AllowAnyMethod()</c>) — <c>WithHeaders("*")</c>/<c>WithMethods("*")</c> would instead
    /// treat <c>"*"</c> as a literal header/method name, which is not what a config author means.
    /// </summary>
    public static IServiceCollection AddCorsPolicy(this IServiceCollection services)
    {
        services.AddCors();

        services.AddOptions<CorsOptions>()
            .Configure<IOptions<CorsSettings>>((options, settingsAccessor) =>
            {
                CorsSettings settings = settingsAccessor.Value;

                options.AddPolicy(PolicyName, policy =>
                {
                    if (settings.AllowedOrigins.Contains("*"))
                    {
                        policy.AllowAnyOrigin();
                    }
                    else
                    {
                        policy.WithOrigins([.. settings.AllowedOrigins]);
                    }

                    if (settings.AllowedHeaders.Contains("*"))
                    {
                        policy.AllowAnyHeader();
                    }
                    else
                    {
                        policy.WithHeaders([.. settings.AllowedHeaders]);
                    }

                    if (settings.AllowedMethods.Contains("*"))
                    {
                        policy.AllowAnyMethod();
                    }
                    else
                    {
                        policy.WithMethods([.. settings.AllowedMethods]);
                    }

                    policy.SetPreflightMaxAge(TimeSpan.FromSeconds(settings.MaxAgeSeconds));

                    // CorsSettingsValidator rejects AllowCredentials + a "*" origin before this
                    // ever runs, so this cast-iron ordering assumption is safe here.
                    if (settings.AllowCredentials)
                    {
                        policy.AllowCredentials();
                    }
                });
            })
            // No IValidateOptions<CorsOptions> is registered, but ValidateOnStart() still forces
            // the Configure<> callback above to run at startup rather than on first use — the
            // same explicit guarantee every SettingsBase<T> makes, instead of relying on
            // UseDefaultServiceProvider(ValidateOnBuild: true) to force-resolve it as a side effect.
            .ValidateOnStart();

        return services;
    }
}
