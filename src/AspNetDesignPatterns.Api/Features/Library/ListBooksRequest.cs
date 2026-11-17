using FluentValidation;

using KAM.Common.Pagination;

using Microsoft.Extensions.Options;

namespace AspNetDesignPatterns.Api.Features.Library;

/// <summary>Query parameters for <c>GET /library/books</c>, bound via <c>[AsParameters]</c>.</summary>
/// <param name="Search">
/// Matches anywhere in the title or author, ignoring case and accents; at least
/// <see cref="ListBooksRequestValidator.MIN_SEARCH_LENGTH"/> characters.
/// </param>
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
    /// <summary>
    /// The search index is built from 3-character slices of the text (the FTS5 trigram
    /// tokenizer), so a shorter search can't match anything: a 400 says so instead of an empty
    /// page that looks like "no such book".
    /// </summary>
    internal const int MIN_SEARCH_LENGTH = 3;

    public ListBooksRequestValidator(IOptions<LibraryOptions> options)
    {
        Include(new CursorPagedRequestValidator<BookCursor>(options.Value.MaxPageSize));

        RuleFor(x => x.Search)
            .MaximumLength(100)
            .Must(search => search!.Trim().Length >= MIN_SEARCH_LENGTH)
            .When(x => !string.IsNullOrWhiteSpace(x.Search))
            .WithMessage($"Search must be at least {MIN_SEARCH_LENGTH} characters.");
    }
}
