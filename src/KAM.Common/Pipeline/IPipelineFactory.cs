namespace KAM.Common.Pipeline;

/// <summary>
/// Creates pipeline builders. Inject this into a handler to compose a pipeline for a specific
/// use case: <c>factory.CreateBuilder&lt;MyContext&gt;().Use&lt;StepA&gt;().Use&lt;StepB&gt;().Build()</c>.
/// </summary>
public interface IPipelineFactory
{
    IPipelineBuilder<TContext> CreateBuilder<TContext>();
}
