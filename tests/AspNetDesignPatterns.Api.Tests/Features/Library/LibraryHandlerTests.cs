using AspNetDesignPatterns.Api.Features.Library;

using KAM.Common.Pagination;
using KAM.Common.Results;

using Microsoft.Extensions.Options;

using NSubstitute;

namespace AspNetDesignPatterns.Api.Tests.Features.Library;

/// <summary>
/// What the handlers own on top of <see cref="ILibraryService"/>: mapping rows to the API contract,
/// passing failures through untouched, and the paging contract. The service is a substitute here;
/// its rules are tested against a real database in <see cref="LibraryServiceTests"/>.
/// </summary>
[TestFixture]
public class LibraryHandlerTests
{
    private static readonly MemberId Alice = new("alice");

    private static readonly IOptions<LibraryOptions> Options =
        Microsoft.Extensions.Options.Options.Create(new LibraryOptions { DefaultPageSize = 2 });

    private readonly ILibraryService _library = Substitute.For<ILibraryService>();

    private static BookRow Book(int id, string title) =>
        new() { Id = new BookId(id), Title = title, Author = "An Author", PublishedYear = 1900, TotalCopies = 1, AvailableCopies = 1 };

    [Test]
    public async Task GetBook_maps_the_row_to_the_response()
    {
        // Arrange
        _library.GetBookAsync(new BookId(7), Arg.Any<CancellationToken>()).Returns(Result.Success(Book(7, "Emma")));

        // Act
        Result<BookResponse> result = await new GetBookHandler(_library).HandleAsync(new GetBookRequest(new BookId(7)), CancellationToken.None);

        // Assert
        result.Value.Should().BeEquivalentTo(new { Id = 7, Title = "Emma", AvailableCopies = 1 });
    }

    [Test]
    public async Task Borrow_passes_the_member_through_and_a_failure_back_unchanged()
    {
        // Arrange
        _library.BorrowBookAsync(new BookId(7), Alice, Arg.Any<CancellationToken>())
            .Returns(Result.Failure<LoanRow>(LibraryErrors.NoCopiesAvailable));

        // Act
        Result<LoanResponse> result = await new BorrowBookHandler(_library)
            .HandleAsync(new BorrowBookRequest(new BookId(7), Alice), CancellationToken.None);

        // Assert
        result.Error.Should().Be(LibraryErrors.NoCopiesAvailable);
    }

    [Test]
    public async Task ListBooks_asks_for_one_extra_row_and_turns_it_into_the_next_cursor()
    {
        // Arrange — default page size 2, so the handler asks for 3; getting 3 means there's more
        _library.ListBooksAsync("e", null, 3, Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<BookRow>>([Book(1, "Emma"), Book(2, "Evelina"), Book(3, "Erewhon")]));

        // Act
        Result<CursorPage<BookResponse>> result = await new ListBooksHandler(_library, Options)
            .HandleAsync(new ListBooksRequest("e", Limit: null, Cursor: null), CancellationToken.None);

        // Assert
        result.Value.Items.Select(b => b.Title).Should().Equal("Emma", "Evelina");
        CursorCodec.TryDecode(result.Value.NextCursor, out BookCursor? next).Should().BeTrue();
        next.Should().Be(new BookCursor("Evelina", 2));
    }

    [Test]
    public async Task ListBooks_hands_the_decoded_cursor_to_the_service()
    {
        // Arrange
        BookCursor after = new("Evelina", 2);
        _library.ListBooksAsync(null, after, 3, Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<BookRow>>([Book(3, "Erewhon")]));

        // Act
        Result<CursorPage<BookResponse>> result = await new ListBooksHandler(_library, Options)
            .HandleAsync(new ListBooksRequest(null, Limit: null, CursorCodec.Encode(after)), CancellationToken.None);

        // Assert — the last page: fewer rows than asked for, so no cursor
        result.Value.Items.Select(b => b.Title).Should().Equal("Erewhon");
        result.Value.NextCursor.Should().BeNull();
    }
}
