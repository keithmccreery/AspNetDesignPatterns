using System.Reflection;

using KAM.Common.DependencyInjection;

using Microsoft.Extensions.DependencyInjection.Extensions;

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
    /// <c>Program.cs</c>). Idempotent: re-scanning registers nothing new.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Two different handlers exist for the same <c>IRequestHandler&lt;TRequest, TResponse&gt;</c>
    /// (found by the scan, or one already registered) — with plain <c>AddScoped</c> the last one
    /// would silently win.
    /// </exception>
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

        foreach (IGrouping<Type, Type> handlersForService in handlers.GroupBy(h => h.Service, h => h.Implementation))
        {
            Type service = handlersForService.Key;

            // Distinct: the same handler found twice (an assembly passed twice) isn't a conflict.
            List<string> candidates = [.. handlersForService.Distinct().Select(t => t.FullName ?? t.Name)];
            ServiceDescriptor? existing = services.FirstOrDefault(d => d.ServiceType == service);
            if (existing is not null)
            {
                string existingName = existing.ImplementationType?.FullName ?? "(a factory or instance registration)";
                if (!candidates.Contains(existingName, StringComparer.Ordinal))
                {
                    candidates.Add(existingName);
                }
            }

            if (candidates.Count > 1)
            {
                throw new InvalidOperationException(
                    $"More than one handler for {nameof(IRequestHandler<object, object>)}<{string.Join(", ", service.GenericTypeArguments.Select(a => a.Name))}>: " +
                    $"{string.Join(", ", candidates)}. A request type must have exactly one handler.");
            }

            services.TryAddScoped(service, handlersForService.First());
        }

        return services;
    }
}
