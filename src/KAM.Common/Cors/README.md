# Cors

Cross-Origin Resource Sharing (CORS), configured from `appsettings.json` rather than hardcoded
— the same "the shape lives in config" idea as [`Authorization/`](../Authorization/README.md),
applied to a smaller, self-contained concern.

```json
"Cors": {
  "AllowedOrigins": [ "http://localhost:5173" ],
  "AllowedHeaders": [ "*" ],
  "AllowedMethods": [ "GET", "POST", "PUT", "DELETE", "OPTIONS", "PATCH" ],
  "AllowCredentials": false,
  "MaxAgeSeconds": 3600
}
```

## Files

| File | Purpose |
|---|---|
| `CorsSettings.cs` | `CorsSettings`, bound from the `Cors` section. |
| `CorsSettingsValidator.cs` | FluentValidation: `AllowedOrigins` non-empty, and rejects a `"*"` origin combined with `AllowCredentials: true`. |
| `CorsExtensions.cs` | `AddCorsPolicy()` — registers a single named policy (`CorsExtensions.PolicyName`) configured lazily from `CorsSettings` (`.ValidateOnStart()` forces that at startup, not first use). |

## Why `"*"` gets special-cased

`CorsPolicyBuilder.WithHeaders("*")` and `.WithMethods("*")` treat `"*"` as a literal header or
method name, not a wildcard — the wildcard behavior only exists on `.AllowAnyHeader()` /
`.AllowAnyMethod()` (and `.AllowAnyOrigin()` for origins). A config author writing
`"AllowedHeaders": ["*"]` means "any header," so `AddCorsPolicy()` checks for that value and
calls the `AllowAny*` method instead of passing `"*"` straight through — otherwise the policy
would silently reject every real header, since none of them literally equal `"*"`.

## Why `AllowedHeaders`/`AllowedMethods` default to empty

The configuration binder *appends* to a collection property that already has items — it
doesn't replace it. With `AllowedMethods` initialized to the six common methods, configuring
`"AllowedMethods": ["GET"]` would bind as all six **plus** `GET`, and `["authorization"]` for
headers would still contain the default `"*"`, i.e. any header. Config could never narrow the
policy. So both properties default to empty, and `AddCorsPolicy()` treats empty as "not
configured": any header, and `CorsSettings.DefaultAllowedMethods`. The same trap applies to any
`SettingsBase<T>` collection with a non-empty initializer — keep collection defaults empty.

## Why `AllowCredentials` defaults to `false`

"Credentials" in CORS means cookies, HTTP auth and TLS client certificates — things the browser
attaches *automatically*. This API authenticates with a bearer token, which the caller's code
sets explicitly in the `Authorization` header, so it works without credentials mode. Turning it
on anyway is latent risk: the moment cookie authentication is added, every origin in
`AllowedOrigins` can make cookie-carrying requests on a signed-in user's behalf. So it's off by
default; switch it on in config deliberately, alongside cookie auth.

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
`AllowAny*` for origins/headers/methods, configured headers/methods *replace* the defaults when
bound from real configuration, credentials + preflight max age applied) — built against a real
`CorsOptions`/`CorsPolicy`, not just checking the settings object.
