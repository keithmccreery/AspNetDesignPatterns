using KAM.Common.Results;

namespace KAM.Common.Tests.Results;

[TestFixture]
public class ErrorTests
{
    [Test]
    public void Categorised_factories_set_the_matching_type()
    {
        // Arrange & Act & Assert
        using (new AssertionScope())
        {
            Error.Failure("a", "b").Type.Should().Be(ErrorType.Failure);
            Error.Validation("a", "b").Type.Should().Be(ErrorType.Validation);
            Error.NotFound("a", "b").Type.Should().Be(ErrorType.NotFound);
            Error.Conflict("a", "b").Type.Should().Be(ErrorType.Conflict);
            Error.Unauthorized("a", "b").Type.Should().Be(ErrorType.Unauthorized);
            Error.Forbidden("a", "b").Type.Should().Be(ErrorType.Forbidden);
            Error.Upstream("a", "b").Type.Should().Be(ErrorType.Upstream);
        }
    }

    [Test]
    public void WithException_and_WithContext_return_copies_that_keep_the_original_fields()
    {
        // Arrange
        InvalidOperationException exception = new("boom");
        Error original = Error.Upstream("Weather.Down", "provider unavailable");

        // Act
        Error enriched = original.WithException(exception).WithContext(new { City = "Berlin" });

        // Assert
        using (new AssertionScope())
        {
            enriched.Code.Should().Be("Weather.Down");
            enriched.Message.Should().Be("provider unavailable");
            enriched.Type.Should().Be(ErrorType.Upstream);
            enriched.Exception.Should().BeSameAs(exception);
            enriched.Context.Should().NotBeNull();

            original.Exception.Should().BeNull("the original error is immutable");
            original.Context.Should().BeNull();
        }
    }

    [Test]
    public void None_is_the_empty_error_and_compares_equal_by_value()
    {
        // Arrange & Act & Assert
        Error.None.Should().Be(new Error(string.Empty, string.Empty));
    }

    [Test]
    public void ValidationError_carries_per_field_failures_and_is_an_Error()
    {
        // Arrange
        Dictionary<string, string[]> failures = new() { ["Name"] = ["required"] };

        // Act
        ValidationError error = new(failures);

        // Assert
        using (new AssertionScope())
        {
            error.Should().BeAssignableTo<Error>();
            error.Type.Should().Be(ErrorType.Validation);
            error.Failures.Should().ContainKey("Name");
        }
    }
}
