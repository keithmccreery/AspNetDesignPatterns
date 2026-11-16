using AspNetDesignPatterns.Api.Features.Library;
using AspNetDesignPatterns.Api.Tests.TestSupport;

using KAM.Common.Results;

namespace AspNetDesignPatterns.Api.Tests.Features.Library;

/// <summary>
/// The unit-of-work behavior is the point of these tests: after every failure, the copy count
/// and the loans table must look as if the request never happened.
/// </summary>
[TestFixture]
public class BorrowBookHandlerTests
{
    private static readonly MemberId Alice = new("alice");
    private static readonly MemberId Bob = new("bob");

    private LibraryTestDatabase _database = null!;

    [SetUp]
    public async Task SetUp() =>
        _database = await LibraryTestDatabase.CreateAsync(new LibraryOptions { LoanPeriod = TimeSpan.FromDays(7) });

    [TearDown]
    public void TearDown() => _database.Dispose();

    private Task<Result<LoanResponse>> BorrowAsync(int bookId, MemberId member) =>
        _database.SendAsync<BorrowBookRequest, LoanResponse>(new BorrowBookRequest(bookId, member));

    [Test]
    public async Task Borrowing_takes_a_copy_and_records_a_loan_due_after_the_loan_period()
    {
        // Arrange
        int bookId = _database.AddBook("Dracula", copies: 3);

        // Act
        Result<LoanResponse> result = await BorrowAsync(bookId, Alice);

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
    public async Task An_unknown_book_is_BookNotFound()
    {
        // Act
        Result<LoanResponse> result = await BorrowAsync(999, Alice);

        // Assert
        result.Error.Code.Should().Be(LibraryErrors.BookNotFound.Code);
        _database.LoanCount().Should().Be(0);
    }

    [Test]
    public async Task No_copy_left_is_NoCopiesAvailable_and_changes_nothing()
    {
        // Arrange
        int bookId = _database.AddBook("Beowulf", copies: 1);
        await BorrowAsync(bookId, Alice);

        // Act
        Result<LoanResponse> result = await BorrowAsync(bookId, Bob);

        // Assert
        result.Error.Code.Should().Be(LibraryErrors.NoCopiesAvailable.Code);
        _database.AvailableCopies(bookId).Should().Be(0);
        _database.LoanCount().Should().Be(1);
    }

    [Test]
    public async Task Borrowing_a_book_you_already_have_rolls_back_the_copy_it_took()
    {
        // Arrange
        int bookId = _database.AddBook("Dracula", copies: 3);
        await BorrowAsync(bookId, Alice);

        // Act — this request takes a copy (3 → 2 → 1) before it finds Alice's open loan
        Result<LoanResponse> result = await BorrowAsync(bookId, Alice);

        // Assert — the rollback put it back
        result.Error.Code.Should().Be(LibraryErrors.AlreadyBorrowed.Code);
        _database.AvailableCopies(bookId).Should().Be(2);
        _database.LoanCount().Should().Be(1);
    }

    [Test]
    public async Task Holding_the_last_copy_yourself_is_AlreadyBorrowed_not_NoCopiesAvailable()
    {
        // Arrange
        int bookId = _database.AddBook("Beowulf", copies: 1);
        await BorrowAsync(bookId, Alice);

        // Act
        Result<LoanResponse> result = await BorrowAsync(bookId, Alice);

        // Assert
        result.Error.Code.Should().Be(LibraryErrors.AlreadyBorrowed.Code);
    }

    [Test]
    public async Task A_database_failure_becomes_DatabaseUnavailable()
    {
        // Arrange
        using LibraryTestDatabase down = await LibraryTestDatabase.CreateAsync(connectionFactory: new UnavailableDatabase());

        // Act
        Result<LoanResponse> result = await down.SendAsync<BorrowBookRequest, LoanResponse>(new BorrowBookRequest(1, Alice));

        // Assert
        result.Error.Code.Should().Be(LibraryErrors.DatabaseUnavailable.Code);
    }
}
