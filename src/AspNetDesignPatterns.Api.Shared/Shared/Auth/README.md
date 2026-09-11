# Shared/Auth

JWT bearer authentication, named authorization policies, and a Development-only endpoint that
mints tokens so you can exercise secured routes from Scalar or curl without an identity
provider.

## Files

| File | Type | Purpose |
|---|---|---|
| `JwtOptions.cs` | `SettingsBase<JwtOptions>` | Bound from the `Jwt` config section. `SigningKey` is a secret and comes from `.env` (`Jwt__SigningKey`), never a committed `appsettings.json`. Validated with **DataAnnotations** (`[Required]`, `[MinLength(32)]`, `[Range]`) because it has no FluentValidation validator — the fallback path of `SettingsBase`. |
| `AuthorizationPolicies.cs` | constants | `AuthorizationPolicies.WeatherRead` (a policy *name*) and `Scopes.WeatherRead` (the scope *value* it checks) are deliberately separate constants — see below. `Scopes.Default` is what the dev-token endpoint embeds. |
| `ScopeClaims.cs` | `ScopeClaims.Has(user, scope)` | Reads a `scope` claim correctly: matches a standard space-delimited claim (what a real IdP sends) *and* a discrete claim per scope. |
| `AuthExtensions.cs` | `AddJwtAuth()` | Registers JWT bearer auth, configures `TokenValidationParameters` lazily from `JwtOptions` (so options `ValidateOnStart` still governs startup), registers the `WeatherRead` policy (`RequireAuthenticatedUser` + `RequireAssertion` via `ScopeClaims`), sets a **deny-by-default `FallbackPolicy`**, and registers `DevTokenIssuer` + `TimeProvider.System`. |
| `TokenContracts.cs` | `TokenRequest` + `TokenRequestValidator`, `TokenResponse` | Request/response records for the dev-token endpoint, and the validator that keeps an empty `Subject` from reaching `DevTokenIssuer`. |
| `DevTokenIssuer.cs` | `DevTokenIssuer` | Builds a signed HS256 JWT from `JwtOptions` — `sub`/`name`/`scope` claims (one space-delimited `scope` claim, the standard shape), `iat`/`nbf`/`exp` from an injected `TimeProvider`. Extracted from the endpoint so the token shaping is unit-testable without a host. |
| `DevTokenEndpoint.cs` | `IEndpoint` | `POST /auth/token` → validated `TokenRequest` → `DevTokenIssuer.Issue(subject)`. **Mapped only in Development** (checks `IHostEnvironment` before mapping). |

## Why the policy name and the scope value are separate constants

`AuthorizationPolicies.WeatherRead` names the *policy*; `Scopes.WeatherRead` is the *scope
claim value* the policy checks. They read the same today, but they answer different
questions, and collapsing them into one constant means renaming the policy silently changes
the required scope (or vice versa). Keep them apart even when — like here — their values
happen to match.

## Why scope checking isn't `RequireClaim`

`RequireClaim("scope", "weather:read")` only matches a claim whose value is *exactly*
`"weather:read"`. Real identity providers (Entra ID, Auth0, Keycloak, …) emit one
space-delimited `scope` claim per the OAuth2 spec — e.g. `"weather:read openid profile"` —
which that check rejects outright. `ScopeClaims.Has` splits every `scope` claim on spaces
before comparing, so it accepts both that standard shape and a discrete claim per scope.

## Why the fallback policy denies by default

`options.FallbackPolicy` governs any endpoint that maps without calling
`.RequireAuthorization(...)` or `.AllowAnonymous()` — set to `RequireAuthenticatedUser()`
here, so that endpoint requires an authenticated user rather than being silently public. This
is a **runtime backstop**, not the primary guardrail: `EndpointAuthorizationTests`
(`tests/…/Integration/`) already fails the build if any mapped endpoint declares neither
explicitly, which is where this mistake should actually get caught. The fallback policy is
what happens if that test is ever skipped, bypassed, or an endpoint is registered through a
path it doesn't walk — the failure mode becomes "401", not "silently public". Every route in
this app already calls one or the other, so this changes nothing today; it only matters for a
future endpoint that forgets to.

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

`tests/AspNetDesignPatterns.Api.Shared.Tests/Auth/` —

- `DevTokenIssuerTests` — issued token validates against the configured parameters; expiry =
  now + lifetime, via a fixed `TimeProvider`.
- `TokenRequestValidatorTests` — rejects a missing/empty `Subject`.
- `DevTokenEndpointTests` — maps `/auth/token` in Development, does not map it in
  Production/Staging (built against a real, unstarted `WebApplication` per environment).
- `ScopeClaimsTests` — matches an exact single claim, a scope embedded in a space-delimited
  claim, and across multiple discrete claims; rejects an absent scope.
- `AuthWiringTests` — `AddJwtAuth` registers the issuer + `TimeProvider`; the `WeatherRead`
  policy denies anonymous users and its assertion accepts either scope-claim shape; bearer
  validation is configured from `JwtOptions`; `JwtOptions` DataAnnotations; an endpoint mapped
  with no declared auth intent gets 401 from the fallback policy (a real `TestServer`
  pipeline, since this is a middleware-level effect, not just a DI registration).

End-to-end (401 without a token, 200 with one) is covered by `tests/…/Integration`.
