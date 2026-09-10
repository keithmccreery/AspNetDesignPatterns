namespace AspNetDesignPatterns.Api.DependencyInjection;

/// <summary>
/// A configuration settings class that binds itself to a section of <c>appsettings.json</c>
/// and validates on startup. Implement this via <see cref="SettingsBase{T}"/> rather than
/// directly; the reflection-based <c>AddSettings()</c> discovers implementations, instantiates
/// each one, and calls <see cref="RegisterSettings"/>.
/// </summary>
public interface ISettings
{
    /// <summary>
    /// The <c>appsettings.json</c> section this class binds to. Each implementing class
    /// declares its own <c>public static string Section</c>; it is read by reflection.
    /// </summary>
    static string Section => string.Empty;

    /// <summary>Binds and validates this settings type against the configuration.</summary>
    void RegisterSettings(IServiceCollection services);
}
