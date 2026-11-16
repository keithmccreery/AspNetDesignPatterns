using KAM.Common.DependencyInjection;
using KAM.Common.FluentValidations;
using KAM.Common.Pagination;

namespace AspNetDesignPatterns.Api.Features.Library;

/// <summary><c>GET /library/books</c>: browse or search the catalogue, one cursor page at a time.</summary>
internal sealed class ListBooksEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/library/books", async (
                [AsParameters] ListBooksRequest request,
                IRequestHandler<ListBooksRequest, CursorPage<BookResponse>> handler,
                ILogger<ListBooksEndpoint> logger,
                CancellationToken cancellationToken) =>
                await handler.HandleAsync(request, cancellationToken)
                    .LogOnFailureAsync(logger, operation: "ListLibraryBooks")
                    .ToHttpResultAsync())
            .WithName("ListLibraryBooks")
            .WithTags("Library")
            .WithSummary("List books in title order, optionally filtered by title or author. Pass nextCursor back as cursor for the next page.")
            .RequireAuthorization(LibraryPolicies.Read)
            .ValidateRequest<ListBooksRequest>()
            .Produces<CursorPage<BookResponse>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
