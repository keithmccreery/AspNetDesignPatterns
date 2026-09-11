using KAM.Common.DependencyInjection;

using Microsoft.Extensions.DependencyInjection;

namespace KAM.Common.Tests.DependencyInjection.Endpoints;

/// <summary>
/// Focused unit tests using types declared in this test assembly, so the assertions don't
/// depend on any consuming app's feature set. End-to-end wiring against the real app is
/// covered by <c>ReflectionRegistrationTests</c> in the Api test project.
/// </summary>
[TestFixture]
public class EndpointRegistrationTests
{
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

    private static readonly System.Reflection.Assembly ThisAssembly = typeof(EndpointRegistrationTests).Assembly;

    [Test]
    public void AddEndpoints_registers_every_IEndpoint_as_transient_without_duplicates()
    {
        // Arrange
        ServiceCollection services = new();

        // Act
        services.AddEndpoints(ThisAssembly);
        services.AddEndpoints(ThisAssembly); // idempotent: TryAddEnumerable

        // Assert
        List<IEndpoint> endpoints = services.BuildServiceProvider().GetServices<IEndpoint>().ToList();

        using (new AssertionScope())
        {
            endpoints.Should().Contain(e => e is EndpointOne).And.Contain(e => e is EndpointTwo);
            endpoints.Count(e => e is EndpointOne).Should().Be(1);
            services.First(d => d.ImplementationType == typeof(EndpointOne)).Lifetime
                .Should().Be(ServiceLifetime.Transient);
        }
    }

    [Test]
    public void AddEndpoints_with_no_assemblies_falls_back_to_the_calling_assembly()
    {
        // Arrange — see DependencyRegistrationTests's equivalent test for why this matters.
        ServiceCollection services = new();

        // Act
        services.AddEndpoints();

        // Assert
        services.BuildServiceProvider().GetServices<IEndpoint>()
            .Should().Contain(e => e is EndpointOne).And.Contain(e => e is EndpointTwo);
    }
}
