using KAM.Common.DependencyInjection;
using KAM.Common.Pagination;

using Microsoft.Extensions.Options;

namespace AspNetDesignPatterns.Api.Features.Library;

/// <summary>One page of the caller's loans, newest first.</summary>
internal sealed class ListLoansHandler : IRequestHandler<ListLoansRequest, CursorPage<LoanResponse>>
{
    private readonly ILibraryService _library;
    private readonly LibraryOptions _options;

    public ListLoansHandler(ILibraryService library, IOptions<LibraryOptions> options)
    {
        _library = library;
        _options = options.Value;
    }

    public Task<Result<CursorPage<LoanResponse>>> HandleAsync(
        ListLoansRequest request,
        CancellationToken cancellationToken)
    {
        int limit = request.EffectiveLimit(_options.DefaultPageSize);
        CursorCodec.TryDecode(request.Cursor, out LoanCursor? after);

        return _library.ListLoansAsync(request.Member, after, limit + 1, cancellationToken)
            .MapAsync(rows => CursorPage
                .From(rows, limit, row => new LoanCursor(row.Id.Value))
                .Map(LoanResponse.From));
    }
}
