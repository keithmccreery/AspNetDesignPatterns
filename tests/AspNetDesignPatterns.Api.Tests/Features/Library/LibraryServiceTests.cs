using AspNetDesignPatterns.Api.Features.Library;
using AspNetDesignPatterns.Api.Tests.TestSupport;

using KAM.Common.Results;

namespace AspNetDesignPatterns.Api.Tests.Features.Library;

/// <summary>
/// The real <see cref="LibraryService"/> over the real repository on in-memory SQLite. The
/// unit-of-work behavior is the point: after every failure, the copy count and the loans table
/// must look as if the call never happened.
/// </summary>
[TestFixture]
public class LibraryServiceTests
{
    private static readonly MemberId Alice = new("alice");
    private static readonly MemberId Bob = new("bob");

    private LibraryTestDatabase _database = null!;

    [SetUp]
    public async Task SetUp() =>
        _database = await LibraryTestDatabase.CreateAsync(new LibraryOptions { LoanPeriod = TimeSpan.FromDays(7) });

    [TearDown]
    public void TearDown() => _database.Dispose();

    private Task<Result<LoanRow>> BorrowAsync(BookId bookId, MemberId member) =>
        _database.CallAsync(library => library.BorrowBookAsync(bookId, member, CancellationToken.None));

    private Task<Result<LoanRow>> ReturnAsync(LoanId loanId, MemberId member) =>
        _database.CallAsync(library => library.ReturnLoanAsync(loanId, member, CancellationToken.None));

    // ── Borrow ──

    [Test]
    public async Task Borrowing_takes_a_copy_and_records_a_loan_due_after_the_loan_period()
    {
        // Arrange
        BookId bookId = _database.AddBook("Dracula", copies: 3);

        // Act
        Result<LoanRow> result = await BorrowAsync(bookId, Alice);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(new
        {
            BookId = bookId,
            BookTitle = "Dracula",
            BorrowedAt = LibraryTestDatabase.Now,
            DueAt = LibraryTestDatabase.Now.AddDays(7),
            ReturnedAt = (DateTimeOffset?) null,
        });
        _database.AvailableCopies(bookId).Should().Be(2);
    }

    [Test]
    public async Task Borrowing_an_unknown_book_is_BookNotFound()
    {
        // Act
        Result<LoanRow> result = await BorrowAsync(new BookId(999), Alice);

        // Assert
        result.Error.Code.Should().Be(LibraryErrors.BookNotFound.Code);
        _database.LoanCount().Should().Be(0);
    }

    [Test]
    public async Task No_copy_left_is_NoCopiesAvailable_and_changes_nothing()
    {
        // Arrange
        BookId bookId = _database.AddBook("Beowulf", copies: 1);
        await BorrowAsync(bookId, Alice);

        // Act
        Result<LoanRow> result = await BorrowAsync(bookId, Bob);

        // Assert
        result.Error.Code.Should().Be(LibraryErrors.NoCopiesAvailable.Code);
        _database.AvailableCopies(bookId).Should().Be(0);
        _database.LoanCount().Should().Be(1);
    }

    [Test]
    public async Task Borrowing_a_book_you_already_have_rolls_back_the_copy_it_took()
    {
        // Arrange
        BookId bookId = _database.AddBook("Dracula", copies: 3);
        await BorrowAsync(bookId, Alice);

        // Act — this call takes a copy (3 → 2 → 1) before it finds Alice's open loan
        Result<LoanRow> result = await BorrowAsync(bookId, Alice);

        // Assert — the rollback put it back
        result.Error.Code.Should().Be(LibraryErrors.AlreadyBorrowed.Code);
        _database.AvailableCopies(bookId).Should().Be(2);
        _database.LoanCount().Should().Be(1);
    }

    [Test]
    public async Task Holding_the_last_copy_yourself_is_AlreadyBorrowed_not_NoCopiesAvailable()
    {
        // Arrange
        BookId bookId = _database.AddBook("Beowulf", copies: 1);
        await BorrowAsync(bookId, Alice);

        // Act
        Result<LoanRow> result = await BorrowAsync(bookId, Alice);

        // Assert
        result.Error.Code.Should().Be(LibraryErrors.AlreadyBorrowed.Code);
    }

    // ── Return ──

