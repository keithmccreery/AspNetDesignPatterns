using System.Data.Common;

using KAM.Common.Data;

using Microsoft.Extensions.Options;

namespace AspNetDesignPatterns.Api.Features.Library;

/// <summary>
/// <see cref="ILibraryService"/>: sequences <see cref="ILibraryRepository"/> calls, decides which
/// <see cref="LibraryErrors"/> applies, and draws the transaction boundary with
/// <see cref="IUnitOfWork.ExecuteInTransactionAsync{T}"/>.
/// </summary>
/// <remarks>
/// Every method runs inside <see cref="GuardAsync{T}"/>, which turns a <see cref="DbException"/>
/// into <see cref="LibraryErrors.DatabaseUnavailable"/>. That covers the unit of work's own
/// failures too (opening the connection, <c>BEGIN IMMEDIATE</c> timing out on SQLite's write lock,
/// <c>COMMIT</c>), which happen outside any repository call. Takes no <c>ILogger</c>, like
/// <c>WeatherService</c>: the error carries the exception and context, and the endpoint logs it.
/// </remarks>
internal sealed class LibraryService : ILibraryService
{
    private readonly ILibraryRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly LibraryOptions _options;

    public LibraryService(
        ILibraryRepository repository,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        IOptions<LibraryOptions> options)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
        _options = options.Value;
    }

    public Task<Result<IReadOnlyList<BookRow>>> ListBooksAsync(
        string? search,
        BookCursor? after,
        int take,
        CancellationToken cancellationToken) =>
        GuardAsync(
            async () => Result.Success(await _repository.ListBooksAsync(search, after, take, cancellationToken)),
            new { Search = search, Take = take });

    public Task<Result<BookRow>> GetBookAsync(BookId bookId, CancellationToken cancellationToken) =>
        GuardAsync<BookRow>(
            async () => await _repository.GetBookAsync(bookId, cancellationToken) is { } book
                ? book
                : LibraryErrors.BookNotFound,
            new { BookId = bookId.Value });

    /// <remarks>
    /// The slice's unit-of-work example: a failure partway through (the member already has the
    /// book) returns an error, and the transaction rolls back the copy already taken, with no
    /// compensating code.
    /// </remarks>
    public Task<Result<LoanRow>> BorrowBookAsync(BookId bookId, MemberId member, CancellationToken cancellationToken) =>
        GuardAsync(
            () => _unitOfWork.ExecuteInTransactionAsync<LoanRow>(
                async transactionToken =>
                {
                    // 1. Take a copy. The conditional UPDATE is the concurrency-safe step, so it
                    //    goes first; if nothing was taken, find out why.
                    if (!await _repository.TryTakeCopyAsync(bookId, transactionToken))
                    {
                        return await WhyNoCopyAsync(bookId, member, transactionToken);
                    }

                    // 2. A member may hold one copy of a book at a time. Returning a failure
                    //    here rolls back step 1: the copy goes back on the shelf.
                    if (await _repository.HasOpenLoanAsync(bookId, member, transactionToken))
                    {
                        return LibraryErrors.AlreadyBorrowed;
                    }

                    // 3. Record the loan. Committed together with step 1, or not at all.
                    DateTimeOffset now = _timeProvider.GetUtcNow();
                    return await _repository.InsertLoanAsync(bookId, member, now, now + _options.LoanPeriod, transactionToken);
                },
                cancellationToken),
            new { BookId = bookId.Value });

    public Task<Result<LoanRow>> GetLoanAsync(LoanId loanId, MemberId member, CancellationToken cancellationToken) =>
        GuardAsync<LoanRow>(
            async () => await _repository.GetLoanAsync(loanId, member, cancellationToken) is { } loan
                ? loan
                : LibraryErrors.LoanNotFound,
            new { LoanId = loanId.Value });

    public Task<Result<IReadOnlyList<LoanRow>>> ListLoansAsync(
        MemberId member,
        LoanCursor? after,
        int take,
        CancellationToken cancellationToken) =>
        GuardAsync(
            async () => Result.Success(await _repository.ListLoansAsync(member, after, take, cancellationToken)),
            new { Take = take });

    public Task<Result<LoanRow>> ReturnLoanAsync(LoanId loanId, MemberId member, CancellationToken cancellationToken) =>
        GuardAsync(
            () => _unitOfWork.ExecuteInTransactionAsync<LoanRow>(
                async transactionToken =>
                {
                    LoanRow? loan = await _repository.GetLoanAsync(loanId, member, transactionToken);
                    if (loan is null)
                    {
                        return LibraryErrors.LoanNotFound;
                    }

                    if (loan.ReturnedAt is not null)
                    {
                        return LibraryErrors.LoanAlreadyReturned;
                    }

                    // Two writes to two tables: committed together, or not at all.
                    LoanRow closed = await _repository.CloseLoanAsync(loan.Id, _timeProvider.GetUtcNow(), transactionToken);
                    await _repository.ReturnCopyAsync(loan.BookId, transactionToken);
                    return closed;
                },
                cancellationToken),
            new { LoanId = loanId.Value });

    // The one place this service turns an exception into a Result. The context is attached for
    // the log; the exception message never reaches the client (ProblemDetails shows the Error's).
    private static Task<Result<T>> GuardAsync<T>(Func<Task<Result<T>>> work, object context) =>
        Result.TryAsync(work, (DbException ex) => LibraryErrors.DatabaseUnavailable.WithException(ex).WithContext(context));

    // Nothing was written yet, so there's nothing to roll back -- only the right 4xx to pick.
    // "You already have it" beats "none left" when the member holds the last copy themselves.
    private async Task<Error> WhyNoCopyAsync(BookId bookId, MemberId member, CancellationToken cancellationToken)
    {
        if (await _repository.GetBookAsync(bookId, cancellationToken) is null)
        {
            return LibraryErrors.BookNotFound;
        }

        return await _repository.HasOpenLoanAsync(bookId, member, cancellationToken)
            ? LibraryErrors.AlreadyBorrowed
            : LibraryErrors.NoCopiesAvailable;
    }
}
