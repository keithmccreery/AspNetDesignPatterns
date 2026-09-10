# Shared/Auth

JWT bearer authentication, named authorization policies, and a Development-only endpoint that
mints tokens so you can exercise secured routes from Scalar or curl without an identity
provider.

## Files

| File | Type | Purpose |
|---|---|---|
| `JwtOptions.cs` | `SettingsBase<JwtOptions>` | Bound from the `Jwt` config section. `SigningKey` is a secret and comes from `.env` (`Jwt__SigningKey`), never a committed `appsettings.json`. Validated with **DataAnnotations** (`[Required]`, `[MinLength(32)]`, `[Range]`) because it has no FluentValidation validator — the fallback path of `SettingsBase`. |
| `AuthorizationPolicies.cs` | constants | `WeatherRead` = `"weather:read"`. Endpoints reference the constant, never a string literal. `DefaultScopes` is what the dev-token endpoint embeds. |
| `AuthExtensions.cs` | `AddJwtAuth()` | Registers JWT bearer auth, configures `TokenValidationParameters` lazily from `JwtOptions` (so options `ValidateOnStart` still governs startup), registers the `WeatherRead` policy (`RequireAuthenticatedUser` + `RequireClaim("scope", "weather:read")`), and registers `DevTokenIssuer` + `TimeProvider.System`. |
| `TokenContracts.cs` | `TokenRequest`, `TokenResponse` | Request/response records for the dev-token endpoint. |
| `DevTokenIssuer.cs` | `DevTokenIssuer` | Builds a signed HS256 JWT from `JwtOptions` — `sub`/`name`/`scope` claims, `iat`/`nbf`/`exp` from an injected `TimeProvider`. Extracted from the endpoint so the token shaping is unit-testable without a host. |
| `DevTokenEndpoint.cs` | `IEndpoint` | `POST /auth/token` → `DevTokenIssuer.Issue(subject)`. **Mapped only in Development** (checks `IHostEnvironment` before mapping). |

## Usage

```csharp
// Program.cs
builder.Services.AddJwtAuth();
// ... app.UseAuthentication(); app.UseAuthorization();

// an endpoint
app.MapGet("/weather/forecast", Handler)
   .RequireAuthorization(AuthorizationPolicies.WeatherRead);

// getting a token in Development
// POST /api/v1/auth/token  { "subject": "me" }  ->  { "accessToken": "...", "expiresAtUtc": "..." }
```

## Tests

`tests/…/Shared/Auth/` — `DevTokenIssuerTests` (issued token validates against the configured
parameters; expiry = now + lifetime, via a fixed `TimeProvider`), `AuthWiringTests`
(`AddJwtAuth` registers the issuer + `TimeProvider`, the `WeatherRead` policy requires the
scope claim, bearer validation is configured from `JwtOptions`; `JwtOptions` DataAnnotations).
End-to-end (401 without a token, 200 with one) is covered by `tests/…/Integration`.
