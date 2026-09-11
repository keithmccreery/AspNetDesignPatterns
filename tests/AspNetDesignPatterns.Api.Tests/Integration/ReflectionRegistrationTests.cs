using AspNetDesignPatterns.Api.DependencyInjection;
using AspNetDesignPatterns.Api.Features.Weather;
using AspNetDesignPatterns.Api.Shared.Handlers;
using AspNetDesignPatterns.Api.Shared.Pipeline;

using FluentValidation;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AspNetDesignPatterns.Api.Tests.Integration;

/// <summary>
/// Proves the convention-based startup wiring actually finds and registers the feature
/// types by reflection — no manual <c>services.Add…</c> per feature.
/// </summary>
[TestFixture]
public class ReflectionRegistrationTests
{
    private static IServiceScope Scope() => GlobalTestSetup.Factory.Services.CreateScope();

    [Test]
    public void Endpoints_are_discovered()
    {
        // Arrange
        using IServiceScope scope = Scope();

        // Act
        List<IEndpoint> endpoints = scope.ServiceProvider.GetServices<IEndpoint>().ToList();

        // Assert
        endpoints.Should().Contain(e => e is GetForecastEndpoint);
    }

    [Test]
    public void Request_handlers_are_discovered_and_resolvable()
    {
        // Arrange
        using IServiceScope scope = Scope();

        // Act
        IRequestHandler<GetForecastRequest, ForecastResponse>? handler = scope.ServiceProvider
            .GetService<IRequestHandler<GetForecastRequest, ForecastResponse>>();

        // Assert
        handler.Should().BeOfType<GetForecastHandler>();
    }

    [Test]
    public void Pipeline_factory_and_steps_are_discovered()
    {
        // Arrange
        using IServiceScope scope = Scope();

        // Act & Assert
        scope.ServiceProvider.GetService<IPipelineFactory>().Should().NotBeNull();
        scope.ServiceProvider.GetService<FetchForecastStep>().Should().NotBeNull();
        scope.ServiceProvider.GetService<ClampForecastWindowStep>().Should().NotBeNull();
        scope.ServiceProvider.GetService<RoundTemperaturesStep>().Should().NotBeNull();
    }

    [Test]
    public void Validators_are_discovered()
    {
        // Arrange
        using IServiceScope scope = Scope();

        // Act
        IValidator<GetForecastRequest>? validator = scope.ServiceProvider.GetService<IValidator<GetForecastRequest>>();

        // Assert
        validator.Should().BeOfType<GetForecastRequestValidator>();
    }

    [Test]
    public void Settings_are_bound_and_available()
    {
        // Arrange
        using IServiceScope scope = Scope();

        // Act
        IOptions<WeatherOptions> options = scope.ServiceProvider
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<WeatherOptions>>();

        // Assert
        options.Value.MaxForecastDays.Should().BeGreaterThan(0);
    }
}