    [Test]
    public async Task Returning_closes_the_loan_and_puts_the_copy_back()
    {
        // Arrange
        BookId bookId = _database.AddBook("Emma", copies: 1);
        LoanId loanId = (await BorrowAsync(bookId, Alice)).Value.Id;

        // Act
        Result<LoanRow> result = await ReturnAsync(loanId, Alice);

        // Assert
        result.Value.ReturnedAt.Should().Be(LibraryTestDatabase.Now);
        _database.AvailableCopies(bookId).Should().Be(1);
    }

    [Test]
    public async Task Returning_twice_is_LoanAlreadyReturned_and_does_not_add_a_copy()
    {
        // Arrange
        BookId bookId = _database.AddBook("Emma", copies: 1);
        LoanId loanId = (await BorrowAsync(bookId, Alice)).Value.Id;
        await ReturnAsync(loanId, Alice);

        // Act
        Result<LoanRow> result = await ReturnAsync(loanId, Alice);

        // Assert
        result.Error.Code.Should().Be(LibraryErrors.LoanAlreadyReturned.Code);
        _database.AvailableCopies(bookId).Should().Be(1);
    }

    [Test]
    public async Task Returning_another_members_loan_is_LoanNotFound_so_its_existence_is_not_revealed()
    {
        // Arrange
        BookId bookId = _database.AddBook("Emma", copies: 1);
        LoanId loanId = (await BorrowAsync(bookId, Alice)).Value.Id;

        // Act
        Result<LoanRow> result = await ReturnAsync(loanId, Bob);

        // Assert
        result.Error.Code.Should().Be(LibraryErrors.LoanNotFound.Code);
        _database.AvailableCopies(bookId).Should().Be(0);
    }

    [Test]
    public async Task Returning_an_unknown_loan_is_LoanNotFound()
    {
        // Act
        Result<LoanRow> result = await ReturnAsync(new LoanId(999), Alice);

        // Assert
        result.Error.Code.Should().Be(LibraryErrors.LoanNotFound.Code);
    }

    // ── Reads ──

    [Test]
    public async Task GetBook_returns_the_book_or_BookNotFound()
    {
        // Arrange
        BookId bookId = _database.AddBook("Middlemarch", "George Eliot", copies: 2);

        // Act
        Result<BookRow> found = await _database.CallAsync(library => library.GetBookAsync(bookId, CancellationToken.None));
        Result<BookRow> missing = await _database.CallAsync(library => library.GetBookAsync(new BookId(999), CancellationToken.None));

        // Assert
        found.Value.Should().BeEquivalentTo(new { Title = "Middlemarch", Author = "George Eliot", AvailableCopies = 2 });
        missing.Error.Code.Should().Be(LibraryErrors.BookNotFound.Code);
    }

    [Test]
    public async Task GetLoan_returns_only_the_members_own_loan()
    {
        // Arrange
        LoanId loanId = (await BorrowAsync(_database.AddBook("Emma"), Alice)).Value.Id;

        // Act
        Result<LoanRow> own = await _database.CallAsync(library => library.GetLoanAsync(loanId, Alice, CancellationToken.None));
        Result<LoanRow> other = await _database.CallAsync(library => library.GetLoanAsync(loanId, Bob, CancellationToken.None));

        // Assert
        own.Value.Id.Should().Be(loanId);
        other.Error.Code.Should().Be(LibraryErrors.LoanNotFound.Code);
    }

    // ── Database failure ──

    [Test]
    public async Task A_database_failure_on_a_read_becomes_DatabaseUnavailable_with_the_exception_attached()
    {
        // Arrange
        using LibraryTestDatabase down = await LibraryTestDatabase.CreateAsync(connectionFactory: new UnavailableDatabase());

        // Act
        Result<IReadOnlyList<BookRow>> result =
            await down.CallAsync(library => library.ListBooksAsync(null, null, 10, CancellationToken.None));

        // Assert
        result.Error.Code.Should().Be(LibraryErrors.DatabaseUnavailable.Code);
        result.Error.Exception.Should().NotBeNull();
    }

    [Test]
    public async Task A_database_failure_opening_the_transaction_becomes_DatabaseUnavailable()
    {
        // Arrange — the unit of work, not the repository, is what throws here
        using LibraryTestDatabase down = await LibraryTestDatabase.CreateAsync(connectionFactory: new UnavailableDatabase());

        // Act
        Result<LoanRow> result = await down.CallAsync(library => library.BorrowBookAsync(new BookId(1), Alice, CancellationToken.None));

        // Assert
        result.Error.Code.Should().Be(LibraryErrors.DatabaseUnavailable.Code);
    }
}
