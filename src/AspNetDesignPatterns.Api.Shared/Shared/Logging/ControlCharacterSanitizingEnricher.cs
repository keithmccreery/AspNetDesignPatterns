using Serilog.Core;
using Serilog.Events;

namespace AspNetDesignPatterns.Api.Shared.Logging;

/// <summary>
/// Serilog enricher that strips carriage-return and line-feed characters from scalar string
/// properties on every log event, neutralizing CRLF log forging / log injection (CWE-117)
/// globally — regardless of which call site produced the event.
/// </summary>
/// <remarks>
/// <para>
/// Because Serilog renders message-template placeholders (e.g. <c>{UserId}</c>) from the
/// structured properties, sanitizing the property value also sanitizes the rendered message
/// for every sink. Only top-level scalar string properties are sanitized; destructured
/// objects and collections are not deep-traversed.
/// </para>
/// <para>
/// <see cref="LogEvent.Exception"/> is intentionally out of scope: a <see cref="System.Exception"/>
/// is effectively immutable, so its message cannot be cleanly rewritten without losing the
/// original type and stack-trace fidelity. Neutralizing fully-rendered exception text belongs
/// in the sink output formatter; structured-property logging (used by all current call sites)
/// is covered here.
/// </para>
/// </remarks>
public sealed class ControlCharacterSanitizingEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        ArgumentNullException.ThrowIfNull(propertyFactory);

        // Scan read-only first; only allocate when a property actually needs sanitizing.
        // Applying replacements after the loop avoids mutating the dictionary mid-enumeration.
        List<LogEventProperty>? sanitized = null;

        foreach (KeyValuePair<string, LogEventPropertyValue> property in logEvent.Properties)
        {
            if (property.Value is ScalarValue { Value: string value }
                && (value.Contains('\r', StringComparison.Ordinal) || value.Contains('\n', StringComparison.Ordinal)))
            {
                sanitized ??= [];
                sanitized.Add(propertyFactory.CreateProperty(property.Key, LogSanitizer.Sanitize(value)));
            }
        }

        if (sanitized is null)
        {
            return;
        }

        foreach (LogEventProperty property in sanitized)
        {
            logEvent.AddOrUpdateProperty(property);
        }
    }
}
