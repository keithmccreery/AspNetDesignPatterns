
namespace AspNetDesignPatterns.Api.Shared.Handlers;

/// <summary>
/// Handles one request type and produces one response, always as a <see cref="Result{T}"/>.
/// Endpoints resolve <c>IRequestHandler&lt;TRequest, TResponse&gt;</c> from DI and delegate
/// straight to it, so the endpoint stays a thin HTTP adapter and the behaviour is unit-testable
/// without a web host.
/// </summary>
public interface IRequestHandler<in TRequest, TResponse>
{
    Task<Result<TResponse>> HandleAsync(TRequest request, CancellationToken cancellationToken);
}
