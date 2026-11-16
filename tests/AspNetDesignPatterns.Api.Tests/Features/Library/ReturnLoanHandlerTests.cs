using AspNetDesignPatterns.Api.Features.Library;
using AspNetDesignPatterns.Api.Tests.TestSupport;

using KAM.Common.Results;

namespace AspNetDesignPatterns.Api.Tests.Features.Library;

[TestFixture]
public class ReturnLoanHandlerTests
{
    private static readonly MemberId Alice = new("alice");
    private static readonly MemberId Bob = new("bob");

    private LibraryTestDatabase _database = null!;
    private int _bookId;

    [SetUp]
    public async Task SetUp()
    {
        _database = await LibraryTestDatabase.CreateAsync();
        _bookId = _database.AddBook("Emma", copies: 1);
    }

    [TearDown]
    public void TearDown() => _database.Dispose();

    private async Task<int> BorrowAsync(MemberId member) =>
        (await _database.SendAsync<BorrowBookRequest, LoanResponse>(new BorrowBookRequest(_bookId, member))).Value.Id;

    private Task<Result<LoanResponse>> ReturnAsync(int loanId, MemberId member) =>
        _database.SendAsync<ReturnLoanRequest, LoanResponse>(new ReturnLoanRequest(loanId, member));

    [Test]
    public async Task Returning_closes_the_loan_and_puts_the_copy_back()
    {
        // Arrange
        int loanId = await BorrowAsync(Alice);

        // Act
        Result<LoanResponse> result = await ReturnAsync(loanId, Alice);

        // Assert
        result.Value.ReturnedAt.Should().Be(LibraryTestDatabase.Now);
        _database.AvailableCopies(_bookId).Should().Be(1);
    }

    [Test]
    public async Task Returning_twice_is_LoanAlreadyReturned_and_does_not_add_a_copy()
    {
        // Arrange
        int loanId = await BorrowAsync(Alice);
        await ReturnAsync(loanId, Alice);

        // Act
        Result<LoanResponse> result = await ReturnAsync(loanId, Alice);

        // Assert
        result.Error.Code.Should().Be(LibraryErrors.LoanAlreadyReturned.Code);
        _database.AvailableCopies(_bookId).Should().Be(1);
    }

    [Test]
    public async Task Another_members_loan_is_LoanNotFound_so_its_existence_is_not_revealed()
    {
        // Arrange
        int loanId = await BorrowAsync(Alice);

        // Act
        Result<LoanResponse> result = await ReturnAsync(loanId, Bob);

        // Assert
        result.Error.Code.Should().Be(LibraryErrors.LoanNotFound.Code);
        _database.AvailableCopies(_bookId).Should().Be(0);
    }

    [Test]
    public async Task An_unknown_loan_is_LoanNotFound()
    {
        // Act
        Result<LoanResponse> result = await ReturnAsync(999, Alice);

        // Assert
        result.Error.Code.Should().Be(LibraryErrors.LoanNotFound.Code);
    }
}
