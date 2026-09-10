namespace AspNetDesignPatterns.Api.Shared.Logging;

/// <summary>
/// Sanitizes user-controlled values before they are written to logs, to prevent CRLF log
/// forging / log injection (CWE-117). Untrusted input containing carriage-return or line-feed
/// characters could otherwise be used to inject forged log entries.
/// </summary>
/// <remarks>
/// Only carriage-return (<c>\r</c>) and line-feed (<c>\n</c>) are replaced — the characters
/// that enable CRLF log forging. Other control characters (e.g. <c>\0</c>, <c>\t</c>, ANSI
/// escape sequences such as <c>\x1b</c>) are intentionally left untouched and out of scope.
/// </remarks>
public static class LogSanitizer
{
    /// <param name="value">The potentially untrusted value to sanitize.</param>
    /// <returns>The value with CR and LF replaced by underscores, or the original value when null/empty.</returns>
    public static string? Sanitize(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return value
            .Replace("\r", "_", StringComparison.Ordinal)
            .Replace("\n", "_", StringComparison.Ordinal);
    }
}
