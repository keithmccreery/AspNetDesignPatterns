using AspNetDesignPatterns.Api.Shared.OpenApi;

using Microsoft.OpenApi;

namespace AspNetDesignPatterns.Api.Tests.Shared.OpenApi;

[TestFixture]
public class BearerSecuritySchemeTransformerTests
{
    private static async Task<OpenApiDocument> Transform()
    {
        var document = new OpenApiDocument();
        var transformer = new BearerSecuritySchemeTransformer();

        await transformer.TransformAsync(document, context: null!, CancellationToken.None);
        return document;
    }

    [Test]
    public async Task Adds_a_bearer_JWT_security_scheme_to_components()
    {
        var document = await Transform();

        var scheme = document.Components!.SecuritySchemes!["Bearer"];

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
        var document = await Transform();

        document.Security.Should().ContainSingle()
            .Which.Keys.Should().ContainSingle(k => k.Reference!.Id == "Bearer");
    }
}
