using AspNetDesignPatterns.Api.Features.Weather;

using KAM.Common.Pipeline;
using KAM.Common.Results;

using Microsoft.Extensions.DependencyInjection;

using NSubstitute;
using NSubstitute.ClearExtensions;
using NSubstitute.ExceptionExtensions;

namespace AspNetDesignPatterns.Api.Tests.Features.Weather;

[TestFixture]
public class GetForecastHandlerTests
{
    private readonly IWeatherClient _client = Substitute.For<IWeatherClient>();

    [SetUp]
    public void Reset() => _client.ClearSubstitute();

    private GetForecastHandler CreateHandler()
    {
        ServiceCollection services = new();
        services.AddSingleton(_client);
        services.AddScoped<IWeatherService, WeatherService>();
        services.AddScoped<FetchForecastStep>();
        services.AddScoped<RoundTemperaturesStep>();
        services.AddSingleton<IPipelineFactory, PipelineFactory>();

        return new GetForecastHandler(services.BuildServiceProvider().GetRequiredService<IPipelineFactory>());
    }

    [Test]
    public async Task Runs_the_pipeline_and_rounds_temperatures()
    {
        // Arrange
        _client.GetForecastAsync(default, default, default, default).ReturnsForAnyArgs(new OpenMeteoForecast(
            1, 2, "UTC",
            new OpenMeteoDaily([new DateOnly(2026, 9, 9)], [20.148], [10.052], [0.0])));

        // Act
        Result<ForecastResponse> result = await CreateHandler().HandleAsync(new GetForecastRequest(1, 2, 1), CancellationToken.None);

        // Assert
        using (new AssertionScope())
        {
            result.IsSuccess.Should().BeTrue();
            result.Value.Days[0].TemperatureMaxC.Should().Be(20.1);
            result.Value.Days[0].TemperatureMinC.Should().Be(10.1);
        }
    }

    [Test]
    public async Task Propagates_a_service_failure_as_the_handler_result()
    {
        // Arrange
        _client.GetForecastAsync(default, default, default, default)
            .ThrowsAsyncForAnyArgs(new WeatherClientException("boom"));

        // Act
        Result<ForecastResponse> result = await CreateHandler().HandleAsync(new GetForecastRequest(1, 2, 1), CancellationToken.None);

        // Assert
        using (new AssertionScope())
        {
            result.IsFailure.Should().BeTrue();
            result.Error.Type.Should().Be(ErrorType.Upstream);
            result.Error.Code.Should().Be(WeatherErrors.ProviderUnavailable.Code);
        }
    }

    [Test]
    public async Task Returns_a_failure_rather_than_null_when_the_pipeline_produces_no_response()
    {
        // Arrange — the factory hands out a builder that already starts with SilentStep, which
        // succeeds without calling next or setting Response; the handler's own steps, appended
        // after it, are never reached (so never even resolved).
        ServiceCollection services = new();
        services.AddScoped<SilentStep>();
        ServiceProvider provider = services.BuildServiceProvider();
        IPipelineFactory factory = Substitute.For<IPipelineFactory>();
        factory.CreateBuilder<ForecastContext>().Returns(_ => new PipelineBuilder<ForecastContext>(provider).Use<SilentStep>());

        // Act
        Result<ForecastResponse> result = await new GetForecastHandler(factory)
            .HandleAsync(new GetForecastRequest(1, 2, 1), CancellationToken.None);

        // Assert
        using (new AssertionScope())
        {
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be(WeatherErrors.NoResponse.Code);
            result.Error.Type.Should().Be(ErrorType.Failure);
        }
    }

    private sealed class SilentStep : IPipelineStep<ForecastContext>
    {
        public Task<Result> ExecuteAsync(ForecastContext context, Func<CancellationToken, Task<Result>> next, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success());
    }
}
