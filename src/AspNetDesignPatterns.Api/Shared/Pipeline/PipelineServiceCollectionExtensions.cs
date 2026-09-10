using System.Reflection;

using AspNetDesignPatterns.Api.Shared.Pipeline;

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

        IEnumerable<Type> stepTypes = assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => t is { IsClass: true, IsAbstract: false }
                        && t.GetInterfaces().Any(i => i.IsGenericType
                            && i.GetGenericTypeDefinition() == typeof(IPipelineStep<>)));

        foreach (Type stepType in stepTypes)
        {
            services.AddScoped(stepType);
        }

        return services;
    }
}
