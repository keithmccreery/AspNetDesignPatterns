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
| `OpenApiExtensions.cs` → `MapApiReference()` | `WebApplication` extension | `MapOpenApi().WithDocumentPerVersion()` serves `/openapi/v1.json` (and `/v2.json`, …). In Development, `MapScalarApiReference()` serves the UI at `/scalar`. |
| `OpenApiExtensions.cs` → `BearerSecuritySchemeTransformer` | `IOpenApiDocumentTransformer` | Adds an HTTP `bearer` / `JSON Web Token` security scheme to `components.securitySchemes["Bearer"]` and a document-wide security requirement referencing it. Registered on the document via `.AddOpenApi(o => o.Document.AddDocumentTransformer<BearerSecuritySchemeTransformer>())`. |

## Usage

```csharp
// Program.cs
builder.Services
    .AddApiVersioning(/* … */)
    .AddApiExplorer(/* 'v'V, SubstituteApiVersionInUrl */)
    .AddOpenApi(o => o.Document.AddDocumentTransformer<BearerSecuritySchemeTransformer>());

var app = builder.Build();
app.MapApiReference();   // /openapi/v1.json  +  /scalar (Development)
```

- Scalar UI: `http://localhost:5139/scalar`
- Raw document: `http://localhost:5139/openapi/v1.json`

## Tests

`tests/…/Shared/OpenApi/BearerSecuritySchemeTransformerTests.cs` — the transformer adds the
bearer scheme to `components` and a document-wide security requirement referencing it.
`MapApiReference` end-to-end (document returns 200, Scalar redirects) is covered by
`tests/…/Integration`.
