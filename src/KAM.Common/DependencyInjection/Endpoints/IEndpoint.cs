namespace KAM.Common.DependencyInjection;

/// <summary>
/// A single Minimal API endpoint. Implementations live next to the feature they belong to
/// (vertical slice) and are discovered and mapped by reflection at startup, so there is no
/// central "routes" file to edit when a feature is added.
/// </summary>
public interface IEndpoint
{
    /// <summary>Maps this endpoint onto the (already version-scoped) route builder.</summary>
    void MapEndpoint(IEndpointRouteBuilder app);
}
