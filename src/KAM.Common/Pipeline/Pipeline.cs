using System.Diagnostics;

using KAM.Common.Telemetry;

namespace KAM.Common.Pipeline;

/// <summary>
/// Executes an ordered chain of <see cref="IPipelineStep{TContext}"/> instances, resolving
/// each one from DI at execution time. Steps are composed as middleware (each receives a
/// <c>next</c> delegate); the chain stops at the first step that returns a failed
/// <see cref="Result"/> or does not call <c>next</c>. Each step runs inside its own span (see
/// <see cref="ActivitySources.Pipeline"/>) — free when nothing's listening for it.
/// </summary>
/// <remarks>
/// <para>
/// Immutable: the step list is a snapshot taken by <see cref="IPipelineBuilder{TContext}.Build"/>,
/// so adding steps to the builder afterwards can't change a pipeline already handed out.
/// </para>
/// <para>
/// Each step's <c>next</c> is bound to that step's own position, so calling it twice runs the
/// rest of the chain twice (e.g. a retry step) instead of silently skipping steps.
/// </para>
/// <para>
/// A failed <see cref="Result"/> (or an exception) marks <em>only the span of the step it came
/// from</em> with <see cref="ActivityStatusCode.Error"/> and an <c>error.type</c> tag. Outer steps
/// that merely pass that same failure along keep an unset status, so a trace shows where the
/// failure originated rather than painting every enclosing span red.
/// </para>
/// </remarks>
public sealed class Pipeline<TContext>
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IReadOnlyList<Type> _stepTypes;

    internal Pipeline(IServiceProvider serviceProvider, IReadOnlyList<Type> stepTypes)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _stepTypes = stepTypes ?? throw new ArgumentNullException(nameof(stepTypes));
    }

    /// <summary>Runs the chain. Returns the first failure, or success if every step calls through.</summary>
    public Task<Result> ExecuteAsync(TContext context, CancellationToken cancellationToken = default) =>
        InvokeAsync(0, context, cancellationToken);

    private async Task<Result> InvokeAsync(int index, TContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (index >= _stepTypes.Count)
        {
            return Result.Success();
        }

        Type stepType = _stepTypes[index];
        IPipelineStep<TContext> step = (IPipelineStep<TContext>) _serviceProvider.GetRequiredService(stepType);

        using Activity? activity = ActivitySources.Pipeline.StartActivity(stepType.Name);

        // What the rest of the chain produced, so this step's span can tell a failure it
        // originated from one it is only passing along.
        Error? errorFromNext = null;
        bool nextThrew = false;

        async Task<Result> NextAsync(CancellationToken token)
        {
            try
            {
                Result inner = await InvokeAsync(index + 1, context, token);
                errorFromNext = inner.IsFailure ? inner.Error : null;
                return inner;
            }
            catch
            {
                nextThrew = true;
                throw;
            }
        }

        try
        {
            Result result = await step.ExecuteAsync(context, NextAsync, cancellationToken);

            if (result.IsFailure && !Equals(result.Error, errorFromNext))
            {
                activity?.SetStatus(ActivityStatusCode.Error, result.Error.Code);
                activity?.SetTag("error.type", result.Error.Code);
            }

            return result;
        }
        catch (Exception ex) when (RecordException(activity, ex, nextThrew))
        {
            // Unreachable: the filter always returns false, so the exception propagates
            // untouched (no catch/rethrow) after being recorded on this step's span.
            throw;
        }
    }

    private static bool RecordException(Activity? activity, Exception exception, bool thrownDeeper)
    {
        // Cancellation isn't a failure of this step, and an exception from deeper in the chain
        // was already recorded on the span of the step that threw it.
        if (activity is not null && !thrownDeeper && exception is not OperationCanceledException)
        {
            activity.AddException(exception);
            activity.SetStatus(ActivityStatusCode.Error, exception.Message);
            activity.SetTag("error.type", exception.GetType().FullName);
        }

        return false;
    }
}
