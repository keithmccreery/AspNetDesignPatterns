using FluentValidation;
using FluentValidation.Results;

using KAM.Common.Pagination;

namespace KAM.Common.Tests.Pagination;

/// <summary>
/// Exercised the way a feature uses it: <c>Include</c>d into the feature's own request validator.
/// </summary>
[TestFixture]
public class CursorPagedRequestValidatorTests
{
    private sealed record IdCursor(int Id);

    private sealed record ListThingsRequest(string? Search, int? Limit, string? Cursor) : ICursorPagedRequest;

    private sealed class ListThingsRequestValidator : AbstractValidator<ListThingsRequest>
    {
        public ListThingsRequestValidator() => Include(new CursorPagedRequestValidator<IdCursor>(maxLimit: 50));
    }

    private static ValidationResult Validate(int? limit = null, string? cursor = null) =>
        new ListThingsRequestValidator().Validate(new ListThingsRequest(Search: null, limit, cursor));

    [Test]
    public void No_limit_and_no_cursor_is_a_valid_first_page_request()
    {
        // Act & Assert
        Validate().IsValid.Should().BeTrue();
    }

    [TestCase(1)]
    [TestCase(50)]
    public void A_limit_within_range_is_valid(int limit)
    {
        // Act & Assert
        Validate(limit).IsValid.Should().BeTrue();
    }

    [TestCase(0)]
    [TestCase(51)]
    public void A_limit_outside_range_fails_on_Limit(int limit)
    {
        // Act
        ValidationResult result = Validate(limit);

        // Assert
        result.Errors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be("Limit must be between 1 and 50.");
    }

    [Test]
    public void A_cursor_produced_by_the_codec_is_valid()
    {
        // Act & Assert
        Validate(cursor: CursorCodec.Encode(new IdCursor(7))).IsValid.Should().BeTrue();
    }

    [Test]
    public void A_tampered_cursor_fails_on_Cursor()
    {
        // Act
        ValidationResult result = Validate(cursor: "tampered");

        // Assert
        result.Errors.Should().ContainSingle().Which.PropertyName.Should().Be("Cursor");
    }

    [Test]
    public void A_max_limit_below_one_is_a_programming_error()
    {
        // Arrange
        Func<CursorPagedRequestValidator<IdCursor>> act = () => new CursorPagedRequestValidator<IdCursor>(0);

        // Act & Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
