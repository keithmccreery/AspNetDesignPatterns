using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using HttpJsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

namespace AspNetDesignPatterns.Api.Tests.Integration;

/// <summary>
/// Program.cs configures System.Text.Json once, on the framework's Minimal API options, and
/// registers that same instance for everything that injects <see cref="JsonSerializerOptions"/>.
/// </summary>
[TestFixture]
public class JsonSerializerOptionsTests
{
    [Test]
    public void The_injectable_options_are_the_same_instance_Minimal_APIs_use()
    {
        // Arrange
        IServiceProvider services = GlobalTestSetup.Factory.Services;

        // Act
        JsonSerializerOptions injected = services.GetRequiredService<JsonSerializerOptions>();
        JsonSerializerOptions minimalApi = services.GetRequiredService<IOptions<HttpJsonOptions>>().Value.SerializerOptions;

        // Assert
        using (new AssertionScope())
        {
            injected.Should().BeSameAs(minimalApi);
            injected.PropertyNamingPolicy.Should().Be(JsonNamingPolicy.CamelCase);
            injected.DefaultIgnoreCondition.Should().Be(JsonIgnoreCondition.WhenWritingNull);
            injected.Converters.Should().ContainSingle(c => c is JsonStringEnumConverter);
        }
    }
}
