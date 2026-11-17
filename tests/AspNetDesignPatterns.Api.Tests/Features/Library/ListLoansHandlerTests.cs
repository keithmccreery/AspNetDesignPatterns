using AspNetDesignPatterns.Api.Features.Library;
using AspNetDesignPatterns.Api.Tests.TestSupport;

using KAM.Common.Pagination;

namespace AspNetDesignPatterns.Api.Tests.Features.Library;

/// <summary>Paging the caller's loans end to end: the handler's cursor over the repository's keyset query.</summary>
[TestFixture]
public class ListLoansHandlerTests
{
    private static readonly MemberId Alice = new("alice");
    private static readonly MemberId Bob = new("bob");

    private LibraryTestDatabase _database = null!;

    [SetUp]
    public async Task SetUp() => _database = await LibraryTestDatabase.CreateAsync();

    [TearDown]
    public void TearDown() => _database.Dispose();

    // The response's id is the plain number clients see, so the test compares numbers.
    private async Task<int> BorrowAsync(BookId bookId, MemberId member) =>
        (await _database.CallAsync(library => library.BorrowBookAsync(bookId, member, CancellationToken.None))).Value.Id.Value;

    private async Task<CursorPage<LoanResponse>> ListAsync(MemberId member, string? cursor) =>
        (await _database.SendAsync<ListLoansRequest, CursorPage<LoanResponse>>(new ListLoansRequest(member, Limit: 2, cursor))).Value;

    [Test]
    public async Task Pages_through_the_callers_loans_newest_first()
    {
        // Arrange
        int first = await BorrowAsync(_database.AddBook("A"), Alice);
        await BorrowAsync(_database.AddBook("B"), Bob);
        int second = await BorrowAsync(_database.AddBook("C"), Alice);
        int third = await BorrowAsync(_database.AddBook("D"), Alice);

        // Act
        CursorPage<LoanResponse> page1 = await ListAsync(Alice, cursor: null);
        CursorPage<LoanResponse> page2 = await ListAsync(Alice, page1.NextCursor);

        // Assert
        page1.Items.Select(l => l.Id).Should().Equal(third, second);
        page2.Items.Select(l => l.Id).Should().Equal(first);
        page2.NextCursor.Should().BeNull();
    }
}
