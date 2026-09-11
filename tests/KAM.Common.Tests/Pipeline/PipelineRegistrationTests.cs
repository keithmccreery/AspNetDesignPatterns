using KAM.Common.Pipeline;
using KAM.Common.Results;

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
    public void Constructing_a_factory_without_a_provider_throws()
    {
        // Arrange
        Func<PipelineFactory> act = () => new PipelineFactory(null!);

        // Act & Assert
        act.Should().Throw<ArgumentNullException>();
    }
}
