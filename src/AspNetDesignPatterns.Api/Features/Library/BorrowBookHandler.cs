using KAM.Common.DependencyInjection;

namespace AspNetDesignPatterns.Api.Features.Library;

/// <summary>
/// Borrows one copy of a book. The rules and the transaction live in
/// <see cref="ILibraryService.BorrowBookAsync"/>; the handler maps the loan to the API contract.
/// </summary>
internal sealed class BorrowBookHandler : IRequestHandler<BorrowBookRequest, LoanResponse>
{
    private readonly ILibraryService _library;

    public BorrowBookHandler(ILibraryService library) => _library = library;

    public Task<Result<LoanResponse>> HandleAsync(BorrowBookRequest request, CancellationToken cancellationToken) =>
        _library.BorrowBookAsync(request.BookId, request.Member, cancellationToken)
            .MapAsync(loan => LoanResponse.From(loan));
}
