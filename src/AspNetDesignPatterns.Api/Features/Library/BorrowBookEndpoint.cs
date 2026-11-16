using KAM.Common.DependencyInjection;
using KAM.Common.Handlers;

namespace AspNetDesignPatterns.Api.Features.Library;

/// <summary>
/// <c>POST /library/books/{bookId}/loans</c>: borrow a copy. Creates a loan, so a success is
/// <c>201 Created</c> with a <c>Location</c> header pointing at <see cref="GetLoanEndpoint"/>.
/// </summary>
internal sealed class BorrowBookEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/library/books/{bookId:int}/loans", async (
                [AsParameters] BorrowBookRequest request,
                IRequestHandler<BorrowBookRequest, LoanResponse> handler,
                ILogger<BorrowBookEndpoint> logger,
                CancellationToken cancellationToken) =>
                await handler.HandleAsync(request, cancellationToken)
                    .LogOnFailureAsync(logger, operation: "BorrowLibraryBook")
                    .ToHttpResultAsync(loan => TypedResults.CreatedAtRoute(
                        loan, GetLoanEndpoint.RouteName, new { loanId = loan.Id })))
            .WithName("BorrowLibraryBook")
            .WithTags("Library")
            .WithSummary("Borrow one copy of a book. Fails with 409 when no copy is available or you already have one.")
            .RequireAuthorization(LibraryPolicies.Write)
            .Produces<LoanResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
