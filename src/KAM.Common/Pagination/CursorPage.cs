using System.Text.Json.Serialization;

namespace KAM.Common.Pagination;

/// <summary>One page of a cursor-paged list.</summary>
/// <param name="Items">The page's items, in the list's sort order.</param>
/// <param name="NextCursor">
/// Pass back as <c>cursor</c> to get the next page; <see langword="null"/> on the last page.
/// </param>
public sealed record CursorPage<T>(
    IReadOnlyList<T> Items,
    // Always serialized, even as null (overriding an app-wide WhenWritingNull): "nextCursor": null
    // is how a client knows it has reached the end, so it shouldn't have to infer that from absence.
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? NextCursor);

public static class CursorPage
{
    /// <summary>
    /// Builds a page from rows fetched with <c>LIMIT limit + 1</c>: the extra row, if present,
    /// is how the query says there's a next page. It's dropped, and the key of the last row
    /// kept becomes <see cref="CursorPage{T}.NextCursor"/>.
    /// </summary>
    public static CursorPage<T> From<T, TKey>(IReadOnlyList<T> rows, int limit, Func<T, TKey> keyOf)
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(keyOf);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);

        if (rows.Count <= limit)
        {
            return new CursorPage<T>(rows, NextCursor: null);
        }

        T[] items = [.. rows.Take(limit)];
        return new CursorPage<T>(items, CursorCodec.Encode(keyOf(items[^1])));
    }

    /// <summary>Maps each item, keeping the cursor — e.g. from a database row to the response DTO.</summary>
    public static CursorPage<TOut> Map<TIn, TOut>(this CursorPage<TIn> page, Func<TIn, TOut> map)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(map);
        return new CursorPage<TOut>([.. page.Items.Select(map)], page.NextCursor);
    }
}
