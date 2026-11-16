using KAM.Common.DependencyInjection;

namespace AspNetDesignPatterns.Api.Features.Library;

/// <summary><c>POST /library/loans/{loanId}/return</c>: return a borrowed book.</summary>
internal sealed class ReturnLoanEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/library/loans/{loanId:int}/return", async (
                [AsParameters] ReturnLoanRequest request,
                IRequestHandler<ReturnLoanRequest, LoanResponse> handler,
                ILogger<ReturnLoanEndpoint> logger,
                CancellationToken cancellationToken) =>
                await handler.HandleAsync(request, cancellationToken)
                    .LogOnFailureAsync(logger, operation: "ReturnLibraryLoan")
                    .ToHttpResultAsync())
            .WithName("ReturnLibraryLoan")
            .WithTags("Library")
            .WithSummary("Return one of your loans; its copy becomes available again.")
            .RequireAuthorization(LibraryPolicies.Write)
            .Produces<LoanResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
