using FluentValidation;

namespace KAM.Common.Pagination;

/// <summary>
/// The paging rules every cursor-paged request shares: <c>limit</c> within 1..max and a
/// <c>cursor</c> that decodes as <typeparamref name="TKey"/>. A feature pulls it into its own
/// request validator with <c>Include(new CursorPagedRequestValidator&lt;BookCursor&gt;(max))</c>,
/// so a bad cursor is a 400 <c>ValidationProblemDetails</c> like any other invalid input.
/// </summary>
public sealed class CursorPagedRequestValidator<TKey> : AbstractValidator<ICursorPagedRequest>
    where TKey : notnull
{
    public CursorPagedRequestValidator(int maxLimit)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxLimit);

        RuleFor(x => x.Limit)
            .InclusiveBetween(1, maxLimit)
            .When(x => x.Limit is not null)
            .WithMessage($"Limit must be between 1 and {maxLimit.ToString(System.Globalization.CultureInfo.InvariantCulture)}.");

        RuleFor(x => x.Cursor)
            .Must(cursor => CursorCodec.TryDecode<TKey>(cursor, out _))
            .When(x => x.Cursor is not null)
            .WithMessage("Cursor is not valid. Pass back the nextCursor value from the previous page unchanged.");
    }
}
