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
        services.AddAppTelemetry("test-service", new TelemetrySettings { Enabled = true });
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
        services.AddAppTelemetry("test-service", new TelemetrySettings { Enabled = false });
        using ServiceProvider provider = services.BuildServiceProvider();

        // Assert
        using (new AssertionScope())
        {
            provider.GetService<TracerProvider>().Should().BeNull();
            provider.GetService<MeterProvider>().Should().BeNull();
        }
    }
}
