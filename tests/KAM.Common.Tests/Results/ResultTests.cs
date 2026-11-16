using KAM.Common.Results;

namespace KAM.Common.Tests.Results;

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
    public void Creating_a_failure_with_no_error_is_rejected()
    {
        // Arrange
        // Result.Failure would refuse Error.None itself; drive the invariant through the ctor guard.
        Func<Result> act = () => new TestableResult(isSuccess: false, Error.None);

        // Act & Assert
        act.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void TryGetValue_returns_the_value_and_true_for_a_success()
    {
        // Arrange
        Result<int> result = Result.Success(5);

        // Act
        bool found = result.TryGetValue(out int value);

        // Assert
        using (new AssertionScope())
        {
            found.Should().BeTrue();
            value.Should().Be(5);
        }
    }

    [Test]
    public void TryGetValue_returns_false_and_no_value_for_a_failure()
    {
        // Arrange
        Result<int> result = Result.Failure<int>(Error.NotFound("X", "y"));

        // Act
        bool found = result.TryGetValue(out int value);

        // Assert
        using (new AssertionScope())
        {
            found.Should().BeFalse();
            value.Should().Be(default);
        }
    }

    [Test]
    public void ToResult_drops_the_value_but_keeps_the_outcome()
    {
        // Arrange & Act & Assert
        Result.Success(1).ToResult().IsSuccess.Should().BeTrue();
        Result.Failure<int>(Error.NotFound("X", "y")).ToResult().Error.Code.Should().Be("X");
    }

    private static readonly Error Unavailable = Error.Failure("Store.Unavailable", "The store is down.");

    [Test]
    public async Task TryAsync_returns_the_works_result_when_nothing_is_thrown()
    {
        // Arrange & Act
        Result<int> result = await Result.TryAsync(
            () => Task.FromResult(Result.Success(7)),
            (InvalidOperationException ex) => Unavailable.WithException(ex));

        // Assert
        result.Value.Should().Be(7);
    }

    [Test]
    public async Task TryAsync_passes_a_failed_result_through_unchanged()
    {
        // Arrange
        Error notFound = Error.NotFound("Store.NotFound", "No such item.");

        // Act
        Result<int> result = await Result.TryAsync(
            () => Task.FromResult(Result.Failure<int>(notFound)),
            (InvalidOperationException ex) => Unavailable.WithException(ex));

        // Assert
        result.Error.Should().Be(notFound);
    }

    [Test]
    public async Task TryAsync_turns_the_chosen_exception_into_the_failure_it_builds()
    {
        // Arrange
        InvalidOperationException thrown = new("disk on fire");

        // Act
        Result<int> result = await Result.TryAsync<int, InvalidOperationException>(
            () => throw thrown,
            ex => Unavailable.WithException(ex));

        // Assert
        using (new AssertionScope())
        {
            result.Error.Code.Should().Be(Unavailable.Code);
            result.Error.Exception.Should().BeSameAs(thrown);
        }
    }

    [Test]
    public Task TryAsync_lets_any_other_exception_propagate()
    {
        // Arrange
        Func<Task<Result<int>>> act = () => Result.TryAsync<int, InvalidOperationException>(
            () => throw new OperationCanceledException(),
            ex => Unavailable.WithException(ex));

        // Act & Assert
        return act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Test]
    public async Task TryAsync_without_a_value_turns_the_chosen_exception_into_a_failure()
    {
        // Arrange & Act
        Result ok = await Result.TryAsync(
            () => Task.FromResult(Result.Success()),
            (InvalidOperationException ex) => Unavailable.WithException(ex));
        Result failed = await Result.TryAsync<InvalidOperationException>(
            () => throw new InvalidOperationException(),
            ex => Unavailable.WithException(ex));

        // Assert
        using (new AssertionScope())
        {
            ok.IsSuccess.Should().BeTrue();
            failed.Error.Code.Should().Be(Unavailable.Code);
        }
    }

    [Test]
    public Task TryAsync_without_a_value_lets_any_other_exception_propagate()
    {
        // Arrange
        Func<Task<Result>> act = () => Result.TryAsync<InvalidOperationException>(
            () => throw new OperationCanceledException(),
            ex => Unavailable.WithException(ex));

        // Act & Assert
        return act.Should().ThrowAsync<OperationCanceledException>();
    }

    // Result<T> is sealed, so exercise the shared base-class guard through the non-generic Result.
    private sealed class TestableResult(bool isSuccess, Error error) : Result(isSuccess, error);
}
