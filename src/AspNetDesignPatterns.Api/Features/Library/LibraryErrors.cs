namespace AspNetDesignPatterns.Api.Features.Library;

/// <summary>
/// Every <see cref="Error"/> the Library slice can return, defined once. The codes are part of
/// the API's contract: they reach the client in <c>ProblemDetails</c> and are what logs and
/// tests match on.
/// </summary>
internal static class LibraryErrors
{
    public static readonly Error BookNotFound = Error.NotFound(
        "Library.BookNotFound",
        "No book exists with the supplied id.");

    public static readonly Error NoCopiesAvailable = Error.Conflict(
        "Library.NoCopiesAvailable",
        "Every copy of this book is currently on loan.");

    public static readonly Error AlreadyBorrowed = Error.Conflict(
        "Library.AlreadyBorrowed",
        "You already have this book on loan.");

    /// <summary>Also returned for another member's loan, so a loan id never reveals that it exists.</summary>
    public static readonly Error LoanNotFound = Error.NotFound(
        "Library.LoanNotFound",
        "No loan of yours exists with the supplied id.");

    public static readonly Error LoanAlreadyReturned = Error.Conflict(
        "Library.LoanAlreadyReturned",
        "This loan has already been returned.");

    /// <summary>The database failed (a <see cref="System.Data.Common.DbException"/>) — HTTP 500.</summary>
    public static readonly Error DatabaseUnavailable = Error.Failure(
        "Library.DatabaseUnavailable",
        "The library database is currently unavailable. Please retry shortly.");
}
