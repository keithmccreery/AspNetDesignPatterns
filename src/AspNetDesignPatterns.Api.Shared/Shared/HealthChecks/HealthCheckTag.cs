namespace AspNetDesignPatterns.Api.Shared.HealthChecks;

/// <summary>
/// The tag that puts a check on the readiness probe. There is no equivalent for liveness:
/// <c>/health/live</c> deliberately runs zero checks (see <c>HealthCheckExtensions</c>), so
/// no tag for it would ever be read.
/// </summary>
public static class HealthCheckTag
{
    /// <summary>A check that gates readiness — run by <c>/health/ready</c>.</summary>
    public const string Ready = "ready";
}
