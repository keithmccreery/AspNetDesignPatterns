using AspNetDesignPatterns.Api.Shared.Configuration;
using AspNetDesignPatterns.Api.Tests.TestSupport;

namespace AspNetDesignPatterns.Api.Tests.Shared.Configuration;

[TestFixture]
public class AppEnvironmentTests
{
    [Test]
    public void Reflects_the_hosting_environment_name()
    {
        // Arrange & Act
        AppEnvironment env = new(new FakeHostEnvironment("Development"));

        // Assert
        using (new AssertionScope())
        {
            env.EnvironmentName.Should().Be("Development");
            env.IsDevelopment.Should().BeTrue();
            env.IsProduction.Should().BeFalse();
        }
    }

    [Test]
    public void Recognises_production()
    {
        // Arrange & Act
        AppEnvironment env = new(new FakeHostEnvironment("Production"));

        // Assert
        using (new AssertionScope())
        {
            env.IsProduction.Should().BeTrue();
            env.IsDevelopment.Should().BeFalse();
        }
    }

    [Test]
    public void IsContainerized_reads_the_dotnet_runtime_flag()
    {
        // Arrange & Act & Assert
        using (new ManageEnvironmentVariables(new Dictionary<string, string?> { ["DOTNET_RUNNING_IN_CONTAINER"] = "true" }))
        {
            new AppEnvironment(new FakeHostEnvironment("Production")).IsContainerized.Should().BeTrue();
        }

        using (new ManageEnvironmentVariables(new Dictionary<string, string?> { ["DOTNET_RUNNING_IN_CONTAINER"] = null }))
        {
            new AppEnvironment(new FakeHostEnvironment("Production")).IsContainerized.Should().BeFalse();
        }
    }

    [Test]
    public void Rejects_a_null_host_environment()
    {
        // Arrange
        Func<AppEnvironment> act = () => new AppEnvironment(null!);

        // Act & Assert
        act.Should().Throw<ArgumentNullException>();
    }
}
