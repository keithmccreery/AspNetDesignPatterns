# Configuration

A small, eagerly-evaluated view of **"where and how am I running"**, built once in
`Program.cs` before the DI container is configured and registered as a singleton.

## Files

| File | Type | Purpose |
|---|---|---|
| `AppEnvironment.cs` | `AppEnvironment` | Derived from `IHostEnvironment` plus the `DOTNET_RUNNING_IN_CONTAINER` env var (set by the .NET base images). Exposes `EnvironmentName`, `IsDevelopment`, `IsProduction`, `IsContainerized`. |

## Usage

```csharp
// Program.cs — built before the container so startup code can branch on it
AppEnvironment appEnvironment = new(builder.Environment);
builder.Services.AddSingleton(appEnvironment);

// …then anywhere: inject AppEnvironment and branch on IsContainerized / IsProduction.
```

Not for choosing a listening port: the hosting platform already does that
(`ASPNETCORE_HTTP_PORTS` in the .NET container images, App Service's own variables,
`launchSettings.json` locally), and binding a port in code would override all of them.

## Why a type instead of injecting `IHostEnvironment` everywhere

- It's the seam for a fuller multi-environment model (production apps also carry
  account/region/ephemeral-slot identity, encrypted-config selection, etc.). Kept minimal
  here on purpose.
- `IsContainerized` isn't something `IHostEnvironment` knows; centralising the env-var read
  keeps that logic in one place.

## Tests

`tests/KAM.Common.Tests/Configuration/AppEnvironmentTests.cs` — reflects the hosting environment
name, recognises Production, `IsContainerized` reads the runtime flag (uses the
`ManageEnvironmentVariables` test helper), rejects a null host environment.
