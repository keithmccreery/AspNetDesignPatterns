using System.Reflection;

using KAM.Common.DependencyInjection;

#pragma warning disable IDE0130 // Namespace does not match folder structure (intentional: discoverable on IServiceCollection)
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130

/// <summary>Discovers every <see cref="ISettings"/> class by reflection and lets it bind + validate itself.</summary>
public static class SettingsRegistration
{
    /// <summary>
    /// Discovers every <see cref="ISettings"/> class in <paramref name="assemblies"/> and lets
    /// it bind + validate itself. Defaults to the calling assembly when none are given — pass
    /// every assembly a feature or shared library can define one in (see <c>Program.cs</c>).
    /// </summary>
    public static IServiceCollection AddSettings(this IServiceCollection services, params Assembly[] assemblies)
    {
        if (assemblies.Length == 0)
        {
            assemblies = [Assembly.GetCallingAssembly()];
        }

        IEnumerable<TypeInfo> settingsTypes = assemblies
            .SelectMany(assembly => assembly.ConcreteTypes())
            .Where(t => t.IsAssignableTo(typeof(ISettings)));

        foreach (TypeInfo type in settingsTypes)
        {
            if (Activator.CreateInstance(type) is not ISettings settings)
            {
                throw new InvalidOperationException($"Could not create settings instance {type.FullName}.");
            }

            settings.RegisterSettings(services);
        }

        return services;
    }
}
