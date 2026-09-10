namespace AspNetDesignPatterns.Api.Shared.HealthChecks;

/// <summary>
/// The tags that split the registered health checks into the two probes Kubernetes (and most
/// orchestrators) expect:
/// <list type="bullet">
///   <item><b>liveness</b> (<c>/health/live</c>) — "is the process wedged?" Runs <em>no</em>
///     checks; a response at all means restart is not warranted.</item>
///   <item><b>readiness</b> (<c>/health/ready</c>) — "can this instance serve traffic?" Runs
///     every check tagged <see cref="Ready"/>: the things that must be reachable before the
///     instance is added to the load balancer.</item>
/// </list>
/// Register a check with the matching tag; the endpoints filter on it (see
/// <c>HealthCheckExtensions</c>).
/// </summary>
public static class HealthCheckTag
{
    /// <summary>A check that gates readiness — run by <c>/health/ready</c>.</summary>
    public const string Ready = "ready";

    /// <summary>A check cheap and local enough to run on the liveness probe.</summary>
    public const string Live = "live";
}
