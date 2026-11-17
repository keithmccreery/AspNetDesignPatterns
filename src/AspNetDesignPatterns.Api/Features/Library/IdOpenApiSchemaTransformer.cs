using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace AspNetDesignPatterns.Api.Features.Library;

/// <summary>
/// Describes <see cref="BookId"/> and <see cref="LoanId"/> in the OpenAPI document as what they
/// are on the wire: a plain 32-bit integer. Without it the generator sees a struct with a
/// <c>Value</c> property and documents that, so clients would be told about a type that never
/// appears in a request or response. Registered by <see cref="LibraryDependencies"/>.
/// </summary>
internal sealed class IdOpenApiSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(context);

        Type type = context.JsonTypeInfo.Type;
        if (type == typeof(BookId) || type == typeof(LoanId))
        {
            schema.Type = JsonSchemaType.Integer;
            schema.Format = "int32";
            schema.Properties?.Clear();
        }

        return Task.CompletedTask;
    }
}
