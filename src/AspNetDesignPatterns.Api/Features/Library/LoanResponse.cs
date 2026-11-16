namespace AspNetDesignPatterns.Api.Features.Library;

/// <summary>A loan as the API returns it. <see cref="ReturnedAt"/> is null while the book is still out.</summary>
public sealed record LoanResponse(
    int Id,
    int BookId,
    string BookTitle,
    DateTimeOffset BorrowedAt,
    DateTimeOffset DueAt,
    DateTimeOffset? ReturnedAt)
{
    internal static LoanResponse From(LoanRow row) =>
        new(row.Id, row.BookId, row.BookTitle, row.BorrowedAt, row.DueAt, row.ReturnedAt);
}
