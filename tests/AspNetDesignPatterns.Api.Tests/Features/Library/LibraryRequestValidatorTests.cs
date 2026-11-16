using AspNetDesignPatterns.Api.Features.Library;

using FluentValidation.Results;

using KAM.Common.Pagination;

using Microsoft.Extensions.Options;

namespace AspNetDesignPatterns.Api.Tests.Features.Library;

/// <summary>
/// The paging rules themselves are tested in KAM.Common; this checks the slice wires them to its
/// own configured maximum and cursor type, and adds its own rule.
/// </summary>
[TestFixture]
public class LibraryRequestValidatorTests
{
    private static readonly IOptions<LibraryOptions> Options =
        Microsoft.Extensions.Options.Options.Create(new LibraryOptions { MaxPageSize = 25 });

    [Test]
    public void ListBooks_uses_the_configured_maximum_page_size()
    {
        // Arrange
        ListBooksRequestValidator validator = new(Options);

        // Act
        ValidationResult atMax = validator.Validate(new ListBooksRequest(null, 25, null));
        ValidationResult overMax = validator.Validate(new ListBooksRequest(null, 26, null));

        // Assert
        atMax.IsValid.Should().BeTrue();
        overMax.Errors.Should().ContainSingle().Which.ErrorMessage.Should().Be("Limit must be between 1 and 25.");
    }

    [Test]
    public void ListBooks_rejects_a_loan_cursor_because_its_key_has_a_different_shape()
    {
        // Arrange
        string loanCursor = CursorCodec.Encode(new LoanCursor(5));

        // Act
        ValidationResult result = new ListBooksRequestValidator(Options).Validate(new ListBooksRequest(null, null, loanCursor));

        // Assert
        result.Errors.Should().ContainSingle().Which.PropertyName.Should().Be("Cursor");
    }

    [Test]
    public void ListBooks_limits_the_search_length()
    {
        // Act
        ValidationResult result = new ListBooksRequestValidator(Options).Validate(new ListBooksRequest(new string('x', 101), null, null));

        // Assert
        result.Errors.Should().ContainSingle().Which.PropertyName.Should().Be("Search");
    }

    [Test]
    public void ListLoans_accepts_its_own_cursor()
    {
        // Act
        ValidationResult result = new ListLoansRequestValidator(Options)
            .Validate(new ListLoansRequest(new MemberId("alice"), null, CursorCodec.Encode(new LoanCursor(5))));

        // Assert
        result.IsValid.Should().BeTrue();
    }
}
