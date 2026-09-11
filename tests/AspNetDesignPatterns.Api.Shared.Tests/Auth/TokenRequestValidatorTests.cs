using AspNetDesignPatterns.Api.Shared.Auth;

using FluentValidation.TestHelper;

namespace AspNetDesignPatterns.Api.Shared.Tests.Auth;

[TestFixture]
public class TokenRequestValidatorTests
{
    private readonly TokenRequestValidator _validator = new();

    [Test]
    public void Accepts_a_non_empty_subject()
    {
        // Arrange & Act
        TestValidationResult<TokenRequest> result = _validator.TestValidate(new TokenRequest("alice"));

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [TestCase("")]
    [TestCase(null)]
    public void Rejects_a_missing_subject(string? subject)
    {
        // Arrange & Act — an empty body deserializes Subject as null; this is what used to
        // reach DevTokenIssuer.Issue and throw ArgumentNullException building the sub claim.
        TestValidationResult<TokenRequest> result = _validator.TestValidate(new TokenRequest(subject!));

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Subject);
    }
}
