using FluentValidation;

using KAM.Common.Pagination;

using Microsoft.Extensions.Options;

namespace AspNetDesignPatterns.Api.Features.Library;

/// <summary>
/// <c>GET /library/loans</c>: the caller's own loans, newest first. <see cref="Member"/> is bound
/// from the access token (<see cref="MemberId.BindAsync"/>), the rest from the query string.
/// </summary>
internal sealed record ListLoansRequest(MemberId Member, int? Limit, string? Cursor) : ICursorPagedRequest;

/// <summary>A position in a member's loan list: the id of the last loan on a page.</summary>
internal sealed record LoanCursor(int Id);

internal sealed class ListLoansRequestValidator : AbstractValidator<ListLoansRequest>
{
    public ListLoansRequestValidator(IOptions<LibraryOptions> options) =>
        Include(new CursorPagedRequestValidator<LoanCursor>(options.Value.MaxPageSize));
}
