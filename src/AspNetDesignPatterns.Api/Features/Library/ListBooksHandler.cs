using System.Data.Common;

using KAM.Common.Handlers;
using KAM.Common.Pagination;

using Microsoft.Extensions.Options;

namespace AspNetDesignPatterns.Api.Features.Library;

/// <summary>
/// One page of books. The handler owns the use case directly — there's no service layer in this
/// slice (see the README): it calls the repository and turns a <see cref="DbException"/> into
/// <see cref="LibraryErrors.DatabaseUnavailable"/>.
/// </summary>
internal sealed class ListBooksHandler : IRequestHandler<ListBooksRequest, CursorPage<BookResponse>>
{
    private readonly LibraryRepository _repository;
    private readonly LibraryOptions _options;

    public ListBooksHandler(LibraryRepository repository, IOptions<LibraryOptions> options)
    {
        _repository = repository;
        _options = options.Value;
    }

    public async Task<Result<CursorPage<BookResponse>>> HandleAsync(
        ListBooksRequest request,
        CancellationToken cancellationToken)
    {
        int limit = request.EffectiveLimit(_options.DefaultPageSize);

        // Already validated by ListBooksRequestValidator; no cursor means the first page.
        CursorCodec.TryDecode(request.Cursor, out BookCursor? after);

        try
        {
            IReadOnlyList<BookRow> rows = await _repository.ListBooksAsync(request.Search, after, limit + 1, cancellationToken);

            return CursorPage
                .From(rows, limit, row => new BookCursor(row.Title, row.Id))
                .Map(BookResponse.From);
        }
        catch (DbException ex)
        {
            return LibraryErrors.DatabaseUnavailable
                .WithException(ex)
                .WithContext(new { request.Search, request.Limit });
        }
    }
}
