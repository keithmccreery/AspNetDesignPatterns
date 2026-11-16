using AspNetDesignPatterns.Api.Features.Library;
using AspNetDesignPatterns.Api.Tests.TestSupport;

using KAM.Common.Pagination;
using KAM.Common.Results;

namespace AspNetDesignPatterns.Api.Tests.Features.Library;

/// <summary>The read-only handlers: <see cref="GetBookHandler"/>, <see cref="GetLoanHandler"/>, <see cref="ListLoansHandler"/>.</summary>
[TestFixture]
public class LibraryQueryHandlerTests
{
    private static readonly MemberId Alice = new("alice");
    private static readonly MemberId Bob = new("bob");

    private LibraryTestDatabase _database = null!;

    [SetUp]
    public async Task SetUp() => _database = await LibraryTestDatabase.CreateAsync();

    [TearDown]
    public void TearDown() => _database.Dispose();

    private async Task<int> BorrowAsync(int bookId, MemberId member) =>
        (await _database.SendAsync<BorrowBookRequest, LoanResponse>(new BorrowBookRequest(bookId, member))).Value.Id;

    [Test]
    public async Task GetBook_returns_the_book_or_BookNotFound()
    {
        // Arrange
        int bookId = _database.AddBook("Middlemarch", "George Eliot", copies: 2);

        // Act
        Result<BookResponse> found = await _database.SendAsync<GetBookRequest, BookResponse>(new GetBookRequest(bookId));
        Result<BookResponse> missing = await _database.SendAsync<GetBookRequest, BookResponse>(new GetBookRequest(999));

        // Assert
        found.Value.Should().BeEquivalentTo(new { Title = "Middlemarch", Author = "George Eliot", AvailableCopies = 2 });
        missing.Error.Code.Should().Be(LibraryErrors.BookNotFound.Code);
    }

    [Test]
    public async Task GetLoan_returns_only_the_callers_own_loan()
    {
        // Arrange
        int loanId = await BorrowAsync(_database.AddBook("Emma"), Alice);

        // Act
        Result<LoanResponse> own = await _database.SendAsync<GetLoanRequest, LoanResponse>(new GetLoanRequest(loanId, Alice));
        Result<LoanResponse> other = await _database.SendAsync<GetLoanRequest, LoanResponse>(new GetLoanRequest(loanId, Bob));

        // Assert
        own.Value.Id.Should().Be(loanId);
        other.Error.Code.Should().Be(LibraryErrors.LoanNotFound.Code);
    }

    [Test]
    public async Task ListLoans_pages_through_the_callers_loans_newest_first()
    {
        // Arrange
        int first = await BorrowAsync(_database.AddBook("A"), Alice);
        await BorrowAsync(_database.AddBook("B"), Bob);
        int second = await BorrowAsync(_database.AddBook("C"), Alice);
        int third = await BorrowAsync(_database.AddBook("D"), Alice);

        // Act
        CursorPage<LoanResponse> page1 = (await _database.SendAsync<ListLoansRequest, CursorPage<LoanResponse>>(
            new ListLoansRequest(Alice, Limit: 2, Cursor: null))).Value;
        CursorPage<LoanResponse> page2 = (await _database.SendAsync<ListLoansRequest, CursorPage<LoanResponse>>(
            new ListLoansRequest(Alice, Limit: 2, Cursor: page1.NextCursor))).Value;

        // Assert
        page1.Items.Select(l => l.Id).Should().Equal(third, second);
        page2.Items.Select(l => l.Id).Should().Equal(first);
        page2.NextCursor.Should().BeNull();
    }
}
