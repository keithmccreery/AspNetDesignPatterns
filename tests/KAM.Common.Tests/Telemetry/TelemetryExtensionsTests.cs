using KAM.Common.Telemetry;

using Microsoft.Extensions.DependencyInjection;

using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace KAM.Common.Tests.Telemetry;

[TestFixture]
public class TelemetryExtensionsTests
{
    [Test]
    public void AddAppTelemetry_registers_a_TracerProvider_and_MeterProvider_when_enabled()
    {
        // Arrange
        ServiceCollection services = new();

        // Act
        services.AddAppTelemetry(new TelemetrySettings { Enabled = true });
        using ServiceProvider provider = services.BuildServiceProvider();

        // Assert
        using (new AssertionScope())
        {
            provider.GetService<TracerProvider>().Should().NotBeNull();
            provider.GetService<MeterProvider>().Should().NotBeNull();
        }
    }

    [Test]
    public void AddAppTelemetry_registers_nothing_when_disabled()
    {
        // Arrange — the no-collector-running case still has to boot cleanly; this proves the
        // "off" switch skips OpenTelemetry registration entirely rather than registering an
        // exporter that will just fail quietly.
        ServiceCollection services = new();

        // Act
        services.AddAppTelemetry(new TelemetrySettings { Enabled = false });
        using ServiceProvider provider = services.BuildServiceProvider();

        // Assert
        using (new AssertionScope())
        {
            provider.GetService<TracerProvider>().Should().BeNull();
            provider.GetService<MeterProvider>().Should().BeNull();
        }
    }

    [Test]
    public void ResolveServiceName_prefers_the_configured_value_over_the_entry_assembly()
    {
        // Arrange & Act & Assert
        TelemetryExtensions.ResolveServiceName(new TelemetrySettings { ServiceName = "custom-service" })
            .Should().Be("custom-service");
    }

    [Test]
    public void ResolveServiceName_falls_back_to_the_entry_assembly_when_unconfigured()
    {
        // Arrange & Act
        string resolved = TelemetryExtensions.ResolveServiceName(new TelemetrySettings());

        // Assert — under the test runner, the entry assembly is the test host, not KAM.Common;
        // the point is that *some* non-empty name is derived, not a specific one.
        resolved.Should().NotBeNullOrEmpty();
    }

    [Test]
    public void ResolveServiceVersion_prefers_the_configured_value_over_the_entry_assembly()
    {
        // Arrange & Act & Assert
        TelemetryExtensions.ResolveServiceVersion(new TelemetrySettings { ServiceVersion = "9.9.9" })
            .Should().Be("9.9.9");
    }
}
