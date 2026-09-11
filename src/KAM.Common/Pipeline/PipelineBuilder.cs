namespace KAM.Common.Pipeline;

/// <inheritdoc cref="IPipelineBuilder{TContext}" />
public sealed class PipelineBuilder<TContext>(IServiceProvider serviceProvider) : IPipelineBuilder<TContext>
{
    private readonly Pipeline<TContext> _pipeline = new(serviceProvider);

    public IPipelineBuilder<TContext> Use<TStep>()
        where TStep : class, IPipelineStep<TContext>
    {
        _pipeline.Use<TStep>();
        return this;
    }

    public Pipeline<TContext> Build() => _pipeline;
}
