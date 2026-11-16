using KAM.Common.DependencyInjection;
using KAM.Common.Pagination;

using Microsoft.Extensions.Options;

namespace AspNetDesignPatterns.Api.Features.Library;

/// <summary>
/// One page of books. The handler owns the paging contract (the cursor, the page size, the extra
/// row that says whether there's a next page); <see cref="ILibraryService"/> owns the data.
/// </summary>
internal sealed class ListBooksHandler : IRequestHandler<ListBooksRequest, CursorPage<BookResponse>>
{
    private readonly ILibraryService _library;
    private readonly LibraryOptions _options;

    public ListBooksHandler(ILibraryService library, IOptions<LibraryOptions> options)
    {
        _library = library;
        _options = options.Value;
    }

    public Task<Result<CursorPage<BookResponse>>> HandleAsync(
        ListBooksRequest request,
        CancellationToken cancellationToken)
    {
        int limit = request.EffectiveLimit(_options.DefaultPageSize);

        // Already validated by ListBooksRequestValidator; no cursor means the first page.
        CursorCodec.TryDecode(request.Cursor, out BookCursor? after);

        return _library.ListBooksAsync(request.Search, after, limit + 1, cancellationToken)
            .MapAsync(rows => CursorPage
                .From(rows, limit, row => new BookCursor(row.Title, row.Id))
                .Map(BookResponse.From));
    }
}
