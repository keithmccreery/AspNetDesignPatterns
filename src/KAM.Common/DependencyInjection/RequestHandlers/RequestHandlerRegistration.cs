using System.Reflection;

using KAM.Common.DependencyInjection;
using KAM.Common.Handlers;

#pragma warning disable IDE0130 // Namespace does not match folder structure (intentional: discoverable on IServiceCollection)
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130

/// <summary>Discovers every closed <see cref="IRequestHandler{TRequest, TResponse}"/> implementation by reflection.</summary>
public static class RequestHandlerRegistration
{
    /// <summary>
    /// Registers every closed <c>IRequestHandler&lt;TRequest, TResponse&gt;</c> implementation
    /// in <paramref name="assemblies"/> (scoped). Defaults to the calling assembly when none
    /// are given — pass every assembly a feature or shared library can define one in (see
    /// <c>Program.cs</c>).
    /// </summary>
    public static IServiceCollection AddRequestHandlers(this IServiceCollection services, params Assembly[] assemblies)
    {
        if (assemblies.Length == 0)
        {
            assemblies = [Assembly.GetCallingAssembly()];
        }

        IEnumerable<(Type Service, Type Implementation)> handlers =
            from type in assemblies.SelectMany(assembly => assembly.ConcreteTypes())
            from @interface in type.GetInterfaces()
            where @interface.IsGenericType
                  && @interface.GetGenericTypeDefinition() == typeof(IRequestHandler<,>)
            select (Service: @interface, Implementation: type.AsType());

        foreach ((Type service, Type implementation) in handlers)
        {
            services.AddScoped(service, implementation);
        }

        return services;
    }
}
