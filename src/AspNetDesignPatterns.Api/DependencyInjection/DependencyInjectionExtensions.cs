using System.Reflection;

using AspNetDesignPatterns.Api.DependencyInjection;
using AspNetDesignPatterns.Api.Shared.Handlers;

using Microsoft.Extensions.DependencyInjection.Extensions;

#pragma warning disable IDE0130 // Namespace does not match folder structure (intentional: discoverable on IServiceCollection)
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130

/// <summary>
/// Reflection-based service registration. A single assembly scan wires up everything that
/// follows a convention — endpoints, settings, request handlers — and <see cref="IDependency"/>
/// implementations contribute anything that does not. Adding a feature never requires editing
/// this file or <c>Program.cs</c>.
/// </summary>
/// <remarks>
/// What each scan registered is asserted by <c>ReflectionRegistrationTests</c> rather than
/// logged at startup, so there is no need to build a throwaway container here.
/// </remarks>
public static class DependencyInjectionExtensions
{
    private static IEnumerable<TypeInfo> ConcreteTypes(Assembly assembly) =>
        assembly.DefinedTypes.Where(t =>
            t is { IsAbstract: false, IsInterface: false, IsGenericTypeDefinition: false });

    /// <summary>Registers every <see cref="IEndpoint"/> in <paramref name="assembly"/> (transient).</summary>
    public static IServiceCollection AddEndpoints(this IServiceCollection services, Assembly? assembly = null)
    {
        assembly ??= Assembly.GetCallingAssembly();

        ServiceDescriptor[] descriptors =
        [
            .. ConcreteTypes(assembly)
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

    /// <summary>Discovers every <see cref="ISettings"/> class and lets it bind + validate itself.</summary>
    public static IServiceCollection AddSettings(this IServiceCollection services, Assembly? assembly = null)
    {
        assembly ??= Assembly.GetCallingAssembly();

        foreach (TypeInfo type in ConcreteTypes(assembly).Where(t => t.IsAssignableTo(typeof(ISettings))))
        {
            if (Activator.CreateInstance(type) is not ISettings settings)
            {
                throw new InvalidOperationException($"Could not create settings instance {type.FullName}.");
            }

            settings.RegisterSettings(services);
        }

        return services;
    }

    /// <summary>Discovers every <see cref="IDependency"/> and invokes its <c>RegisterServices</c>.</summary>
    public static IServiceCollection AddDependencies(this IServiceCollection services, Assembly? assembly = null)
    {
        assembly ??= Assembly.GetCallingAssembly();

        foreach (TypeInfo type in ConcreteTypes(assembly).Where(t => t.IsAssignableTo(typeof(IDependency))))
        {
            if (Activator.CreateInstance(type) is not IDependency dependency)
            {
                throw new InvalidOperationException($"Could not create dependency module {type.FullName}.");
            }

            dependency.RegisterServices(services);
        }

        return services;
    }

    /// <summary>Registers every closed <c>IRequestHandler&lt;TRequest, TResponse&gt;</c> implementation (scoped).</summary>
    public static IServiceCollection AddRequestHandlers(this IServiceCollection services, Assembly? assembly = null)
    {
        assembly ??= Assembly.GetCallingAssembly();

        IEnumerable<(Type Service, Type Implementation)> handlers =
            from type in ConcreteTypes(assembly)
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
