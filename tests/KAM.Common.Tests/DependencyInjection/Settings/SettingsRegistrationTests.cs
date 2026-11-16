using KAM.Common.DependencyInjection;

using Microsoft.Extensions.DependencyInjection;

namespace KAM.Common.Tests.DependencyInjection.Settings;

/// <summary>
/// Tests the reflection scan itself (<see cref="ISettings"/> discovery). The binding/validation
/// behaviour a discovered type gets is <see cref="SettingsBaseTests"/>'s concern — which is also
/// what these tests lean on: this assembly already has intentionally-invalid <c>ISettings</c>
/// fixtures there (<c>EmptySectionSettings</c>, <c>MisnamedSettings</c>), which fail fast by
/// design. Which one the scan hits first depends on reflection's enumeration order, so the
/// assertion accepts either — what matters is that the scan found one.
/// </summary>
[TestFixture]
public class SettingsRegistrationTests
{
    private static readonly System.Reflection.Assembly ThisAssembly = typeof(SettingsRegistrationTests).Assembly;

    [Test]
    public void AddSettings_scans_the_given_assembly()
    {
        // Arrange & Act — if this discovered nothing, nothing would throw.
        ServiceCollection services = new();
        Action act = () => services.AddSettings(ThisAssembly);

        // Assert
        act.Should().Throw<InvalidOperationException>().Which.Message.Should().ContainAny("EmptySectionSettings", "MisnamedSettings");
    }

    [Test]
    public void AddSettings_with_no_assemblies_falls_back_to_the_calling_assembly_not_the_declaring_one()
    {
        // Arrange — if the zero-arg default scanned the *wrong* assembly (e.g. KAM.Common
        // itself, via Assembly.GetExecutingAssembly() — the exact bug this fallback exists to
        // avoid, see Program.cs's appAssemblies comment), this would NOT throw: KAM.Common has
        // no misconfigured settings type. Throwing for the right reason proves it scanned the
        // right assembly.
        ServiceCollection services = new();

        // Act
        Action act = () => services.AddSettings();

        // Assert
        act.Should().Throw<InvalidOperationException>().Which.Message.Should().ContainAny("EmptySectionSettings", "MisnamedSettings");
    }
}
