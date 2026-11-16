using KAM.Common.DependencyInjection;
using KAM.Common.FluentValidations;
using KAM.Common.Handlers;
using KAM.Common.Pagination;

namespace AspNetDesignPatterns.Api.Features.Library;

/// <summary><c>GET /library/loans</c>: the caller's loans, newest first, one cursor page at a time.</summary>
internal sealed class ListLoansEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/library/loans", async (
                [AsParameters] ListLoansRequest request,
                IRequestHandler<ListLoansRequest, CursorPage<LoanResponse>> handler,
                ILogger<ListLoansEndpoint> logger,
                CancellationToken cancellationToken) =>
                await handler.HandleAsync(request, cancellationToken)
                    .LogOnFailureAsync(logger, operation: "ListLibraryLoans")
                    .ToHttpResultAsync())
            .WithName("ListLibraryLoans")
            .WithTags("Library")
            .WithSummary("List your loans, newest first, including returned ones. Pass nextCursor back as cursor for the next page.")
            .RequireAuthorization(LibraryPolicies.Read)
            .ValidateRequest<ListLoansRequest>()
            .Produces<CursorPage<LoanResponse>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
