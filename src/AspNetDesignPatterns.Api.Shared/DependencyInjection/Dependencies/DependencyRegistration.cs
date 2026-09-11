using System.Reflection;

using AspNetDesignPatterns.Api.DependencyInjection;

#pragma warning disable IDE0130 // Namespace does not match folder structure (intentional: discoverable on IServiceCollection)
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130

/// <summary>Discovers every <see cref="IDependency"/> module by reflection and invokes it.</summary>
public static class DependencyRegistration
{
    /// <summary>
    /// Discovers every <see cref="IDependency"/> in <paramref name="assemblies"/> and invokes
    /// its <c>RegisterServices</c>. Defaults to the calling assembly when none are given — pass
    /// every assembly a feature or shared library can define one in (see <c>Program.cs</c>).
    /// </summary>
    public static IServiceCollection AddDependencies(this IServiceCollection services, params Assembly[] assemblies)
    {
        if (assemblies.Length == 0)
        {
            assemblies = [Assembly.GetCallingAssembly()];
        }

        IEnumerable<TypeInfo> dependencyTypes = assemblies
            .SelectMany(assembly => assembly.ConcreteTypes())
            .Where(t => t.IsAssignableTo(typeof(IDependency)));

        foreach (TypeInfo type in dependencyTypes)
        {
            if (Activator.CreateInstance(type) is not IDependency dependency)
            {
                throw new InvalidOperationException($"Could not create dependency module {type.FullName}.");
            }

            dependency.RegisterServices(services);
        }

        return services;
    }
}
