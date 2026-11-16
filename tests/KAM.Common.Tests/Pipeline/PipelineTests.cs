using System.Diagnostics;
using System.Reflection;

using KAM.Common.Pipeline;
using KAM.Common.Results;
using KAM.Common.Telemetry;

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

    // Calls next twice — the shape of a retry step.
    private sealed class TwiceStep : IPipelineStep<Ctx>
    {
        public async Task<Result> ExecuteAsync(Ctx context, Func<CancellationToken, Task<Result>> next, CancellationToken cancellationToken)
        {
            await next(cancellationToken);
            return await next(cancellationToken);
        }
    }

    private sealed class ThrowingStep : IPipelineStep<Ctx>
    {
        public Task<Result> ExecuteAsync(Ctx context, Func<CancellationToken, Task<Result>> next, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("kaboom");
    }

    private static IPipelineBuilder<Ctx> Builder(params Type[] steps)
    {
        ServiceCollection services = new();
        services.AddScoped<StepA>().AddScoped<StepB>().AddScoped<StepC>()
            .AddScoped<FailingStep>().AddScoped<TwiceStep>().AddScoped<ThrowingStep>();

        IPipelineBuilder<Ctx> builder = new PipelineBuilder<Ctx>(services.BuildServiceProvider());
        MethodInfo use = typeof(IPipelineBuilder<Ctx>).GetMethod(nameof(IPipelineBuilder<Ctx>.Use))!;
        foreach (Type step in steps)
        {
            use.MakeGenericMethod(step).Invoke(builder, null);
        }

        return builder;
    }

    private static Pipeline<Ctx> Build(params Type[] steps) => Builder(steps).Build();

    /// <summary>Records every finished pipeline span. Reads the source name first — see ActivitySources's remarks.</summary>
    private static (ActivityListener Listener, List<Activity> Spans) ListenToPipelineSpans()
    {
        string sourceName = ActivitySources.Pipeline.Name;
        List<Activity> spans = [];
        ActivityListener listener = new()
        {
            ShouldListenTo = source => source.Name == sourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = spans.Add,
        };
        ActivitySource.AddActivityListener(listener);
        return (listener, spans);
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
    public void Constructing_a_builder_without_a_provider_throws()
    {
        // Arrange
        Func<PipelineBuilder<Ctx>> act = () => new PipelineBuilder<Ctx>(null!);

        // Act & Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Test]
    public async Task A_built_pipeline_is_not_changed_by_later_Use_calls_on_its_builder()
    {
        // Arrange
        IPipelineBuilder<Ctx> builder = Builder(typeof(StepA));
        Pipeline<Ctx> built = builder.Build();
        builder.Use<StepB>();
        Ctx context = new();

        // Act
        await built.ExecuteAsync(context, CancellationToken.None);

        // Assert
        context.Visited.Should().Equal("A:before", "A:after");
    }

    [Test]
    public async Task Calling_next_twice_runs_the_rest_of_the_chain_twice_rather_than_skipping_steps()
    {
        // Arrange
        Ctx context = new();

        // Act
        await Build(typeof(TwiceStep), typeof(StepB), typeof(StepC)).ExecuteAsync(context, CancellationToken.None);

        // Assert
        context.Visited.Should().Equal(
            "B:before", "C:before", "C:after", "B:after",
            "B:before", "C:before", "C:after", "B:after");
    }

    [Test]
    public async Task A_failure_marks_only_the_span_of_the_step_it_originated_in()
    {
        // Arrange
        (ActivityListener listener, List<Activity> spans) = ListenToPipelineSpans();
        using ActivityListener _ = listener;

        // Act — A passes boom's failure along unchanged.
        await Build(typeof(StepA), typeof(FailingStep)).ExecuteAsync(new Ctx(), CancellationToken.None);

        // Assert
        using (new AssertionScope())
        {
            Activity failing = spans.Single(s => s.OperationName == nameof(FailingStep));
            failing.Status.Should().Be(ActivityStatusCode.Error);
            failing.GetTagItem("error.type").Should().Be("boom.Failed");
            spans.Single(s => s.OperationName == nameof(StepA)).Status.Should().Be(ActivityStatusCode.Unset);
        }
    }

    [Test]
    public async Task An_exception_is_recorded_once_on_the_span_of_the_step_that_threw()
    {
        // Arrange
        (ActivityListener listener, List<Activity> spans) = ListenToPipelineSpans();
        using ActivityListener _ = listener;

        // Act
        Func<Task<Result>> act = () => Build(typeof(StepA), typeof(ThrowingStep)).ExecuteAsync(new Ctx(), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("kaboom");
        using (new AssertionScope())
        {
            Activity thrower = spans.Single(s => s.OperationName == nameof(ThrowingStep));
            thrower.Status.Should().Be(ActivityStatusCode.Error);
            thrower.Events.Should().ContainSingle(e => e.Name == "exception");
            Activity outer = spans.Single(s => s.OperationName == nameof(StepA));
            outer.Status.Should().Be(ActivityStatusCode.Unset);
            outer.Events.Should().BeEmpty();
        }
    }
}
