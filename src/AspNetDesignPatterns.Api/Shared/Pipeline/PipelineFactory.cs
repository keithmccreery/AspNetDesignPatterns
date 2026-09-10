namespace AspNetDesignPatterns.Api.Shared.Pipeline;

/// <inheritdoc cref="IPipelineFactory" />
public sealed class PipelineFactory(IServiceProvider serviceProvider) : IPipelineFactory
{
    private readonly IServiceProvider _serviceProvider =
        serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

    public IPipelineBuilder<TContext> CreateBuilder<TContext>() =>
        new PipelineBuilder<TContext>(_serviceProvider);
}
