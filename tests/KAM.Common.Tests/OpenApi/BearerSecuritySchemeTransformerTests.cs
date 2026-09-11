using KAM.Common.OpenApi;

using Microsoft.OpenApi;

namespace KAM.Common.Tests.OpenApi;

[TestFixture]
public class BearerSecuritySchemeTransformerTests
{
    private static async Task<OpenApiDocument> TransformAsync()
    {
        OpenApiDocument document = new();
        BearerSecuritySchemeTransformer transformer = new();

        await transformer.TransformAsync(document, context: null!, CancellationToken.None);
        return document;
    }

    [Test]
    public async Task Adds_a_bearer_JWT_security_scheme_to_components()
    {
        // Arrange & Act
        OpenApiDocument document = await TransformAsync();

        // Assert
        IOpenApiSecurityScheme scheme = document.Components!.SecuritySchemes!["Bearer"];

        using (new AssertionScope())
        {
            scheme.Type.Should().Be(SecuritySchemeType.Http);
            scheme.Scheme.Should().Be("bearer");
            scheme.BearerFormat.Should().Be("JSON Web Token");
        }
    }

    [Test]
    public async Task Adds_a_document_wide_security_requirement_referencing_the_scheme()
    {
        // Arrange & Act
        OpenApiDocument document = await TransformAsync();

        // Assert
        document.Security.Should().ContainSingle()
            .Which.Keys.Should().ContainSingle(k => k.Reference!.Id == "Bearer");
    }
}
