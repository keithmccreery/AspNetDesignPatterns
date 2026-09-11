using AspNetDesignPatterns.Api.DependencyInjection;

#pragma warning disable IDE0130 // Namespace does not match folder structure (intentional: discoverable on IServiceCollection)
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130

/// <summary>
/// Wires up <see cref="GlobalExceptionHandler"/> so <c>Program.cs</c> never needs visibility
/// into the (internal) handler type itself — the same shape as <c>AddJwtAuth()</c> or
/// <c>AddHealthChecks()</c>.
/// </summary>
public static class ExceptionHandlingRegistration
{
    /// <summary>Registers the last-resort <see cref="GlobalExceptionHandler"/> for anything a client throws that escapes a request.</summary>
    public static IServiceCollection AddGlobalExceptionHandler(this IServiceCollection services) =>
        services.AddExceptionHandler<GlobalExceptionHandler>();
}
