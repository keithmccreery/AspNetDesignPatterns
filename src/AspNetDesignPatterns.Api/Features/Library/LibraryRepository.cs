using System.Data.Common;

using Dapper;

using KAM.Common.Data;

namespace AspNetDesignPatterns.Api.Features.Library;

/// <summary>
/// The Library slice's data access: Dapper over the request's <see cref="IUnitOfWork"/>
/// connection, enlisting in its transaction when one is open. This is the slice's "client"
/// layer, the database counterpart of <c>OpenMeteoWeatherClient</c>: SQL in, rows out, and
/// failures surface as a <see cref="DbException"/> for the handler to turn into a
/// <see cref="Result"/>. No business rules live here.
/// </summary>
internal sealed class LibraryRepository
{
    private const string BOOK_COLUMNS =
        "id AS Id, title AS Title, author AS Author, published_year AS PublishedYear, " +
        "total_copies AS TotalCopies, available_copies AS AvailableCopies";

    private const string LOAN_SELECT =
        "SELECT l.id AS Id, l.book_id AS BookId, b.title AS BookTitle, l.borrowed_at AS BorrowedAt, " +
        "l.due_at AS DueAt, l.returned_at AS ReturnedAt FROM loans l JOIN books b ON b.id = l.book_id";

    private readonly IUnitOfWork _unitOfWork;

