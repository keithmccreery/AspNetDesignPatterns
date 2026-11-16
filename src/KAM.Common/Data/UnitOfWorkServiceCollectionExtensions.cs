using KAM.Common.Data;

using Microsoft.Extensions.DependencyInjection.Extensions;

#pragma warning disable IDE0130 // Namespace does not match folder structure (intentional: discoverable on IServiceCollection)
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130

public static class UnitOfWorkServiceCollectionExtensions
{
    /// <summary>
    /// Registers the scoped <see cref="IUnitOfWork"/> — one connection and transaction per
    /// request. Requires an <see cref="IDbConnectionFactory"/> to be registered as well.
    /// Idempotent, so each feature that uses the database can call it from its own
    /// <c>IDependency</c> without coordinating with the others.
    /// </summary>
    public static IServiceCollection AddUnitOfWork(this IServiceCollection services)
    {
        services.TryAddScoped<IUnitOfWork, UnitOfWork>();
        return services;
    }
}
