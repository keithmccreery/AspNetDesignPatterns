using AspNetDesignPatterns.Api.DependencyInjection;
using AspNetDesignPatterns.Api.Shared.Handlers;
using AspNetDesignPatterns.Api.Shared.Results;

using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace AspNetDesignPatterns.Api.Tests.DependencyInjection;

/// <summary>
/// Focused unit tests for the reflection-registration extensions, using types declared in
/// this test assembly so the assertions don't depend on the app's feature set. The
/// end-to-end wiring against the real app is covered by <c>ReflectionRegistrationTests</c>.
/// </summary>
[TestFixture]
public class DependencyInjectionExtensionsTests
{
    // --- fixtures ---
    public sealed class EndpointOne : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
        }
    }

    public sealed class EndpointTwo : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
        }
    }

    public sealed class SampleDependency : IDependency
    {
        public void RegisterServices(IServiceCollection services) => services.AddSingleton(new Marker());
    }

    public sealed class Marker;

    public sealed record Ping(int N);

    public sealed class PingHandler : IRequestHandler<Ping, int>
    {
        public Task<Result<int>> HandleAsync(Ping request, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success(request.N));
    }

    private static readonly System.Reflection.Assembly ThisAssembly = typeof(DependencyInjectionExtensionsTests).Assembly;

    [Test]
    public void AddEndpoints_registers_every_IEndpoint_as_transient_without_duplicates()
    {
        ServiceCollection services = new();

        services.AddEndpoints(ThisAssembly);
        services.AddEndpoints(ThisAssembly); // idempotent: TryAddEnumerable

        var endpoints = services.BuildServiceProvider().GetServices<IEndpoint>().ToList();

        using (new AssertionScope())
        {
            endpoints.Should().Contain(e => e is EndpointOne).And.Contain(e => e is EndpointTwo);
            endpoints.Count(e => e is EndpointOne).Should().Be(1);
            services.First(d => d.ImplementationType == typeof(EndpointOne)).Lifetime
                .Should().Be(ServiceLifetime.Transient);
        }
    }

    [Test]
    public void AddDependencies_invokes_each_IDependency_module()
    {
        ServiceCollection services = new();

        services.AddDependencies(ThisAssembly);

        services.BuildServiceProvider().GetService<Marker>().Should().NotBeNull();
    }

    [Test]
    public void AddRequestHandlers_registers_closed_handler_interfaces_as_scoped()
    {
        ServiceCollection services = new();

        services.AddRequestHandlers(ThisAssembly);

        using (new AssertionScope())
        {
            var handler = services.BuildServiceProvider().GetService<IRequestHandler<Ping, int>>();
            handler.Should().BeOfType<PingHandler>();
            services.First(d => d.ServiceType == typeof(IRequestHandler<Ping, int>)).Lifetime
                .Should().Be(ServiceLifetime.Scoped);
        }
    }
}
