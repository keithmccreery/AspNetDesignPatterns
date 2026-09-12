using System.Diagnostics;

namespace KAM.Common.Telemetry;

/// <summary>
/// Shared <see cref="ActivitySource"/>s for custom spans. ASP.NET Core and <see cref="HttpClient"/>
/// already emit their own via the instrumentation registered in
/// <see cref="TelemetryExtensions.AddAppTelemetry"/>; this is for operations below that —
/// currently, one span per <c>Pipeline{TContext}</c> step (see <c>Pipeline.cs</c>).
/// </summary>
/// <remarks>
/// A source with no registered listener makes <c>StartActivity</c> return <see langword="null"/>
/// without allocating — so every call site using <c>ActivitySources.Pipeline.StartActivity(...)</c>
/// is a no-op with no cost when <see cref="TelemetrySettings.Enabled"/> is <see langword="false"/>
/// or nothing has called <see cref="TelemetryExtensions.AddAppTelemetry"/> at all (e.g. most
/// unit tests).
///
/// A test registering its own <see cref="ActivityListener"/> against a source here must read
/// the field into a local (<c>string name = Pipeline.Name;</c>) <em>before</em> calling
/// <see cref="ActivitySource.AddActivityListener"/>, and have its predicate close over that
/// local — not read <c>ActivitySources.Pipeline</c> directly. <see cref="ActivitySource"/>'s own
/// constructor notifies every already-registered listener synchronously, so a predicate that
/// re-enters this class's static field while it's still being assigned sees
/// <see langword="null"/> and throws — and because a failed static constructor is cached for
/// the process's lifetime, every later use of this class fails the same way, not just the one
/// test that got the order wrong. (Hit exactly this writing the tests for the Pipeline span.)
/// </remarks>
public static class ActivitySources
{
    /// <summary>One span per <c>Pipeline{TContext}</c> step, named after the step type.</summary>
    public static readonly ActivitySource Pipeline = new("KAM.Common.Pipeline");
}
