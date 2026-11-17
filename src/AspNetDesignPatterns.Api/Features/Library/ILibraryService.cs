namespace AspNetDesignPatterns.Api.Features.Library;

/// <summary>
/// The Library slice's service layer, the counterpart of <c>IWeatherService</c>: the business
/// rules, the order of the repository calls, and the transaction boundary. Every method returns a
/// <see cref="Result{T}"/>; a database failure becomes <see cref="LibraryErrors.DatabaseUnavailable"/>
/// here, so nothing above this layer handles exceptions.
/// </summary>
/// <remarks>
/// Speaks in rows (<see cref="BookRow"/>, <see cref="LoanRow"/>), not the API contract: mapping to
/// responses and encoding cursors belong to the handlers.
/// </remarks>
internal interface ILibraryService
{
    /// <summary>
    /// One keyset page of books, optionally filtered by title or author. Pass <c>take = limit + 1</c>:
    /// the extra row tells <c>CursorPage.From</c> whether there's a next page.
    /// </summary>
    Task<Result<IReadOnlyList<BookRow>>> ListBooksAsync(string? search, BookCursor? after, int take, CancellationToken cancellationToken);

    /// <summary>The book, or <see cref="LibraryErrors.BookNotFound"/>.</summary>
    Task<Result<BookRow>> GetBookAsync(BookId bookId, CancellationToken cancellationToken);

    /// <summary>
    /// Takes a copy and records a loan due after <see cref="LibraryOptions.LoanPeriod"/>, in one
    /// transaction. Fails with <see cref="LibraryErrors.BookNotFound"/>,
    /// <see cref="LibraryErrors.AlreadyBorrowed"/> or <see cref="LibraryErrors.NoCopiesAvailable"/>,
    /// leaving the database as it was.
    /// </summary>
    Task<Result<LoanRow>> BorrowBookAsync(BookId bookId, MemberId member, CancellationToken cancellationToken);

    /// <summary>The member's loan, or <see cref="LibraryErrors.LoanNotFound"/> (also for someone else's).</summary>
    Task<Result<LoanRow>> GetLoanAsync(LoanId loanId, MemberId member, CancellationToken cancellationToken);

    /// <summary>One keyset page of the member's loans, newest first. Pass <c>take = limit + 1</c>.</summary>
    Task<Result<IReadOnlyList<LoanRow>>> ListLoansAsync(MemberId member, LoanCursor? after, int take, CancellationToken cancellationToken);

    /// <summary>
    /// Closes the member's loan and puts its copy back, in one transaction. Fails with
    /// <see cref="LibraryErrors.LoanNotFound"/> or <see cref="LibraryErrors.LoanAlreadyReturned"/>.
    /// </summary>
    Task<Result<LoanRow>> ReturnLoanAsync(LoanId loanId, MemberId member, CancellationToken cancellationToken);
}
