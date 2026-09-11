# Shared/Logging

Hardening against **CRLF log forging / log injection (CWE-117)**. Untrusted input containing
`\r` or `\n` can otherwise be used to inject forged log lines. Serilog itself is configured
in `Program.cs` (`AddSerilog`); this folder is just the enricher it plugs in.

## Files

| File | Type | Purpose |
|---|---|---|
| `LogSanitizer.cs` | `LogSanitizer.Sanitize(string?)` | Replaces `\r` and `\n` with `_`. Only those two characters — other control chars (`\0`, `\t`, ANSI escapes) are intentionally out of scope. Null/empty pass through. |
| `ControlCharacterSanitizingEnricher.cs` | `ILogEventEnricher` | On every log event, rewrites any **top-level scalar string** property that contains `\r`/`\n`. Because Serilog renders `{Placeholder}` tokens from the structured properties, sanitizing the property also sanitizes the rendered message for every sink. |

## Wiring

```csharp
// Program.cs
builder.Services.AddSerilog((services, cfg) => cfg
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .Enrich.With<ControlCharacterSanitizingEnricher>());   // <-- global, every event
```

## Scope and limits

- **Top-level scalar strings only.** Destructured objects/collections (`{@Thing}`) are not
  deep-traversed. A CR/LF string nested inside a destructured object slips through — so keep
  destructured payloads to ids and enums, not raw user input. (This is the accepted
  trade-off called out in [`../Results`](../Results/README.md) for `Error.Context`.)
- **`LogEvent.Exception` is out of scope** — an `Exception` is effectively immutable and
  can't be cleanly rewritten without losing type/stack-trace fidelity. Neutralizing rendered
  exception text belongs in the sink's output formatter.
- Allocates a replacement list only when a property actually needs sanitizing; a clean event
  costs one scan and no allocation.

## Tests

`tests/AspNetDesignPatterns.Api.Shared.Tests/Logging/LogSanitizerTests.cs` — `LogSanitizer` (CR/LF replaced, other
control chars kept, null passthrough) and `ControlCharacterSanitizingEnricher` (scalar
strings sanitized, non-strings and clean values untouched).
