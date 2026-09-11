using System.Reflection;

using KAM.Common.Pipeline;
using KAM.Common.Results;

using Microsoft.Extensions.DependencyInjection;

namespace KAM.Common.Tests.Pipeline;

[TestFixture]
public class PipelineTests
{
    private sealed class Ctx
    {
        public List<string> Visited { get; } = [];
    }

    private abstract class RecordingStep(string name, bool fail = false) : IPipelineStep<Ctx>
    {
        public async Task<Result> ExecuteAsync(
            Ctx context,
            Func<CancellationToken, Task<Result>> next,
            CancellationToken cancellationToken)
        {
            context.Visited.Add($"{name}:before");
            if (fail)
            {
                return Result.Failure(Error.Failure($"{name}.Failed", name));
            }

            Result result = await next(cancellationToken);
            context.Visited.Add($"{name}:after");
            return result;
        }
    }

    private sealed class StepA() : RecordingStep("A");
    private sealed class StepB() : RecordingStep("B");
    private sealed class StepC() : RecordingStep("C");
    private sealed class FailingStep() : RecordingStep("boom", fail: true);

    private static Pipeline<Ctx> Build(params Type[] steps)
    {
        ServiceCollection services = new();
        foreach (Type step in steps)
        {
            services.AddScoped(step);
        }

        Pipeline<Ctx> pipeline = new(services.BuildServiceProvider());
        foreach (Type step in steps)
        {
            typeof(Pipeline<Ctx>).GetMethod(nameof(Pipeline<Ctx>.Use), BindingFlags.NonPublic | BindingFlags.Instance)!
                .MakeGenericMethod(step)
                .Invoke(pipeline, null);
        }

        return pipeline;
    }

    [Test]
    public async Task Steps_wrap_each_other_in_registration_order()
    {
        // Arrange
        Ctx context = new();

        // Act
        Result result = await Build(typeof(StepA), typeof(StepB), typeof(StepC))
            .ExecuteAsync(context, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        context.Visited.Should().Equal(
            "A:before", "B:before", "C:before", "C:after", "B:after", "A:after");
    }

    [Test]
    public async Task An_empty_pipeline_succeeds()
    {
        // Arrange & Act
        Result result = await Build().ExecuteAsync(new Ctx(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Test]
    public async Task A_failure_stops_deeper_steps_but_outer_steps_still_unwind()
    {
        // Arrange
        Ctx context = new();

        // Act
        Result result = await Build(typeof(StepA), typeof(FailingStep), typeof(StepC))
            .ExecuteAsync(context, CancellationToken.None);

        // Assert
        using (new AssertionScope())
        {
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("boom.Failed");
            context.Visited.Should().NotContain("C:before");          // deeper step never entered
            context.Visited.Should().ContainInOrder("A:before", "boom:before", "A:after");
        }
    }

    [Test]
    public async Task A_cancelled_token_stops_the_pipeline_before_the_next_step()
    {
        // Arrange
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        // Act
        Func<Task<Result>> act = () => Build(typeof(StepA), typeof(StepB)).ExecuteAsync(new Ctx(), cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Test]
    public void Constructing_a_pipeline_without_a_provider_throws()
    {
        // Arrange
        Func<Pipeline<Ctx>> act = () => new Pipeline<Ctx>(null!);

        // Act & Assert
        act.Should().Throw<ArgumentNullException>();
    }
}
