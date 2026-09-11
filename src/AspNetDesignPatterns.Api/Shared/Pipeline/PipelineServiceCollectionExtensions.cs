using System.Reflection;

using AspNetDesignPatterns.Api.Shared.Pipeline;

using Microsoft.Extensions.DependencyInjection.Extensions;

#pragma warning disable IDE0130 // Namespace does not match folder structure (intentional: discoverable on IServiceCollection)
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130

public static class PipelineServiceCollectionExtensions
{
    /// <summary>
    /// Registers the <see cref="IPipelineFactory"/>. Scoped, so the pipeline resolves its
    /// steps from the active request scope (steps and their dependencies are typically scoped).
    /// </summary>
    public static IServiceCollection AddPipeline(this IServiceCollection services)
    {
        services.AddScoped<IPipelineFactory, PipelineFactory>();
        return services;
    }

    /// <summary>
    /// Registers every concrete <see cref="IPipelineStep{TContext}"/> in the given assemblies
    /// (scoped) so the pipeline can resolve them by type at execution time.
    /// </summary>
    public static IServiceCollection AddPipelineSteps(this IServiceCollection services, params Assembly[] assemblies)
    {
        if (assemblies.Length == 0)
        {
            assemblies = [Assembly.GetCallingAssembly()];
        }

        // Mirrors DependencyInjectionExtensions.ConcreteTypes: DefinedTypes over GetTypes(),
        // which throws ReflectionTypeLoadException if any single type in the assembly fails
        // to load, and TryAddScoped so a repeated scan (or a step registered by hand) doesn't
        // double-register.
        IEnumerable<TypeInfo> stepTypes = assemblies
            .SelectMany(assembly => assembly.DefinedTypes)
            .Where(type => type is { IsAbstract: false, IsInterface: false, IsGenericTypeDefinition: false }
                        && type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IPipelineStep<>)));

        foreach (TypeInfo stepType in stepTypes)
        {
            services.TryAddScoped(stepType);
        }

        return services;
    }
}
