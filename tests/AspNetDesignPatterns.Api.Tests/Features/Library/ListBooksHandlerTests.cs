using AspNetDesignPatterns.Api.Features.Library;
using AspNetDesignPatterns.Api.Tests.TestSupport;

using KAM.Common.Pagination;
using KAM.Common.Results;

namespace AspNetDesignPatterns.Api.Tests.Features.Library;

[TestFixture]
public class ListBooksHandlerTests
{
    private LibraryTestDatabase _database = null!;

    [SetUp]
    public async Task SetUp() => _database = await LibraryTestDatabase.CreateAsync(new LibraryOptions { DefaultPageSize = 2 });

    [TearDown]
    public void TearDown() => _database.Dispose();

    private Task<Result<CursorPage<BookResponse>>> ListAsync(string? search = null, int? limit = null, string? cursor = null) =>
        _database.SendAsync<ListBooksRequest, CursorPage<BookResponse>>(new ListBooksRequest(search, limit, cursor));

    private async Task<List<string>> ReadAllPagesAsync(int limit, string? search = null)
    {
        List<string> titles = [];
        string? cursor = null;
        do
        {
            CursorPage<BookResponse> page = (await ListAsync(search, limit, cursor)).Value;
            titles.AddRange(page.Items.Select(b => $"{b.Title}|{b.Author}"));
            cursor = page.NextCursor;
        }
        while (cursor is not null);

        return titles;
    }

    [Test]
    public async Task Returns_the_first_page_in_title_order_with_a_cursor_for_the_next()
    {
        // Arrange
        _database.AddBook("Cranford");
        _database.AddBook("Adam Bede");
        _database.AddBook("Bleak House");

        // Act — no limit: the configured default of 2
        CursorPage<BookResponse> page = (await ListAsync()).Value;

        // Assert
        page.Items.Select(b => b.Title).Should().Equal("Adam Bede", "Bleak House");
        page.NextCursor.Should().NotBeNull();
    }

    [Test]
    public async Task Following_the_cursors_visits_every_book_once_and_ends_with_no_cursor()
    {
        // Arrange — equal titles on either side of a page boundary are what (title, id) is for
        _database.AddBook("Poems", "Emily Dickinson");
        _database.AddBook("Emma");
        _database.AddBook("Poems", "Wilfred Owen");
        _database.AddBook("Poems", "John Keats");
        _database.AddBook("Ulysses");

        // Act
        List<string> titles = await ReadAllPagesAsync(limit: 2);

        // Assert
        titles.Should().Equal(
            "Emma|An Author",
            "Poems|Emily Dickinson",
            "Poems|Wilfred Owen",
            "Poems|John Keats",
            "Ulysses|An Author");
    }

    [Test]
    public async Task A_book_added_behind_the_cursor_does_not_shift_the_next_page()
    {
        // Arrange
        _database.AddBook("B");
        _database.AddBook("C");
        _database.AddBook("D");
        CursorPage<BookResponse> first = (await ListAsync(limit: 2)).Value;

        // Act — with offset paging this insert would push "C" onto page two again
        _database.AddBook("A");
        CursorPage<BookResponse> second = (await ListAsync(limit: 2, cursor: first.NextCursor)).Value;

        // Assert
        second.Items.Select(b => b.Title).Should().Equal("D");
    }

    [Test]
    public async Task Search_matches_title_or_author_case_insensitively()
    {
        // Arrange
        _database.AddBook("Hard Times", "Charles Dickens");
        _database.AddBook("Dickensian Days", "Someone Else");
        _database.AddBook("Emma", "Jane Austen");

        // Act
        List<string> titles = await ReadAllPagesAsync(limit: 10, search: "dickens");

        // Assert
        titles.Should().Equal("Dickensian Days|Someone Else", "Hard Times|Charles Dickens");
    }

    [Test]
    public async Task Search_treats_like_wildcards_as_literal_text()
    {
        // Arrange
        _database.AddBook("100% Pure");
        _database.AddBook("1000 Years");

        // Act
        List<string> titles = await ReadAllPagesAsync(limit: 10, search: "100%");

        // Assert
        titles.Should().Equal("100% Pure|An Author");
    }

    [Test]
    public async Task A_database_failure_becomes_DatabaseUnavailable()
    {
        // Arrange
        using LibraryTestDatabase down = await LibraryTestDatabase.CreateAsync(connectionFactory: new UnavailableDatabase());

        // Act
        Result<CursorPage<BookResponse>> result =
            await down.SendAsync<ListBooksRequest, CursorPage<BookResponse>>(new ListBooksRequest(null, null, null));

        // Assert
        result.Error.Code.Should().Be(LibraryErrors.DatabaseUnavailable.Code);
        result.Error.Exception.Should().NotBeNull();
    }
}
