using KAM.Common.DependencyInjection;

using Microsoft.Extensions.DependencyInjection;

namespace KAM.Common.Tests.DependencyInjection.Dependencies;

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

    [Test]
    public void AddDependencies_with_no_assemblies_falls_back_to_the_calling_assembly()
    {
        // Arrange — the zero-arg default (Assembly.GetCallingAssembly()) is what silently
        // stopped finding types once a real app split across two assemblies (see
        // Program.cs's appAssemblies comment); this proves the fallback itself still works
        // for the single-assembly case it's meant for.
        ServiceCollection services = new();

        // Act
        services.AddDependencies();

        // Assert
        services.BuildServiceProvider().GetService<Marker>().Should().NotBeNull();
    }
}
