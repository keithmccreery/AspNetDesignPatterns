namespace KAM.Common.DependencyInjection;

/// <summary>
/// The marker the reflection-based <c>AddSettings()</c> scan looks for: it instantiates each
/// implementation and calls <see cref="RegisterSettings"/>. Don't implement this directly —
/// derive from <see cref="SettingsBase{T}"/>, which implements it.
/// </summary>
public interface ISettings
{
    /// <summary>Binds and validates this settings type against the configuration.</summary>
    void RegisterSettings(IServiceCollection services);
}
