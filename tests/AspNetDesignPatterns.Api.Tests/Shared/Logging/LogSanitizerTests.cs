using AspNetDesignPatterns.Api.Shared.Logging;

using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;

namespace AspNetDesignPatterns.Api.Tests.Shared.Logging;

[TestFixture]
public class LogSanitizerTests
{
    [TestCase("clean value", "clean value")]
    [TestCase("line one\r\nline two", "line one__line two")]
    [TestCase("carriage\rreturn", "carriage_return")]
    [TestCase("line\nfeed", "line_feed")]
    [TestCase("", "")]
    public void Replaces_only_CR_and_LF(string input, string expected)
    {
        LogSanitizer.Sanitize(input).Should().Be(expected);
    }

    [Test]
    public void Leaves_other_control_characters_untouched()
    {
        LogSanitizer.Sanitize("tab\tnull\0esc\x1b").Should().Be("tab\tnull\0esc\x1b");
    }

    [Test]
    public void Passes_null_through()
    {
        LogSanitizer.Sanitize(null).Should().BeNull();
    }
}

[TestFixture]
public class ControlCharacterSanitizingEnricherTests
{
    private static readonly MessageTemplate Template = new MessageTemplateParser().Parse("test");

    private static LogEvent EventWith(params (string Name, object? Value)[] properties)
    {
        var props = properties.Select(p => new LogEventProperty(p.Name, new ScalarValue(p.Value)));
        return new LogEvent(DateTimeOffset.UtcNow, LogEventLevel.Information, exception: null, Template, props);
    }

    private static string? ScalarString(LogEvent logEvent, string name) =>
        ((ScalarValue) logEvent.Properties[name]).Value as string;

    [Test]
    public void Strips_CRLF_from_scalar_string_properties()
    {
        var logEvent = EventWith(("User", "attacker\r\nFAKE ENTRY"), ("Path", "/safe"));

        new ControlCharacterSanitizingEnricher().Enrich(logEvent, new NoopPropertyFactory());

        using (new AssertionScope())
        {
            ScalarString(logEvent, "User").Should().Be("attacker__FAKE ENTRY");
            ScalarString(logEvent, "Path").Should().Be("/safe");
        }
    }

    [Test]
    public void Leaves_non_string_and_clean_properties_alone()
    {
        var logEvent = EventWith(("Count", 42), ("Clean", "ok"));

        new ControlCharacterSanitizingEnricher().Enrich(logEvent, new NoopPropertyFactory());

        using (new AssertionScope())
        {
            ((ScalarValue) logEvent.Properties["Count"]).Value.Should().Be(42);
            ScalarString(logEvent, "Clean").Should().Be("ok");
        }
    }

    private sealed class NoopPropertyFactory : ILogEventPropertyFactory
    {
        public LogEventProperty CreateProperty(string name, object? value, bool destructureObjects = false) =>
            new(name, new ScalarValue(value));
    }
}
