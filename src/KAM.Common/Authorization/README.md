# Authorization

Data-driven authorization policies: what a caller can do, expressed as configuration rather
than hardcoded C#. Kept separate from [`Auth/`](../Auth/README.md), which handles
authentication (who the caller is) — the two compose, but neither depends on the other beyond
`ScopeClaims` (see below).

## Why config-driven

A named policy — the default one, or any entry under `Authorization:Policies` — is a required-
scopes/roles/claims/custom-requirements shape read from `appsettings.json`. Tightening a
policy, or adding a new one, is a configuration change, not a code change and redeploy. This
is the single biggest thing this folder demonstrates that the rest of the repo doesn't.

```json
"Authorization": {
  "Policies": {
    "WeatherRead": {
      "RequiredScopes": [ "weather:read" ]
    },
    "ValidClientId": {
      "RequiredScopes": [ "weather:read" ],
      "CustomRequirements": [ "ValidClientIdRequirement" ]
    }
  },
  "AllowedClientIds": [ "web-client-01", "mobile-client-02" ],
  "EnableFallbackPolicy": true
}
```

`ValidClientId` above is defined but not applied to any endpoint in this repo — an
**available-but-unused example policy**, proven only by
`tests/KAM.Common.Tests/Authorization/`, demonstrating the custom-requirement mechanism
without needing a second real feature to hang it off.

## Files

| File | Purpose |
|---|---|
| `AuthorizationSettings.cs` | `AuthorizationSettings` (bound from the `Authorization` section), plus `DefaultPolicySettings` and `PolicySettings` — the shape one policy takes in config. |
| `AuthorizationPolicyBuilderExtensions.cs` | `ApplyPolicySettings(...)` — turns one `PolicySettings` (or `DefaultPolicySettings`) into calls on an `AuthorizationPolicyBuilder`. |
| `AuthorizationExtensions.cs` | `AddPolicyDrivenAuthorization()` — registers `AddAuthorization()`, configures `AuthorizationOptions` lazily from `AuthorizationSettings`, and registers `ValidClientIdHandler`. |
| `Requirements/ValidClientIdRequirement.cs` | `ValidClientIdRequirement` + `ValidClientIdHandler` — a worked example of a custom `IAuthorizationRequirement`. |

## Why policy names can't contain `:`

`Dictionary<string, PolicySettings>` binds one entry per immediate child key under
`Authorization:Policies` in the composed `IConfiguration` — and `IConfiguration` itself uses
`:` as its own path separator. A JSON property name like `"weather:read"` doesn't survive that
as a literal string; it becomes *two* nested configuration sections (`weather`, then `read`),
so the dictionary never gets an entry literally named `"weather:read"`, and
`.RequireAuthorization("weather:read")` fails at request time with
`AuthorizationPolicy named 'weather:read' was not found` — a real bug hit and fixed while
building this, not a hypothetical. This is exactly why [`AuthorizationPolicies.WeatherRead`](../Auth/AuthorizationPolicies.cs)
(the policy *name*, `"WeatherRead"`) and `Scopes.WeatherRead` (the scope *value*,
`"weather:read"`) are separate constants — the policy name has to avoid `:`, the scope value
is an OAuth2 convention that uses `:` freely, and conflating them would force a choice that
breaks one or the other.

## Why scope checking isn't `RequireClaim`

`RequireClaim("scope", "weather:read")` only matches a claim whose value is *exactly*
`"weather:read"`. Real identity providers (Entra ID, Auth0, Keycloak, …) emit one
space-delimited `scope` claim per the OAuth2 spec — e.g. `"weather:read openid profile"` —
which that check rejects outright. `ApplyPolicySettings` checks `RequiredScopes` via
[`KAM.Common.Auth.ScopeClaims`](../Auth/README.md) instead, through `RequireAssertion`, so it
accepts both that standard shape and a discrete claim per scope.

## Why the fallback policy denies by default

`AuthorizationSettings.EnableFallbackPolicy` (default `true`) sets `AuthorizationOptions.FallbackPolicy`
to the default policy, so an endpoint that maps without calling `.RequireAuthorization(...)` or
`.AllowAnonymous()` requires an authenticated user rather than being silently public. This is a
**runtime backstop**, not the primary guardrail: `EndpointAuthorizationTests`
(`tests/…/Integration/`) already fails the build if any mapped endpoint declares neither
explicitly, which is where this mistake should actually get caught. The fallback policy is
what happens if that test is ever skipped, bypassed, or an endpoint is registered through a
path it doesn't walk.

## Adding a custom requirement

1. Write the `IAuthorizationRequirement` + `AuthorizationHandler<T>` pair (see
   `Requirements/ValidClientIdRequirement.cs`).
2. Register the handler: `services.AddScoped<IAuthorizationHandler, YourHandler>()` in
   `AddPolicyDrivenAuthorization()`.
3. Add a `case nameof(YourRequirement):` arm to the switch in
   `AuthorizationPolicyBuilderExtensions.ApplyPolicySettings`.
4. Reference it by name in a policy's `CustomRequirements` list in `appsettings.json`.

An unrecognized name in `CustomRequirements` is logged as a warning and skipped, not a startup
failure — configuration is allowed to name a requirement that doesn't (yet) exist in this
build.

## Tests

`tests/KAM.Common.Tests/Authorization/` — `AuthorizationExtensionsTests` (default policy denies
anonymous, a named policy accepts either scope-claim shape, roles/claims wiring, the custom
requirement is added when configured and skipped when unrecognized or under-configured, the
fallback policy toggle); `Requirements/ValidClientIdHandlerTests` (allowed/case-insensitive/
denied/missing-claim, against the handler directly — no host needed).
