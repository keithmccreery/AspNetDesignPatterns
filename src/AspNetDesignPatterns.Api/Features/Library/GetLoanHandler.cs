using KAM.Common.Handlers;

namespace AspNetDesignPatterns.Api.Features.Library;

/// <summary>One of the caller's loans, or <see cref="LibraryErrors.LoanNotFound"/>.</summary>
internal sealed class GetLoanHandler : IRequestHandler<GetLoanRequest, LoanResponse>
{
    private readonly ILibraryService _library;

    public GetLoanHandler(ILibraryService library) => _library = library;

    public Task<Result<LoanResponse>> HandleAsync(GetLoanRequest request, CancellationToken cancellationToken) =>
        _library.GetLoanAsync(request.LoanId, request.Member, cancellationToken)
            .MapAsync(loan => LoanResponse.From(loan));
}
