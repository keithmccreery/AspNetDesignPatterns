using System.Data.Common;

using KAM.Common.Handlers;
using KAM.Common.Pagination;

using Microsoft.Extensions.Options;

namespace AspNetDesignPatterns.Api.Features.Library;

/// <summary>One page of the caller's loans, newest first.</summary>
internal sealed class ListLoansHandler : IRequestHandler<ListLoansRequest, CursorPage<LoanResponse>>
{
    private readonly LibraryRepository _repository;
    private readonly LibraryOptions _options;

    public ListLoansHandler(LibraryRepository repository, IOptions<LibraryOptions> options)
    {
        _repository = repository;
        _options = options.Value;
    }

    public async Task<Result<CursorPage<LoanResponse>>> HandleAsync(
        ListLoansRequest request,
        CancellationToken cancellationToken)
    {
        int limit = request.EffectiveLimit(_options.DefaultPageSize);
        CursorCodec.TryDecode(request.Cursor, out LoanCursor? after);

        try
        {
            IReadOnlyList<LoanRow> rows = await _repository.ListLoansAsync(request.Member, after, limit + 1, cancellationToken);

            return CursorPage
                .From(rows, limit, row => new LoanCursor(row.Id))
                .Map(LoanResponse.From);
        }
        catch (DbException ex)
        {
            return LibraryErrors.DatabaseUnavailable.WithException(ex).WithContext(new { request.Limit });
        }
    }
}
