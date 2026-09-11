using AspNetDesignPatterns.Api.DependencyInjection;

using Microsoft.Extensions.DependencyInjection;

namespace AspNetDesignPatterns.Api.Shared.Tests.DependencyInjection.Dependencies;

[TestFixture]
public class DependencyRegistrationTests
{
    public sealed class SampleDependency : IDependency
    {
        public void RegisterServices(IServiceCollection services) => services.AddSingleton(new Marker());
    }

    public sealed class Marker;

    private static readonly System.Reflection.Assembly ThisAssembly = typeof(DependencyRegistrationTests).Assembly;

    [Test]
    public void AddDependencies_invokes_each_IDependency_module()
    {
        // Arrange
        ServiceCollection services = new();

        // Act
        services.AddDependencies(ThisAssembly);

        // Assert
        services.BuildServiceProvider().GetService<Marker>().Should().NotBeNull();
    }
}
