using AspNetDesignPatterns.Api.Shared.Results;

namespace AspNetDesignPatterns.Api.Tests.Shared;

[TestFixture]
public class ResultTests
{
    [Test]
    public void Success_result_exposes_its_value()
    {
        // Arrange & Act
        Result<int> result = Result.Success(42);

        // Assert
        using (new AssertionScope())
        {
            result.IsSuccess.Should().BeTrue();
            result.IsFailure.Should().BeFalse();
            result.Value.Should().Be(42);
            result.Error.Should().Be(Error.None);
        }
    }

    [Test]
    public void Failure_result_exposes_its_error_and_hides_the_value()
    {
        // Arrange
        Error error = Error.NotFound("Widget.NotFound", "No widget.");

        // Act
        Result<int> result = Result.Failure<int>(error);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(error);
        result.Invoking(r => r.Value).Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void Error_implicitly_converts_to_a_failed_result()
    {
        // Arrange & Act
        Result<string> result = Error.Validation("X", "bad");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Test]
    public void Value_implicitly_converts_to_a_successful_result()
    {
        // Arrange & Act
        Result<string> result = "hello";

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("hello");
    }

    [Test]
    public void Match_invokes_the_branch_matching_the_outcome()
    {
        // Arrange
        Result<int> success = 10;
        Result<int> failure = Error.Failure("E", "e");

        // Act & Assert
        success.Match(v => $"ok:{v}", e => $"err:{e.Code}").Should().Be("ok:10");
        failure.Match(v => $"ok:{v}", e => $"err:{e.Code}").Should().Be("err:E");
    }

    [Test]
    public void Creating_a_success_with_an_error_is_rejected()
    {
        // Arrange
        // Result.Failure<T> would refuse an empty error; drive the invariant through the ctor guard.
        Func<Result> act = () => new TestableResult(isSuccess: true, Error.Failure("E", "e"));

        // Act & Assert
        act.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void ToResult_drops_the_value_but_keeps_the_outcome()
    {
        // Arrange & Act & Assert
        Result.Success(1).ToResult().IsSuccess.Should().BeTrue();
        Result.Failure<int>(Error.NotFound("X", "y")).ToResult().Error.Code.Should().Be("X");
    }

    // Result<T> is sealed, so exercise the shared base-class guard through the non-generic Result.
    private sealed class TestableResult(bool isSuccess, Error error) : Result(isSuccess, error);
}
