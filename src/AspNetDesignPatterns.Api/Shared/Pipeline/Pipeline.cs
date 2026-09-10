
namespace AspNetDesignPatterns.Api.Shared.Pipeline;

/// <summary>
/// Executes an ordered chain of <see cref="IPipelineStep{TContext}"/> instances, resolving
/// each one from DI at execution time. Steps are composed as middleware (each receives a
/// <c>next</c> delegate); the chain stops at the first step that returns a failed
/// <see cref="Result"/> or does not call <c>next</c>.
/// </summary>
public sealed class Pipeline<TContext>(IServiceProvider serviceProvider)
{
    private readonly IServiceProvider _serviceProvider =
        serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

    private readonly List<Type> _stepTypes = [];

    /// <summary>Appends a step type to the chain. Resolved from DI when the pipeline runs.</summary>
    public Pipeline<TContext> Use<TStep>()
        where TStep : class, IPipelineStep<TContext>
    {
        _stepTypes.Add(typeof(TStep));
        return this;
    }

    /// <summary>Runs the chain. Returns the first failure, or success if every step calls through.</summary>
    public Task<Result> ExecuteAsync(TContext context, CancellationToken cancellationToken = default)
    {
        int index = 0;

        Task<Result> NextAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            if (index >= _stepTypes.Count)
            {
                return Task.FromResult(Result.Success());
            }

            Type stepType = _stepTypes[index++];
            IPipelineStep<TContext> step = (IPipelineStep<TContext>) _serviceProvider.GetRequiredService(stepType);
            return step.ExecuteAsync(context, NextAsync, token);
        }

        return NextAsync(cancellationToken);
    }
}