    public LibraryRepository(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

    /// <summary>
    /// One keyset page of books in (title, id) order, optionally filtered. Pass <c>take = limit + 1</c>;
    /// the extra row tells <c>CursorPage.From</c> whether there's a next page.
    /// </summary>
    public async Task<IReadOnlyList<BookRow>> ListBooksAsync(
        string? search,
        BookCursor? after,
        int take,
        CancellationToken cancellationToken)
    {
        // (title, id) > (@AfterTitle, @AfterId) is a row-value comparison: "after this exact
        // position", which the (title, id) index can seek to. The id makes the order strict, so
        // two books with the same title are neither skipped nor repeated at a page boundary.
        const string SQL = $"""
            SELECT {BOOK_COLUMNS}
            FROM books
            WHERE (@Pattern IS NULL OR title LIKE @Pattern ESCAPE '\' OR author LIKE @Pattern ESCAPE '\')
              AND (@AfterTitle IS NULL OR (title, id) > (@AfterTitle, @AfterId))
            ORDER BY title, id
            LIMIT @Take;
            """;

        return [.. await QueryAsync<BookRow>(
            SQL,
            new { Pattern = ContainsPattern(search), AfterTitle = after?.Title, AfterId = after?.Id, Take = take },
            cancellationToken)];
    }

    public async Task<BookRow?> GetBookAsync(int bookId, CancellationToken cancellationToken)
    {
        const string SQL = $"SELECT {BOOK_COLUMNS} FROM books WHERE id = @BookId;";
        DbConnection connection = await _unitOfWork.GetConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<BookRow>(Command(SQL, new { BookId = bookId }, cancellationToken));
    }

    /// <summary>
    /// Takes one copy off the shelf if there is one. A conditional UPDATE, not read-then-write:
    /// the check and the decrement are one statement, so two borrowers can't both take the last copy.
    /// </summary>
    /// <returns><see langword="false"/> when the book doesn't exist or has no copy available.</returns>
    public async Task<bool> TryTakeCopyAsync(int bookId, CancellationToken cancellationToken)
    {
        const string SQL = """
            UPDATE books SET available_copies = available_copies - 1
            WHERE id = @BookId AND available_copies > 0;
            """;
        return await ExecuteAsync(SQL, new { BookId = bookId }, cancellationToken) == 1;
    }

    public async Task<bool> HasOpenLoanAsync(int bookId, MemberId member, CancellationToken cancellationToken)
    {
        const string SQL = """
            SELECT EXISTS (
                SELECT 1 FROM loans
                WHERE book_id = @BookId AND member_id = @MemberId AND returned_at IS NULL);
            """;
        DbConnection connection = await _unitOfWork.GetConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(
            Command(SQL, new { BookId = bookId, MemberId = member.Value }, cancellationToken));
    }

    public async Task<LoanRow> InsertLoanAsync(
        int bookId,
        MemberId member,
        DateTimeOffset borrowedAt,
        DateTimeOffset dueAt,
        CancellationToken cancellationToken)
    {
        const string SQL = """
            INSERT INTO loans (book_id, member_id, borrowed_at, due_at)
            VALUES (@BookId, @MemberId, @BorrowedAt, @DueAt)
            RETURNING id;
            """;
        DbConnection connection = await _unitOfWork.GetConnectionAsync(cancellationToken);
        int loanId = await connection.ExecuteScalarAsync<int>(Command(
            SQL,
            new { BookId = bookId, MemberId = member.Value, BorrowedAt = borrowedAt, DueAt = dueAt },
            cancellationToken));

        return await GetRequiredLoanAsync(loanId, cancellationToken);
    }

    /// <summary>The member's loan, or <see langword="null"/> if it doesn't exist or belongs to someone else.</summary>
    public async Task<LoanRow?> GetLoanAsync(int loanId, MemberId member, CancellationToken cancellationToken)
    {
        const string SQL = $"{LOAN_SELECT} WHERE l.id = @LoanId AND l.member_id = @MemberId;";
        DbConnection connection = await _unitOfWork.GetConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<LoanRow>(
            Command(SQL, new { LoanId = loanId, MemberId = member.Value }, cancellationToken));
    }

    /// <summary>One keyset page of the member's loans, newest first. Pass <c>take = limit + 1</c>.</summary>
    public async Task<IReadOnlyList<LoanRow>> ListLoansAsync(
        MemberId member,
        LoanCursor? after,
        int take,
        CancellationToken cancellationToken)
    {
        // Loan ids only ever increase, so id alone is a strict "newest first" order -- a
        // single-column cursor, where books need (title, id).
        const string SQL = $"""
            {LOAN_SELECT}
            WHERE l.member_id = @MemberId AND (@AfterId IS NULL OR l.id < @AfterId)
            ORDER BY l.id DESC
            LIMIT @Take;
            """;

        return [.. await QueryAsync<LoanRow>(
            SQL,
            new { MemberId = member.Value, AfterId = after?.Id, Take = take },
            cancellationToken)];
    }

    /// <summary>Closes the loan and puts its copy back on the shelf. Two statements: call inside a transaction.</summary>
    public async Task<LoanRow> ReturnLoanAsync(LoanRow loan, DateTimeOffset returnedAt, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(loan);

        await ExecuteAsync(
            "UPDATE loans SET returned_at = @ReturnedAt WHERE id = @LoanId AND returned_at IS NULL;",
            new { LoanId = loan.Id, ReturnedAt = returnedAt },
            cancellationToken);

        await ExecuteAsync(
            "UPDATE books SET available_copies = available_copies + 1 WHERE id = @BookId;",
            new { loan.BookId },
            cancellationToken);

        return await GetRequiredLoanAsync(loan.Id, cancellationToken);
    }

    // A loan this repository has just written: QuerySingle, so a missing row (which would be a
    // bug) throws here, in the layer allowed to throw, instead of reaching a handler as null.
    private async Task<LoanRow> GetRequiredLoanAsync(int loanId, CancellationToken cancellationToken)
    {
        DbConnection connection = await _unitOfWork.GetConnectionAsync(cancellationToken);
        return await connection.QuerySingleAsync<LoanRow>(
            Command($"{LOAN_SELECT} WHERE l.id = @LoanId;", new { LoanId = loanId }, cancellationToken));
    }

    private async Task<IEnumerable<T>> QueryAsync<T>(string sql, object parameters, CancellationToken cancellationToken)
    {
        DbConnection connection = await _unitOfWork.GetConnectionAsync(cancellationToken);
        return await connection.QueryAsync<T>(Command(sql, parameters, cancellationToken));
    }

    private async Task<int> ExecuteAsync(string sql, object parameters, CancellationToken cancellationToken)
    {
        DbConnection connection = await _unitOfWork.GetConnectionAsync(cancellationToken);
        return await connection.ExecuteAsync(Command(sql, parameters, cancellationToken));
    }

    // Every command names the unit of work's transaction (null outside one): a provider rejects
    // a command on a connection with an open transaction that doesn't enlist in it.
    private CommandDefinition Command(string sql, object parameters, CancellationToken cancellationToken) =>
        new(sql, parameters, _unitOfWork.Transaction, cancellationToken: cancellationToken);

    // User input inside LIKE: escape its own wildcards so "50%" means the text "50%".
    private static string? ContainsPattern(string? search) =>
        string.IsNullOrWhiteSpace(search)
            ? null
            : "%" + search.Trim()
                .Replace(@"\", @"\\", StringComparison.Ordinal)
                .Replace("%", @"\%", StringComparison.Ordinal)
                .Replace("_", @"\_", StringComparison.Ordinal) + "%";
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
