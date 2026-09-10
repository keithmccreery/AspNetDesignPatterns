using AspNetDesignPatterns.Api.Shared.Pipeline;
using AspNetDesignPatterns.Api.Shared.Results;

using Microsoft.Extensions.DependencyInjection;

namespace AspNetDesignPatterns.Api.Tests.Shared.Pipeline;

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
        using var provider = BuildProvider();

        provider.GetService<IPipelineFactory>().Should().BeOfType<PipelineFactory>();
    }

    [Test]
    public void AddPipelineSteps_discovers_every_concrete_step_by_reflection()
    {
        using var provider = BuildProvider();

        using (new AssertionScope())
        {
            provider.GetService<DoubleStep>().Should().NotBeNull();
            provider.GetService<AddOneStep>().Should().NotBeNull();
        }
    }

    [Test]
    public async Task Builder_composes_a_runnable_pipeline_in_declared_order()
    {
        using var provider = BuildProvider();
        var factory = provider.GetRequiredService<IPipelineFactory>();

        var pipeline = factory.CreateBuilder<Ctx>()
            .Use<AddOneStep>()   // 5 -> 6
            .Use<DoubleStep>()   // 6 -> 12
            .Build();

        var context = new Ctx { Value = 5 };
        var result = await pipeline.ExecuteAsync(context, CancellationToken.None);

        using (new AssertionScope())
        {
            result.IsSuccess.Should().BeTrue();
            context.Value.Should().Be(12);
        }
    }

    [Test]
    public void Constructing_a_factory_without_a_provider_throws()
    {
        var act = () => new PipelineFactory(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
