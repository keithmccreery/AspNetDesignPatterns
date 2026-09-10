using AspNetDesignPatterns.Api.DependencyInjection;
using AspNetDesignPatterns.Api.Shared.Auth;
using AspNetDesignPatterns.Api.Shared.FluentValidations;
using AspNetDesignPatterns.Api.Shared.Handlers;

namespace AspNetDesignPatterns.Api.Features.Weather;

/// <summary>
/// <c>GET /weather/forecast</c>. A thin HTTP adapter: bind + validate the request, delegate
/// to the handler, translate the <c>Result</c> to an HTTP response.
/// </summary>
internal sealed class GetForecastEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/weather/forecast", async (
                [AsParameters] GetForecastRequest request,
                IRequestHandler<GetForecastRequest, ForecastResponse> handler,
                ILogger<GetForecastEndpoint> logger,
                CancellationToken cancellationToken) =>
            {
                // Async Result chain: run the handler, log on failure, translate to HTTP —
                // all without an intermediate `await` or an `if`.
                return await handler.HandleAsync(request, cancellationToken)
                    .LogOnFailureAsync(logger, operation: "GetWeatherForecast")
                    .ToHttpResultAsync();
            })
            .WithName("GetWeatherForecast")
            .WithTags("Weather")
            .WithSummary("Get a daily weather forecast for a set of coordinates.")
            .RequireAuthorization(AuthorizationPolicies.WeatherRead)
            .ValidateRequest<GetForecastRequest>()
            .Produces<ForecastResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status502BadGateway);
    }
}
