using KAM.Common.DependencyInjection;
using KAM.Common.Handlers;

namespace AspNetDesignPatterns.Api.Features.Library;

/// <summary><c>GET /library/loans/{loanId}</c>: one of the caller's loans (404 for anyone else's).</summary>
internal sealed class GetLoanEndpoint : IEndpoint
{
    /// <summary>The route name <see cref="BorrowBookEndpoint"/>'s <c>Location</c> header links to.</summary>
    public const string RouteName = "GetLibraryLoan";

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/library/loans/{loanId:int}", async (
                [AsParameters] GetLoanRequest request,
                IRequestHandler<GetLoanRequest, LoanResponse> handler,
                ILogger<GetLoanEndpoint> logger,
                CancellationToken cancellationToken) =>
                await handler.HandleAsync(request, cancellationToken)
                    .LogOnFailureAsync(logger, operation: RouteName)
                    .ToHttpResultAsync())
            .WithName(RouteName)
            .WithTags("Library")
            .WithSummary("Get one of your loans.")
            .RequireAuthorization(LibraryPolicies.Read)
            .Produces<LoanResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
