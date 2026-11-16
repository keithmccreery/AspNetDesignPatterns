using System.Data.Common;

using KAM.Common.Handlers;

namespace AspNetDesignPatterns.Api.Features.Library;

/// <summary>One of the caller's loans, or <see cref="LibraryErrors.LoanNotFound"/>.</summary>
internal sealed class GetLoanHandler : IRequestHandler<GetLoanRequest, LoanResponse>
{
    private readonly LibraryRepository _repository;

    public GetLoanHandler(LibraryRepository repository) => _repository = repository;

    public async Task<Result<LoanResponse>> HandleAsync(GetLoanRequest request, CancellationToken cancellationToken)
    {
        try
        {
            LoanRow? loan = await _repository.GetLoanAsync(request.LoanId, request.Member, cancellationToken);
            return loan is null ? LibraryErrors.LoanNotFound : LoanResponse.From(loan);
        }
        catch (DbException ex)
        {
            return LibraryErrors.DatabaseUnavailable.WithException(ex).WithContext(new { request.LoanId });
        }
    }
}
