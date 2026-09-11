using System.Reflection;

using KAM.Common.DependencyInjection;

using Microsoft.Extensions.DependencyInjection.Extensions;

#pragma warning disable IDE0130 // Namespace does not match folder structure (intentional: discoverable on IServiceCollection)
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130

/// <summary>
/// Discovers and maps every <see cref="IEndpoint"/> by reflection, so a new feature slice
/// never means editing a central "routes" file or <c>Program.cs</c>.
/// </summary>
public static class EndpointRegistration
{
    /// <summary>
    /// Registers every <see cref="IEndpoint"/> in <paramref name="assemblies"/> (transient).
    /// Defaults to the calling assembly when none are given — pass every assembly a feature
    /// or shared library can define one in (see <c>Program.cs</c>).
    /// </summary>
    public static IServiceCollection AddEndpoints(this IServiceCollection services, params Assembly[] assemblies)
    {
        if (assemblies.Length == 0)
        {
            assemblies = [Assembly.GetCallingAssembly()];
        }

        ServiceDescriptor[] descriptors =
        [
            .. assemblies
                .SelectMany(assembly => assembly.ConcreteTypes())
                .Where(t => t.IsAssignableTo(typeof(IEndpoint)))
                .Select(t => ServiceDescriptor.Transient(typeof(IEndpoint), t)),
        ];

        services.TryAddEnumerable(descriptors);
        return services;
    }

    /// <summary>Resolves and maps every <see cref="IEndpoint"/> onto <paramref name="routeGroupBuilder"/> (or the app).</summary>
    public static IEndpointRouteBuilder MapEndpoints(this WebApplication app, RouteGroupBuilder? routeGroupBuilder = null)
    {
        IEndpointRouteBuilder target = routeGroupBuilder ?? (IEndpointRouteBuilder) app;

        foreach (IEndpoint endpoint in app.Services.GetRequiredService<IEnumerable<IEndpoint>>())
        {
            endpoint.MapEndpoint(target);
            app.Logger.LogDebug("Mapped endpoint {Endpoint}", endpoint.GetType().Name);
        }

        return app;
    }
}
