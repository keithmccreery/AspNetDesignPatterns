# Cors

Cross-Origin Resource Sharing (CORS), configured from `appsettings.json` rather than hardcoded
— the same "the shape lives in config" idea as [`Authorization/`](../Authorization/README.md),
applied to a smaller, self-contained concern.

```json
"Cors": {
  "AllowedOrigins": [ "http://localhost:5173" ],
  "AllowedHeaders": [ "*" ],
  "AllowedMethods": [ "GET", "POST", "PUT", "DELETE", "OPTIONS", "PATCH" ],
  "AllowCredentials": true,
  "MaxAgeSeconds": 3600
}
```

## Files

| File | Purpose |
|---|---|
| `CorsSettings.cs` | `CorsSettings`, bound from the `Cors` section. |
| `CorsSettingsValidator.cs` | FluentValidation: `AllowedOrigins` non-empty, and rejects a `"*"` origin combined with `AllowCredentials: true`. |
| `CorsExtensions.cs` | `AddCorsPolicy()` — registers a single named policy (`CorsExtensions.PolicyName`) configured lazily from `CorsSettings`. |

## Why `"*"` gets special-cased

`CorsPolicyBuilder.WithHeaders("*")` and `.WithMethods("*")` treat `"*"` as a literal header or
method name, not a wildcard — the wildcard behavior only exists on `.AllowAnyHeader()` /
`.AllowAnyMethod()` (and `.AllowAnyOrigin()` for origins). A config author writing
`"AllowedHeaders": ["*"]` means "any header," so `AddCorsPolicy()` checks for that value and
calls the `AllowAny*` method instead of passing `"*"` straight through — otherwise the policy
would silently reject every real header, since none of them literally equal `"*"`.

## Why a `"*"` origin can't have credentials

The Fetch spec (and every browser) refuses a CORS response that combines an `Access-Control-
Allow-Origin: *` with `Access-Control-Allow-Credentials: true` — a same-origin credentialed
request already works without CORS, and a wildcard-origin credentialed response would let any
site read a user's authenticated data. `AddCors` would only surface this as a confusing runtime
failure in a browser's console; `CorsSettingsValidator` catches the same misconfiguration at
startup instead.

## Usage

```csharp
// Program.cs
builder.Services.AddCorsPolicy();
// ...
app.UseCors(CorsExtensions.PolicyName); // before UseAuthentication()/UseAuthorization()
```

## Tests

`tests/KAM.Common.Tests/Cors/` — `CorsSettingsValidatorTests` (valid settings, empty origins
rejected, wildcard + credentials rejected, wildcard without credentials accepted);
`CorsExtensionsTests` (explicit origins/headers/methods wired through, `"*"` treated as
`AllowAny*` for origins/headers/methods, credentials + preflight max age applied) — built
against a real `CorsOptions`/`CorsPolicy`, not just checking the settings object.
