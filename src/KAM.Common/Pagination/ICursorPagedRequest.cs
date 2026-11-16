namespace KAM.Common.Pagination;

/// <summary>
/// A request for one page of a cursor-paged (keyset) list. Implemented by the feature's own
/// request record — not a base class, so every request stays a flat <c>sealed record</c> bound
/// with <c>[AsParameters]</c>; this interface is only what <see cref="CursorPagedRequestValidator{TKey}"/>
/// and <see cref="CursorPagedRequestExtensions"/> need to read.
/// </summary>
public interface ICursorPagedRequest
{
    /// <summary>Page size. <see langword="null"/> means the feature's default.</summary>
    int? Limit { get; }

    /// <summary>
    /// The <see cref="CursorPage{T}.NextCursor"/> from the previous page, or
    /// <see langword="null"/> for the first page.
    /// </summary>
    string? Cursor { get; }
}

public static class CursorPagedRequestExtensions
{
    /// <summary>The page size to use: the requested <see cref="ICursorPagedRequest.Limit"/>, or <paramref name="defaultLimit"/>.</summary>
    public static int EffectiveLimit(this ICursorPagedRequest request, int defaultLimit)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Limit ?? defaultLimit;
    }
}
