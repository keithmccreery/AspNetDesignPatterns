namespace KAM.Common.Pipeline;

/// <inheritdoc cref="IPipelineBuilder{TContext}" />
public sealed class PipelineBuilder<TContext> : IPipelineBuilder<TContext>
{
    private readonly IServiceProvider _serviceProvider;
    private readonly List<Type> _stepTypes = [];

    public PipelineBuilder(IServiceProvider serviceProvider) =>
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

    public IPipelineBuilder<TContext> Use<TStep>()
        where TStep : class, IPipelineStep<TContext>
    {
        _stepTypes.Add(typeof(TStep));
        return this;
    }

    /// <summary>
    /// Returns a pipeline over a <em>snapshot</em> of the steps added so far: calling
    /// <see cref="Use{TStep}"/> afterwards affects only pipelines built later, never this one.
    /// </summary>
    public Pipeline<TContext> Build() => new(_serviceProvider, [.. _stepTypes]);
}
