using AspNetDesignPatterns.Api.Features.Library;
using AspNetDesignPatterns.Api.Features.Weather;

using FluentValidation;

using KAM.Common.Data;
using KAM.Common.DependencyInjection;
using KAM.Common.Handlers;
using KAM.Common.Pagination;
using KAM.Common.Pipeline;

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
    public void Pipeline_factory_steps_and_service_are_registered()
    {
        // Arrange
        using IServiceScope scope = Scope();

        // Act & Assert
        scope.ServiceProvider.GetService<IPipelineFactory>().Should().NotBeNull();
        scope.ServiceProvider.GetService<IWeatherService>().Should().BeOfType<WeatherService>();
        scope.ServiceProvider.GetService<FetchForecastStep>().Should().NotBeNull();
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

    [Test]
    public void The_second_slice_is_wired_the_same_way()
    {
        // Arrange
        using IServiceScope scope = Scope();
        IServiceProvider services = scope.ServiceProvider;

        // Act & Assert — nothing about Library was added to Program.cs
        services.GetServices<IEndpoint>().Should().Contain(e => e is BorrowBookEndpoint);
        services.GetService<IRequestHandler<BorrowBookRequest, LoanResponse>>().Should().BeOfType<BorrowBookHandler>();
        services.GetService<IRequestHandler<ListBooksRequest, CursorPage<BookResponse>>>().Should().BeOfType<ListBooksHandler>();
        services.GetService<IValidator<ListBooksRequest>>().Should().BeOfType<ListBooksRequestValidator>();
        services.GetService<IUnitOfWork>().Should().NotBeNull();
        services.GetService<ILibraryService>().Should().BeOfType<LibraryService>();
        services.GetService<ILibraryRepository>().Should().BeOfType<LibraryRepository>();
        services.GetRequiredService<IOptions<LibraryOptions>>().Value.MaxPageSize.Should().BeGreaterThan(0);
    }
}
