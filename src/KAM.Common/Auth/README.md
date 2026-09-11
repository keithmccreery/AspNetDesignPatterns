# Auth

JWT bearer **authentication** — who the caller is — plus a Development-only endpoint that
mints tokens so you can exercise secured routes from Scalar or curl without an identity
provider. **Authorization** — what the caller can do — is a separate concern, in
[`Authorization/`](../Authorization/README.md); the two are wired independently in
`Program.cs` (`AddJwtAuth()` then `AddPolicyDrivenAuthorization()`).

## Files

| File | Type | Purpose |
|---|---|---|
| `JwtOptions.cs` | `SettingsBase<JwtOptions>` | Bound from the `Jwt` config section. `SigningKey` is a secret and comes from `.env` (`Jwt__SigningKey`), never a committed `appsettings.json`. Validated with **DataAnnotations** (`[Required]`, `[MinLength(32)]`, `[Range]`) because it has no FluentValidation validator — the fallback path of `SettingsBase`. |
| `AuthorizationPolicies.cs` | constants | `AuthorizationPolicies.WeatherRead` (a policy *name*, `"WeatherRead"`) and `Scopes.WeatherRead` (the scope *value*, `"weather:read"`) are deliberately separate constants — see below. `Scopes.Default` is what the dev-token endpoint embeds. |
| `ScopeClaims.cs` | `ScopeClaims.Has(user, scope)` | Reads a `scope` claim correctly: matches a standard space-delimited claim (what a real IdP sends) *and* a discrete claim per scope. Used by `Authorization/`'s policy config, not just here. |
| `AuthExtensions.cs` | `AddJwtAuth()` | Registers JWT bearer auth, configures `TokenValidationParameters` lazily from `JwtOptions` (so options `ValidateOnStart` still governs startup), wires `JwtBearerEvents` for authentication observability (failed/validated/challenge, logged at Debug/Warning since they fire on every request), and registers `DevTokenIssuer` + `TimeProvider.System`. **Does not** touch `AddAuthorization` — see `Authorization/`. |
| `TokenContracts.cs` | `TokenRequest` + `TokenRequestValidator`, `TokenResponse` | Request/response records for the dev-token endpoint, and the validator that keeps an empty `Subject` from reaching `DevTokenIssuer`. |
| `DevTokenIssuer.cs` | `DevTokenIssuer` | Builds a signed HS256 JWT from `JwtOptions` — `sub`/`name`/`scope` claims (one space-delimited `scope` claim, the standard shape), `iat`/`nbf`/`exp` from an injected `TimeProvider`. Extracted from the endpoint so the token shaping is unit-testable without a host. |
| `DevTokenEndpoint.cs` | `IEndpoint` | `POST /auth/token` → validated `TokenRequest` → `DevTokenIssuer.Issue(subject)`. **Mapped only in Development** (checks `IHostEnvironment` before mapping). |

## What's configurable on `JwtOptions`

Beyond `SigningKey`/`Issuer`/`Audience`/`AccessTokenMinutes`: `ValidateIssuer`,
`ValidateAudience`, `ValidateLifetime` (all default `true`), `ClockSkewSeconds` (default `30`,
range 0–3600), `SaveToken` (default `false`), and optional `NameClaimType`/`RoleClaimType`
overrides. All of these used to be hardcoded in `AddJwtAuth()` — moving them to config means
tightening (or, for a test environment, loosening) token validation doesn't need a code change.

## Why the policy name and the scope value are separate constants

`AuthorizationPolicies.WeatherRead` names the *policy*; `Scopes.WeatherRead` is the *scope
claim value* the policy's config (`Authorization:Policies:WeatherRead:RequiredScopes` in
`appsettings.json`) checks. They used to share one string when the policy was hardcoded C#;
now that the policy is config-driven, they **can't** share one — a policy name flows through
`IConfiguration`, which treats `:` as its own path separator, so a name containing `:` doesn't
bind as a literal dictionary key (see `Authorization/README.md` for the full story, including
the actual runtime failure this caused while building it). Keep the two apart even where, like
the scope value itself, a `:` would otherwise be natural.

## Why scope checking isn't `RequireClaim`

`RequireClaim("scope", "weather:read")` only matches a claim whose value is *exactly*
`"weather:read"`. Real identity providers (Entra ID, Auth0, Keycloak, …) emit one
space-delimited `scope` claim per the OAuth2 spec — e.g. `"weather:read openid profile"` —
which that check rejects outright. `ScopeClaims.Has` splits every `scope` claim on spaces
before comparing, so it accepts both that standard shape and a discrete claim per scope. This
is used both by `DevTokenIssuer`'s tests and by `Authorization/`'s config-driven policies.

## Usage

```csharp
// Program.cs
builder.Services.AddJwtAuth();
builder.Services.AddPolicyDrivenAuthorization(); // Authorization/ — the named policies + fallback
// ... app.UseAuthentication(); app.UseAuthorization();

// an endpoint
app.MapGet("/weather/forecast", Handler)
   .RequireAuthorization(AuthorizationPolicies.WeatherRead);

// getting a token in Development
// POST /api/v1/auth/token  { "subject": "me" }  ->  { "accessToken": "...", "expiresAtUtc": "..." }
```

## Tests

`tests/KAM.Common.Tests/Auth/` —

- `DevTokenIssuerTests` — issued token validates against the configured parameters; expiry =
  now + lifetime, via a fixed `TimeProvider`.
- `TokenRequestValidatorTests` — rejects a missing/empty `Subject`.
- `DevTokenEndpointTests` — maps `/auth/token` in Development, does not map it in
  Production/Staging (built against a real, unstarted `WebApplication` per environment).
- `ScopeClaimsTests` — matches an exact single claim, a scope embedded in a space-delimited
  claim, and across multiple discrete claims; rejects an absent scope.
- `AuthWiringTests` — `AddJwtAuth` registers the issuer + `TimeProvider`; bearer validation
  (including the new configurable toggles, clock skew, `SaveToken`, and claim-type overrides)
  is configured from `JwtOptions`; `JwtBearerEvents` are wired; `JwtOptions` DataAnnotations.
  The named-policy and fallback-policy behavior now lives in
  `tests/KAM.Common.Tests/Authorization/` — see that folder's README.

End-to-end (401 without a token, 200 with one) is covered by `tests/…/Integration`.
