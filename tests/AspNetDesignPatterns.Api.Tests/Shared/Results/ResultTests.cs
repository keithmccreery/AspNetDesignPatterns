using AspNetDesignPatterns.Api.Shared.Results;

namespace AspNetDesignPatterns.Api.Tests.Shared;

[TestFixture]
public class ResultTests
{
    [Test]
    public void Success_result_exposes_its_value()
    {
        Result<int> result = Result.Success(42);

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
        Error error = Error.NotFound("Widget.NotFound", "No widget.");

        Result<int> result = Result.Failure<int>(error);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(error);
        result.Invoking(r => r.Value).Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void Error_implicitly_converts_to_a_failed_result()
    {
        Result<string> result = Error.Validation("X", "bad");

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Test]
    public void Value_implicitly_converts_to_a_successful_result()
    {
        Result<string> result = "hello";

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("hello");
    }

    [Test]
    public void Match_invokes_the_branch_matching_the_outcome()
    {
        Result<int> success = 10;
        Result<int> failure = Error.Failure("E", "e");

        success.Match(v => $"ok:{v}", e => $"err:{e.Code}").Should().Be("ok:10");
        failure.Match(v => $"ok:{v}", e => $"err:{e.Code}").Should().Be("err:E");
    }

    [Test]
    public void Creating_a_success_with_an_error_is_rejected()
    {
        // Result.Failure<T> would refuse an empty error; drive the invariant through the ctor guard.
        Func<Result> act = () => new TestableResult(isSuccess: true, Error.Failure("E", "e"));

        act.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void ToResult_drops_the_value_but_keeps_the_outcome()
    {
        Result.Success(1).ToResult().IsSuccess.Should().BeTrue();
        Result.Failure<int>(Error.NotFound("X", "y")).ToResult().Error.Code.Should().Be("X");
    }

    // Result<T> is sealed, so exercise the shared base-class guard through the non-generic Result.
    private sealed class TestableResult(bool isSuccess, Error error) : Result(isSuccess, error);
}
