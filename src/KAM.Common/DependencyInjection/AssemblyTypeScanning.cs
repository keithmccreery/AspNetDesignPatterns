using System.Reflection;

namespace KAM.Common.DependencyInjection;

/// <summary>
/// The one "find concrete types" step every reflection-based registration extension builds on
/// — the four in this folder (<c>EndpointRegistration</c>, <c>SettingsRegistration</c>,
/// <c>DependencyRegistration</c>, <c>RequestHandlerRegistration</c>) plus
/// <c>PipelineServiceCollectionExtensions.AddPipelineSteps</c>.
/// </summary>
internal static class AssemblyTypeScanning
{
    /// <summary>Every non-abstract, non-interface, non-open-generic type in <paramref name="assembly"/>.</summary>
    public static IEnumerable<TypeInfo> ConcreteTypes(this Assembly assembly) =>
        assembly.DefinedTypes.Where(t =>
            t is { IsAbstract: false, IsInterface: false, IsGenericTypeDefinition: false });
}
