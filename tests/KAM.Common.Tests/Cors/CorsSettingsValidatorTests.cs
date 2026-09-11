using FluentValidation.TestHelper;

using KAM.Common.Cors;

namespace KAM.Common.Tests.Cors;

[TestFixture]
public class CorsSettingsValidatorTests
{
    private readonly CorsSettingsValidator _validator = new();

    [Test]
    public void A_fully_populated_settings_object_is_valid()
    {
        // Arrange & Act
        TestValidationResult<CorsSettings> result = _validator.TestValidate(new CorsSettings
        {
            AllowedOrigins = ["https://app.example.com"],
        });

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Test]
    public void Empty_allowed_origins_is_rejected()
    {
        // Arrange & Act
        TestValidationResult<CorsSettings> result = _validator.TestValidate(new CorsSettings { AllowedOrigins = [] });

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.AllowedOrigins);
    }

    [Test]
    public void Wildcard_origin_combined_with_credentials_is_rejected()
    {
        // Arrange & Act — a browser refuses a CORS response combining a wildcard origin with
        // credentials; catching this at startup beats a confusing runtime failure in a browser.
        TestValidationResult<CorsSettings> result = _validator.TestValidate(new CorsSettings
        {
            AllowedOrigins = ["*"],
            AllowCredentials = true,
        });

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.AllowedOrigins);
    }

    [Test]
    public void Wildcard_origin_without_credentials_is_valid()
    {
        // Arrange & Act
        TestValidationResult<CorsSettings> result = _validator.TestValidate(new CorsSettings
        {
            AllowedOrigins = ["*"],
            AllowCredentials = false,
        });

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }
}
