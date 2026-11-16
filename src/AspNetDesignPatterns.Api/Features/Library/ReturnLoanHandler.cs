using KAM.Common.DependencyInjection;

namespace AspNetDesignPatterns.Api.Features.Library;

/// <summary>
/// Returns a loan. The rules and the transaction live in
/// <see cref="ILibraryService.ReturnLoanAsync"/>; the handler maps the loan to the API contract.
/// </summary>
internal sealed class ReturnLoanHandler : IRequestHandler<ReturnLoanRequest, LoanResponse>
{
    private readonly ILibraryService _library;

    public ReturnLoanHandler(ILibraryService library) => _library = library;

    public Task<Result<LoanResponse>> HandleAsync(ReturnLoanRequest request, CancellationToken cancellationToken) =>
        _library.ReturnLoanAsync(request.LoanId, request.Member, cancellationToken)
            .MapAsync(loan => LoanResponse.From(loan));
}
