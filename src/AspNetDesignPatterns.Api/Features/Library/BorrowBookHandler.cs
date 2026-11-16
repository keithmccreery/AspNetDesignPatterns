using System.Data.Common;

using KAM.Common.Data;
using KAM.Common.Handlers;

using Microsoft.Extensions.Options;

namespace AspNetDesignPatterns.Api.Features.Library;

/// <summary>
/// Borrows one copy of a book: the slice's unit-of-work example. Everything runs in one
/// transaction that <see cref="IUnitOfWork.ExecuteInTransactionAsync{T}"/> commits only if the
/// result is a success — so a failure partway through (here: the member already has the book)
/// undoes the copy already taken off the shelf, with no compensating code.
/// </summary>
internal sealed class BorrowBookHandler : IRequestHandler<BorrowBookRequest, LoanResponse>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly LibraryRepository _repository;
    private readonly TimeProvider _timeProvider;
    private readonly LibraryOptions _options;

    public BorrowBookHandler(
        IUnitOfWork unitOfWork,
        LibraryRepository repository,
        TimeProvider timeProvider,
        IOptions<LibraryOptions> options)
    {
        _unitOfWork = unitOfWork;
        _repository = repository;
        _timeProvider = timeProvider;
        _options = options.Value;
    }

    public async Task<Result<LoanResponse>> HandleAsync(BorrowBookRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return await _unitOfWork.ExecuteInTransactionAsync<LoanResponse>(
                async transactionToken =>
                {
                    // 1. Take a copy. The conditional UPDATE is the concurrency-safe step, so it
                    //    goes first; if nothing was taken, find out why.
                    if (!await _repository.TryTakeCopyAsync(request.BookId, transactionToken))
                    {
                        return await WhyNoCopyAsync(request, transactionToken);
                    }

                    // 2. A member may hold one copy of a book at a time. Returning a failure
                    //    here rolls back step 1: the copy goes back on the shelf.
                    if (await _repository.HasOpenLoanAsync(request.BookId, request.Member, transactionToken))
                    {
                        return LibraryErrors.AlreadyBorrowed;
                    }

                    // 3. Record the loan. Committed together with step 1, or not at all.
                    DateTimeOffset now = _timeProvider.GetUtcNow();
                    LoanRow loan = await _repository.InsertLoanAsync(
                        request.BookId, request.Member, now, now + _options.LoanPeriod, transactionToken);

                    return LoanResponse.From(loan);
                },
                cancellationToken);
        }
        catch (DbException ex)
        {
            return LibraryErrors.DatabaseUnavailable.WithException(ex).WithContext(new { request.BookId });
        }
    }

    // Nothing was written yet, so there's nothing to roll back -- only the right 4xx to pick.
    // "You already have it" beats "none left" when the member holds the last copy themselves.
    private async Task<Error> WhyNoCopyAsync(BorrowBookRequest request, CancellationToken cancellationToken)
    {
        if (await _repository.GetBookAsync(request.BookId, cancellationToken) is null)
        {
            return LibraryErrors.BookNotFound;
        }

        return await _repository.HasOpenLoanAsync(request.BookId, request.Member, cancellationToken)
            ? LibraryErrors.AlreadyBorrowed
            : LibraryErrors.NoCopiesAvailable;
    }
}
