using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

using Scalar.AspNetCore;

namespace AspNetDesignPatterns.Api.Shared.OpenApi;

public static class OpenApiExtensions
{
    /// <summary>
    /// Serves one OpenAPI JSON per API version (<c>/openapi/v1.json</c>, …) and the Scalar UI
    /// at <c>/scalar</c> (Development only). The document generator itself is registered by
    /// <c>AddApiVersioning().AddOpenApi(...)</c> in <c>Program.cs</c>.
    /// </summary>
    public static WebApplication MapApiReference(this WebApplication app)
    {
        app.MapOpenApi().WithDocumentPerVersion();

        if (app.Environment.IsDevelopment())
        {
            app.MapScalarApiReference(options => options
                .WithTitle("AspNetDesignPatterns API")
                .WithTheme(ScalarTheme.Mars));
        }

        return app;
    }
}

/// <summary>Adds a JWT bearer security scheme to the OpenAPI document so Scalar shows an "Authorize" box.</summary>
internal sealed class BearerSecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        OpenApiSecurityScheme scheme = new()
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            In = ParameterLocation.Header,
            BearerFormat = "JSON Web Token",
            Description = "Paste a token from POST /api/v1/auth/token (Development only).",
        };

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = scheme;

        document.Security ??= [];
        document.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", document)] = [],
        });

        return Task.CompletedTask;
    }
}
