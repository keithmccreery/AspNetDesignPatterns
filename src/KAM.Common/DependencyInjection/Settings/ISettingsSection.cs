namespace KAM.Common.DependencyInjection;

/// <summary>
/// Declares the configuration section a settings class binds to. A <c>static abstract</c>
/// member, so the compiler — not a reflection lookup at startup — guarantees every
/// <see cref="SettingsBase{T}"/> subclass has one: leaving it out is a build error
/// (<see cref="SettingsBase{T}"/>'s <c>T : ISettingsSection</c> constraint isn't satisfied).
/// </summary>
/// <remarks>
/// A separate interface rather than a member of <see cref="ISettings"/>: an abstract base class
/// can't satisfy a <c>static abstract</c> member on behalf of its subclasses, so
/// <see cref="SettingsBase{T}"/> could no longer implement <see cref="ISettings"/>.
/// </remarks>
public interface ISettingsSection
{
    /// <summary>The configuration section name, e.g. <c>"Weather"</c> for <c>appsettings.json</c>'s <c>"Weather": { … }</c>.</summary>
    static abstract string Section { get; }
}
