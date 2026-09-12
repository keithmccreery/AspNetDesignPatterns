using System.Diagnostics;

using KAM.Common.Pipeline;
using KAM.Common.Results;
using KAM.Common.Telemetry;

using Microsoft.Extensions.DependencyInjection;

namespace KAM.Common.Tests.Pipeline;

[TestFixture]
public class PipelineFactoryTests
{
    private sealed class Ctx
    {
        public int Value { get; set; }
    }

    private sealed class DoubleStep : IPipelineStep<Ctx>
    {
        public Task<Result> ExecuteAsync(Ctx context, Func<CancellationToken, Task<Result>> next, CancellationToken cancellationToken)
        {
            context.Value *= 2;
            return next(cancellationToken);
        }
    }

    private sealed class AddOneStep : IPipelineStep<Ctx>
    {
        public Task<Result> ExecuteAsync(Ctx context, Func<CancellationToken, Task<Result>> next, CancellationToken cancellationToken)
        {
            context.Value += 1;
            return next(cancellationToken);
        }
    }

    private static ServiceProvider BuildProvider()
    {
        ServiceCollection services = new();
        services.AddPipeline();
        services.AddPipelineSteps(typeof(PipelineFactoryTests).Assembly);
        return services.BuildServiceProvider();
    }

    [Test]
    public void AddPipeline_registers_the_factory()
    {
        // Arrange
        using ServiceProvider provider = BuildProvider();

        // Act & Assert
        provider.GetService<IPipelineFactory>().Should().BeOfType<PipelineFactory>();
    }

    [Test]
    public void AddPipelineSteps_discovers_every_concrete_step_by_reflection()
    {
        // Arrange
        using ServiceProvider provider = BuildProvider();

        // Act & Assert
        using (new AssertionScope())
        {
            provider.GetService<DoubleStep>().Should().NotBeNull();
            provider.GetService<AddOneStep>().Should().NotBeNull();
        }
    }

    [Test]
    public async Task Builder_composes_a_runnable_pipeline_in_declared_order()
    {
        // Arrange
        using ServiceProvider provider = BuildProvider();
        IPipelineFactory factory = provider.GetRequiredService<IPipelineFactory>();

        Pipeline<Ctx> pipeline = factory.CreateBuilder<Ctx>()
            .Use<AddOneStep>()   // 5 -> 6
            .Use<DoubleStep>()   // 6 -> 12
            .Build();

        Ctx context = new() { Value = 5 };

        // Act
        Result result = await pipeline.ExecuteAsync(context, CancellationToken.None);

        // Assert
        using (new AssertionScope())
        {
            result.IsSuccess.Should().BeTrue();
            context.Value.Should().Be(12);
        }
    }

    [Test]
    public async Task Each_step_runs_inside_its_own_span_when_something_is_listening()
    {
        // Arrange — no listener is registered in any other test in this file, so those prove
        // the free/no-op path (see ActivitySources's own remarks); this proves the span itself.
        // Reading the name *before* registering the listener matters: ActivitySource notifies
        // every registered listener synchronously from its own constructor, so a listener whose
        // predicate reads ActivitySources.Pipeline (instead of a captured local) risks re-entering
        // that static field before the assignment which is constructing it completes.
        string pipelineSourceName = ActivitySources.Pipeline.Name;
        List<string?> spanNames = [];
        using ActivityListener listener = new()
        {
            ShouldListenTo = source => source.Name == pipelineSourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStarted = activity => spanNames.Add(activity.OperationName),
        };
        ActivitySource.AddActivityListener(listener);

        using ServiceProvider provider = BuildProvider();
        IPipelineFactory factory = provider.GetRequiredService<IPipelineFactory>();
        Pipeline<Ctx> pipeline = factory.CreateBuilder<Ctx>().Use<AddOneStep>().Use<DoubleStep>().Build();

        // Act
        await pipeline.ExecuteAsync(new Ctx { Value = 1 }, CancellationToken.None);

        // Assert
        spanNames.Should().Equal(nameof(AddOneStep), nameof(DoubleStep));
    }

    [Test]
    public void AddPipelineSteps_with_no_assemblies_falls_back_to_the_calling_assembly()
    {
        // Arrange — see DependencyRegistrationTests's equivalent test for why this matters.
        ServiceCollection services = new();
        services.AddPipeline();

        // Act
        services.AddPipelineSteps();

        // Assert
        using ServiceProvider provider = services.BuildServiceProvider();
        using (new AssertionScope())
        {
            provider.GetService<DoubleStep>().Should().NotBeNull();
            provider.GetService<AddOneStep>().Should().NotBeNull();
        }
    }

    [Test]
    public void Constructing_a_factory_without_a_provider_throws()
    {
        // Arrange
        Func<PipelineFactory> act = () => new PipelineFactory(null!);

        // Act & Assert
        act.Should().Throw<ArgumentNullException>();
    }
}
