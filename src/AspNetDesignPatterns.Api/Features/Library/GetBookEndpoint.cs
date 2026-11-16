using KAM.Common.DependencyInjection;
using KAM.Common.Handlers;

namespace AspNetDesignPatterns.Api.Features.Library;

/// <summary><c>GET /library/books/{bookId}</c>.</summary>
internal sealed class GetBookEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/library/books/{bookId:int}", async (
                [AsParameters] GetBookRequest request,
                IRequestHandler<GetBookRequest, BookResponse> handler,
                ILogger<GetBookEndpoint> logger,
                CancellationToken cancellationToken) =>
                await handler.HandleAsync(request, cancellationToken)
                    .LogOnFailureAsync(logger, operation: "GetLibraryBook")
                    .ToHttpResultAsync())
            .WithName("GetLibraryBook")
            .WithTags("Library")
            .WithSummary("Get one book, including how many copies are available.")
            .RequireAuthorization(LibraryPolicies.Read)
            .Produces<BookResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
