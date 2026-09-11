using FluentValidation;

namespace KAM.Common.Cors;

/// <summary>
/// Validates <see cref="CorsSettings"/>. The interesting rule is the last one: a browser
/// refuses a CORS response that combines a wildcard origin with credentials (the fetch spec
/// forbids it), so catching that combination at startup beats discovering it as a confusing
/// runtime CORS failure in a browser months later.
/// </summary>
internal sealed class CorsSettingsValidator : AbstractValidator<CorsSettings>
{
    public CorsSettingsValidator()
    {
        RuleFor(x => x.AllowedOrigins).NotEmpty();

        RuleFor(x => x.MaxAgeSeconds).GreaterThanOrEqualTo(0);

        RuleFor(x => x)
            .Must(x => !(x.AllowCredentials && x.AllowedOrigins.Contains("*")))
            .WithMessage("AllowedOrigins cannot contain \"*\" when AllowCredentials is true — browsers reject that combination.")
            .WithName(nameof(CorsSettings.AllowedOrigins));
    }
}
