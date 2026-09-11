namespace AspNetDesignPatterns.Api.Shared.Pipeline;

/// <summary>
/// One step in a sequential pipeline that operates on a shared, mutable
/// <typeparamref name="TContext"/>. Steps are middleware: a step may inspect/mutate the
/// context, decide whether to call the next delegate (short-circuiting the rest of the chain
/// when it does not), and run code <em>after</em> it returns (post-processing, timing, catching).
/// </summary>
public interface IPipelineStep<in TContext>
{
    /// <summary>Runs this step.</summary>
    /// <param name="context">The state threaded through the pipeline.</param>
    /// <param name="next">Invokes the remainder of the pipeline. Not calling it stops the chain.</param>
    /// <param name="cancellationToken">Checked before each step runs.</param>
    Task<Result> ExecuteAsync(
        TContext context,
        Func<CancellationToken, Task<Result>> next,
        CancellationToken cancellationToken = default);
}
