using System.Data.Common;

namespace AspNetDesignPatterns.Api.Features.Library;

/// <summary>
/// The Library slice's gateway to its database: the counterpart of <c>IWeatherClient</c>. SQL
/// in, rows out, and failures surface as a <see cref="DbException"/> for
/// <see cref="ILibraryService"/> to turn into a <see cref="Result"/>.
/// </summary>
/// <remarks>
/// <para>
/// Each method is <b>one operation</b> on the database (one statement, or one write and a
/// read-back of what it wrote) with no business branching. Sequencing several operations,
/// deciding what a missing row means, and drawing the transaction around them are the
/// service's job.
/// </para>
/// <para>
/// Only <see cref="ILibraryService"/> depends on this interface (an architecture test keeps
/// handlers off it). Tests run the real <see cref="LibraryRepository"/> on in-memory SQLite: the
/// SQL, the transaction and the constraints are what they need to prove, and a substitute would
/// fake those away. Every method uses the request's <see cref="KAM.Common.Data.IUnitOfWork"/>, so
/// calls made inside <c>ExecuteInTransactionAsync</c> share its transaction.
/// </para>
/// </remarks>
internal interface ILibraryRepository
{
    /// <summary>
    /// One keyset page of books in (title, id) order, optionally filtered by title or author.
    /// Pass <c>take = limit + 1</c>: the extra row tells <c>CursorPage.From</c> whether there's a next page.
    /// </summary>
    Task<IReadOnlyList<BookRow>> ListBooksAsync(string? search, BookCursor? after, int take, CancellationToken cancellationToken);

    /// <summary>The book, or <see langword="null"/> if no book has that id.</summary>
    Task<BookRow?> GetBookAsync(int bookId, CancellationToken cancellationToken);

    /// <summary>
    /// Takes one copy off the shelf if there is one. A conditional UPDATE, not read-then-write:
    /// the check and the decrement are one statement, so two borrowers can't both take the last copy.
    /// </summary>
    /// <returns><see langword="false"/> when the book doesn't exist or has no copy available.</returns>
    Task<bool> TryTakeCopyAsync(int bookId, CancellationToken cancellationToken);

    /// <summary>Whether the member has a loan of this book that hasn't been returned.</summary>
    Task<bool> HasOpenLoanAsync(int bookId, MemberId member, CancellationToken cancellationToken);

    /// <summary>Records a new loan and returns it.</summary>
    Task<LoanRow> InsertLoanAsync(int bookId, MemberId member, DateTimeOffset borrowedAt, DateTimeOffset dueAt, CancellationToken cancellationToken);

    /// <summary>The member's loan, or <see langword="null"/> if it doesn't exist or belongs to someone else.</summary>
    Task<LoanRow?> GetLoanAsync(int loanId, MemberId member, CancellationToken cancellationToken);

    /// <summary>One keyset page of the member's loans, newest first. Pass <c>take = limit + 1</c>.</summary>
    Task<IReadOnlyList<LoanRow>> ListLoansAsync(MemberId member, LoanCursor? after, int take, CancellationToken cancellationToken);

    /// <summary>Marks the loan returned and returns it. Doesn't touch the book: see <see cref="ReturnCopyAsync"/>.</summary>
    Task<LoanRow> CloseLoanAsync(int loanId, DateTimeOffset returnedAt, CancellationToken cancellationToken);

    /// <summary>Puts one copy of the book back on the shelf; the counterpart of <see cref="TryTakeCopyAsync"/>.</summary>
    Task ReturnCopyAsync(int bookId, CancellationToken cancellationToken);
}

/// <summary>A <c>books</c> row.</summary>
internal sealed class BookRow
{
    public int Id { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Author { get; init; } = string.Empty;

    public int PublishedYear { get; init; }

    public int TotalCopies { get; init; }

    public int AvailableCopies { get; init; }
}

/// <summary>A <c>loans</c> row joined to its book's title.</summary>
internal sealed class LoanRow
{
    public int Id { get; init; }

    public int BookId { get; init; }

    public string BookTitle { get; init; } = string.Empty;

    public DateTimeOffset BorrowedAt { get; init; }

    public DateTimeOffset DueAt { get; init; }

    public DateTimeOffset? ReturnedAt { get; init; }
}
