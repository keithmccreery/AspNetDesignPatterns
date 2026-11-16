using System.Data.Common;

using KAM.Common.Data;
using KAM.Common.Handlers;

namespace AspNetDesignPatterns.Api.Features.Library;

/// <summary>
/// Returns a loan: closes it and puts its copy back on the shelf, in one transaction so the two
/// updates can't be separated.
/// </summary>
internal sealed class ReturnLoanHandler : IRequestHandler<ReturnLoanRequest, LoanResponse>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly LibraryRepository _repository;
    private readonly TimeProvider _timeProvider;

    public ReturnLoanHandler(IUnitOfWork unitOfWork, LibraryRepository repository, TimeProvider timeProvider)
    {
        _unitOfWork = unitOfWork;
        _repository = repository;
        _timeProvider = timeProvider;
    }

    public async Task<Result<LoanResponse>> HandleAsync(ReturnLoanRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return await _unitOfWork.ExecuteInTransactionAsync<LoanResponse>(
                async transactionToken =>
                {
                    LoanRow? loan = await _repository.GetLoanAsync(request.LoanId, request.Member, transactionToken);
                    if (loan is null)
                    {
                        return LibraryErrors.LoanNotFound;
                    }

                    if (loan.ReturnedAt is not null)
                    {
                        return LibraryErrors.LoanAlreadyReturned;
                    }

                    LoanRow returned = await _repository.ReturnLoanAsync(loan, _timeProvider.GetUtcNow(), transactionToken);
                    return LoanResponse.From(returned);
                },
                cancellationToken);
        }
        catch (DbException ex)
        {
            return LibraryErrors.DatabaseUnavailable.WithException(ex).WithContext(new { request.LoanId });
        }
    }
}
