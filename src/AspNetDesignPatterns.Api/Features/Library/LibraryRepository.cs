using System.Data.Common;

using Dapper;

using KAM.Common.Data;

namespace AspNetDesignPatterns.Api.Features.Library;

/// <summary>
/// <see cref="ILibraryRepository"/> with Dapper over the request's <see cref="IUnitOfWork"/>
/// connection, enlisting in its transaction when one is open. All of the slice's SQL lives here.
/// </summary>
internal sealed class LibraryRepository : ILibraryRepository
{
    private const string BOOK_COLUMNS =
        "id AS Id, title AS Title, author AS Author, published_year AS PublishedYear, " +
        "total_copies AS TotalCopies, available_copies AS AvailableCopies";

    private const string LOAN_SELECT =
        "SELECT l.id AS Id, l.book_id AS BookId, b.title AS BookTitle, l.borrowed_at AS BorrowedAt, " +
        "l.due_at AS DueAt, l.returned_at AS ReturnedAt FROM loans l JOIN books b ON b.id = l.book_id";

    private readonly IUnitOfWork _unitOfWork;

    public LibraryRepository(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

    /// <inheritdoc />
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

    /// <inheritdoc />
    public async Task<BookRow?> GetBookAsync(int bookId, CancellationToken cancellationToken)
    {
        const string SQL = $"SELECT {BOOK_COLUMNS} FROM books WHERE id = @BookId;";
        DbConnection connection = await _unitOfWork.GetConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<BookRow>(Command(SQL, new { BookId = bookId }, cancellationToken));
    }

    /// <inheritdoc />
    public async Task<bool> TryTakeCopyAsync(int bookId, CancellationToken cancellationToken)
    {
        const string SQL = """
            UPDATE books SET available_copies = available_copies - 1
            WHERE id = @BookId AND available_copies > 0;
            """;
        return await ExecuteAsync(SQL, new { BookId = bookId }, cancellationToken) == 1;
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
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

    /// <inheritdoc />
    public async Task<LoanRow?> GetLoanAsync(int loanId, MemberId member, CancellationToken cancellationToken)
    {
        const string SQL = $"{LOAN_SELECT} WHERE l.id = @LoanId AND l.member_id = @MemberId;";
        DbConnection connection = await _unitOfWork.GetConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<LoanRow>(
            Command(SQL, new { LoanId = loanId, MemberId = member.Value }, cancellationToken));
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
    public async Task<LoanRow> CloseLoanAsync(int loanId, DateTimeOffset returnedAt, CancellationToken cancellationToken)
    {
        await ExecuteAsync(
            "UPDATE loans SET returned_at = @ReturnedAt WHERE id = @LoanId AND returned_at IS NULL;",
            new { LoanId = loanId, ReturnedAt = returnedAt },
            cancellationToken);

        return await GetRequiredLoanAsync(loanId, cancellationToken);
    }

    /// <inheritdoc />
    public Task ReturnCopyAsync(int bookId, CancellationToken cancellationToken) =>
        ExecuteAsync(
            "UPDATE books SET available_copies = available_copies + 1 WHERE id = @BookId;",
            new { BookId = bookId },
            cancellationToken);

    // A loan this repository has just written: QuerySingle, so a missing row (which would be a
    // bug) throws here, in the layer allowed to throw, instead of reaching the service as null.
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
