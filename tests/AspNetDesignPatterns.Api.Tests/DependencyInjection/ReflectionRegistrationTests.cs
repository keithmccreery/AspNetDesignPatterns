using AspNetDesignPatterns.Api.DependencyInjection;
using AspNetDesignPatterns.Api.Features.Weather;
using AspNetDesignPatterns.Api.Shared.Handlers;
using AspNetDesignPatterns.Api.Shared.Pipeline;
using AspNetDesignPatterns.Api.Tests.Integration;

using FluentValidation;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AspNetDesignPatterns.Api.Tests.DependencyInjection;

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
        using IServiceScope scope = Scope();

        List<IEndpoint> endpoints = scope.ServiceProvider.GetServices<IEndpoint>().ToList();

        endpoints.Should().Contain(e => e is GetForecastEndpoint);
    }

    [Test]
    public void Request_handlers_are_discovered_and_resolvable()
    {
        using IServiceScope scope = Scope();

        IRequestHandler<GetForecastRequest, ForecastResponse>? handler = scope.ServiceProvider
            .GetService<IRequestHandler<GetForecastRequest, ForecastResponse>>();

        handler.Should().BeOfType<GetForecastHandler>();
    }

    [Test]
    public void Pipeline_factory_and_steps_are_discovered()
    {
        using IServiceScope scope = Scope();

        scope.ServiceProvider.GetService<IPipelineFactory>().Should().NotBeNull();
        scope.ServiceProvider.GetService<FetchForecastStep>().Should().NotBeNull();
        scope.ServiceProvider.GetService<ClampForecastWindowStep>().Should().NotBeNull();
        scope.ServiceProvider.GetService<RoundTemperaturesStep>().Should().NotBeNull();
    }

    [Test]
    public void Validators_are_discovered()
    {
        using IServiceScope scope = Scope();

        IValidator<GetForecastRequest>? validator = scope.ServiceProvider.GetService<IValidator<GetForecastRequest>>();

        validator.Should().BeOfType<GetForecastRequestValidator>();
    }

    [Test]
    public void Settings_are_bound_and_available()
    {
        using IServiceScope scope = Scope();

        IOptions<WeatherOptions> options = scope.ServiceProvider
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<WeatherOptions>>();

        options.Value.MaxForecastDays.Should().BeGreaterThan(0);
    }
}
