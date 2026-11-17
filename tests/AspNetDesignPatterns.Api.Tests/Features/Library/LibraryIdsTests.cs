using AspNetDesignPatterns.Api.Features.Library;

namespace AspNetDesignPatterns.Api.Tests.Features.Library;

/// <summary>
/// The edge of the typed ids: <c>TryParse</c> is what Minimal APIs call to bind <c>{bookId}</c>,
/// and <c>ToString</c> is what a log line or an interpolated route prints. Reading and writing
/// them through Dapper is covered by every <see cref="LibraryServiceTests"/> test.
/// </summary>
[TestFixture]
public class LibraryIdsTests
{
    [TestCase("7", 7)]
    [TestCase("-5", -5)]
    public void BookId_and_LoanId_parse_the_route_value(string value, int expected)
    {
        // Act
        bool book = BookId.TryParse(value, out BookId bookId);
        bool loan = LoanId.TryParse(value, out LoanId loanId);

        // Assert
        book.Should().BeTrue();
        loan.Should().BeTrue();
        bookId.Should().Be(new BookId(expected));
        loanId.Should().Be(new LoanId(expected));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("abc")]
    [TestCase("7.5")]
    public void BookId_and_LoanId_reject_a_value_that_is_not_an_integer(string? value)
    {
        // Act & Assert
        BookId.TryParse(value, out _).Should().BeFalse();
        LoanId.TryParse(value, out _).Should().BeFalse();
    }

    [Test]
    public void ToString_is_the_bare_number_not_the_record_shape()
    {
        // Act & Assert
        new BookId(7).ToString().Should().Be("7");
        new LoanId(42).ToString().Should().Be("42");
    }
}
