using FluentValidation;

using KAM.Common.Pagination;

using Microsoft.Extensions.Options;

namespace AspNetDesignPatterns.Api.Features.Library;

/// <summary>Query parameters for <c>GET /library/books</c>, bound via <c>[AsParameters]</c>.</summary>
/// <param name="Search">Matches anywhere in the title or author, case-insensitively.</param>
/// <param name="Limit">Page size; defaults to <see cref="LibraryOptions.DefaultPageSize"/>.</param>
/// <param name="Cursor">The previous page's <c>nextCursor</c>; omit for the first page.</param>
internal sealed record ListBooksRequest(string? Search, int? Limit, string? Cursor) : ICursorPagedRequest;

/// <summary>
/// A position in the book list: the sort key (title, then id) of the last book on a page.
/// Encoded into the opaque <c>nextCursor</c> by <see cref="CursorCodec"/>.
/// </summary>
internal sealed record BookCursor(string Title, int Id);

internal sealed class ListBooksRequestValidator : AbstractValidator<ListBooksRequest>
{
    public ListBooksRequestValidator(IOptions<LibraryOptions> options)
    {
        Include(new CursorPagedRequestValidator<BookCursor>(options.Value.MaxPageSize));

        RuleFor(x => x.Search).MaximumLength(100);
    }
}
