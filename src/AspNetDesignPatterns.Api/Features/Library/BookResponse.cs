namespace AspNetDesignPatterns.Api.Features.Library;

/// <summary>A book as the API returns it. Ids are plain numbers on the wire (see <c>LibraryIds.cs</c>).</summary>
public sealed record BookResponse(
    int Id,
    string Title,
    string Author,
    int PublishedYear,
    int TotalCopies,
    int AvailableCopies)
{
    internal static BookResponse From(BookRow row) =>
        new(row.Id.Value, row.Title, row.Author, row.PublishedYear, row.TotalCopies, row.AvailableCopies);
}
