using KAM.Common.Handlers;

namespace AspNetDesignPatterns.Api.Features.Library;

/// <summary>One book by id, or <see cref="LibraryErrors.BookNotFound"/>.</summary>
internal sealed class GetBookHandler : IRequestHandler<GetBookRequest, BookResponse>
{
    private readonly ILibraryService _library;

    public GetBookHandler(ILibraryService library) => _library = library;

    public Task<Result<BookResponse>> HandleAsync(GetBookRequest request, CancellationToken cancellationToken) =>
        _library.GetBookAsync(request.BookId, cancellationToken)
            .MapAsync(book => BookResponse.From(book));
}
