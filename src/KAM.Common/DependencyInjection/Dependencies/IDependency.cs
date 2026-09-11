namespace KAM.Common.DependencyInjection;

/// <summary>
/// A feature slice's (or subsystem's) private DI registrations that convention-based scanning
/// cannot express: typed clients with resilience, OAuth handlers, caching, third-party SDKs.
/// Implementations are discovered by reflection and invoked once during startup, so adding a
/// feature never means editing a central composition-root file.
/// </summary>
public interface IDependency
{
    /// <summary>
    /// Registers application-specific services into the container. Called during startup,
    /// before the application begins handling requests.
    /// </summary>
    void RegisterServices(IServiceCollection services);
}
