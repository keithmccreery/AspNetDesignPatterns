using System.Text.Json;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AspNetDesignPatterns.Api.Shared.HealthChecks;

/// <summary>
/// Renders a <see cref="HealthReport"/> as a small, stable JSON document — enough for a human
/// or a dashboard to see <em>which</em> check is failing and for how long, without the
/// framework default (a bare <c>Healthy</c> string) or a third-party UI package.
/// </summary>
/// <remarks>
/// Only the exception <em>message</em> is surfaced, never the stack trace. Health endpoints
/// should be reachable only from inside the cluster; this keeps the blast radius small if one
/// is ever exposed by mistake.
/// </remarks>
internal static class HealthCheckResponseWriter
{
    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";

        JsonSerializerOptions jsonOptions = context.RequestServices.GetRequiredService<JsonSerializerOptions>();

        HealthReportBody body = new(
            Status: report.Status.ToString(),
            TotalDurationMs: report.TotalDuration.TotalMilliseconds,
            Entries: report.Entries.ToDictionary(
                entry => entry.Key,
                entry => new HealthEntryBody(
                    Status: entry.Value.Status.ToString(),
                    DurationMs: entry.Value.Duration.TotalMilliseconds,
                    Description: entry.Value.Description,
                    Error: entry.Value.Exception?.Message,
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
