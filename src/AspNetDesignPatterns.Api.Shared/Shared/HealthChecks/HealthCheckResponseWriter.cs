using System.Text.Json;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace AspNetDesignPatterns.Api.Shared.HealthChecks;

/// <summary>
/// Renders a <see cref="HealthReport"/> as a small, stable JSON document — enough for a human
/// or a dashboard to see <em>which</em> check is failing and for how long, without the
/// framework default (a bare <c>Healthy</c> string) or a third-party UI package.
/// </summary>
/// <remarks>
/// The description/exception detail is included only outside Production. Health endpoints are
/// anonymous by design (an orchestrator's probe carries no credentials), so this file is
/// exactly the kind of "read and copy from" code where <c>Exception.Message</c> — a hostname,
/// a port, a connection-string fragment, depending on what a future check wraps — must not be
/// handed to an unauthenticated caller. Status and duration are enough for a probe; the detail
/// is for whoever is looking at Scalar/logs in a non-Production environment.
/// </remarks>
internal static class HealthCheckResponseWriter
{
    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";

        JsonSerializerOptions jsonOptions = context.RequestServices.GetRequiredService<JsonSerializerOptions>();
        bool includeDetail = !context.RequestServices.GetRequiredService<IHostEnvironment>().IsProduction();

        HealthReportBody body = new(
            Status: report.Status.ToString(),
            TotalDurationMs: report.TotalDuration.TotalMilliseconds,
            Entries: report.Entries.ToDictionary(
                entry => entry.Key,
                entry => new HealthEntryBody(
                    Status: entry.Value.Status.ToString(),
                    DurationMs: entry.Value.Duration.TotalMilliseconds,
                    Description: includeDetail ? entry.Value.Description : null,
                    Error: includeDetail ? entry.Value.Exception?.Message : null,
                    Tags: [.. entry.Value.Tags]),
                StringComparer.Ordinal));

        return context.Response.WriteAsJsonAsync(body, jsonOptions, context.RequestAborted);
    }

    private sealed record HealthReportBody(
        string Status,
        double TotalDurationMs,
        IReadOnlyDictionary<string, HealthEntryBody> Entries);

    private sealed record HealthEntryBody(
        string Status,
        double DurationMs,
        string? Description,
        string? Error,
        IReadOnlyList<string> Tags);
}
