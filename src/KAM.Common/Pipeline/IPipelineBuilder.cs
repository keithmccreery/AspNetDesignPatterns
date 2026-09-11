namespace KAM.Common.Pipeline;

/// <summary>Fluent builder for composing a <see cref="Pipeline{TContext}"/>.</summary>
public interface IPipelineBuilder<TContext>
{
    /// <summary>Adds <typeparamref name="TStep"/> to the end of the chain.</summary>
    IPipelineBuilder<TContext> Use<TStep>() where TStep : class, IPipelineStep<TContext>;

    /// <summary>Returns the configured pipeline, ready to execute.</summary>
    Pipeline<TContext> Build();
}
