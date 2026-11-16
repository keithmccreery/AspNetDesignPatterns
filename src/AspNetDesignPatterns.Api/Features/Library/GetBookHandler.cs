using System.Data.Common;

using KAM.Common.Handlers;

namespace AspNetDesignPatterns.Api.Features.Library;

/// <summary>One book by id, or <see cref="LibraryErrors.BookNotFound"/>.</summary>
internal sealed class GetBookHandler : IRequestHandler<GetBookRequest, BookResponse>
{
    private readonly ILibraryRepository _repository;

    public GetBookHandler(ILibraryRepository repository) => _repository = repository;

    public async Task<Result<BookResponse>> HandleAsync(GetBookRequest request, CancellationToken cancellationToken)
    {
        try
        {
            BookRow? book = await _repository.GetBookAsync(request.BookId, cancellationToken);
            return book is null ? LibraryErrors.BookNotFound : BookResponse.From(book);
        }
        catch (DbException ex)
        {
            return LibraryErrors.DatabaseUnavailable.WithException(ex).WithContext(new { request.BookId });
        }
    }
}
