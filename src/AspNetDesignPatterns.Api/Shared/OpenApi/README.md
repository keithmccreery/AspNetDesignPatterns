# Shared/OpenApi

The OpenAPI document + [Scalar](https://scalar.com) UI, with a JWT bearer security scheme so
the "Authorize" box appears.

The document *generator* is registered in `Program.cs` by
`AddApiVersioning().AddApiExplorer().AddOpenApi(...)` — the versioning-aware `AddOpenApi`
produces **one document per API version** and supersedes a bare `services.AddOpenApi()`
(analyzer `AV0029`). This folder is the serving side plus the security-scheme transformer.

## Files

| File | Type | Purpose |
|---|---|---|
| `OpenApiExtensions.cs` → `MapApiReference()` | `WebApplication` extension | `MapOpenApi().WithDocumentPerVersion()` serves `/openapi/v1.json` (and `/v2.json`, …) **outside Production**. In Development, `MapScalarApiReference()` also serves the UI at `/scalar`. Both routes are explicitly `.AllowAnonymous()`. |
| `OpenApiExtensions.cs` → `BearerSecuritySchemeTransformer` | `IOpenApiDocumentTransformer` | Adds an HTTP `bearer` / `JSON Web Token` security scheme to `components.securitySchemes["Bearer"]` and a document-wide security requirement referencing it. Registered on the document via `.AddOpenApi(o => o.Document.AddDocumentTransformer<BearerSecuritySchemeTransformer>())`. |

## Why the document is Production-gated

The document is anonymous by necessity (an API doc behind a login isn't very useful) and it
lists every route plus the auth scheme description. Fine for a reference app; the
conservative default for a real API is to not publish your full surface to the internet.
Remove the `IsProduction` check in `MapApiReference` if your API doc is meant to be public —
that's a product decision, not a security requirement either way.

## Usage

```csharp
// Program.cs
builder.Services
    .AddApiVersioning(/* … */)
    .AddApiExplorer(/* 'v'V, SubstituteApiVersionInUrl */)
    .AddOpenApi(o => o.Document.AddDocumentTransformer<BearerSecuritySchemeTransformer>());

var app = builder.Build();
app.MapApiReference();   // /openapi/v1.json (outside Production) + /scalar (Development)
```

- Scalar UI: `http://localhost:5139/scalar`
- Raw document: `http://localhost:5139/openapi/v1.json`

## Tests

- `tests/…/Shared/OpenApi/BearerSecuritySchemeTransformerTests.cs` — the transformer adds the
  bearer scheme to `components` and a document-wide security requirement referencing it.
- `tests/…/Shared/OpenApi/OpenApiExposureTests.cs` — `MapApiReference` serves the document
  outside Production and hides it in Production; serves Scalar only in Development. Built
  against a real (unstarted) `WebApplication` per environment, since this is invisible to
  NetArchTest's type-level rules.

`MapApiReference` end-to-end against the real app (document returns 200 in the
Development-hosted test factory) is covered incidentally by `tests/…/Integration`.
