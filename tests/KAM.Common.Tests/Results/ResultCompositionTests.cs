using KAM.Common.Results;

namespace KAM.Common.Tests.Results;

[TestFixture]
public class ResultCompositionTests
{
    private static readonly Error Boom = Error.Failure("X.Boom", "boom");

    [Test]
    public void Bind_chains_while_successful_and_short_circuits_on_failure()
    {
        // Arrange & Act
        Result<int> ok = Result.Success(2)
            .Bind(x => Result.Success(x * 3))
            .Bind(x => Result.Success(x + 1));

        Result<int> stopped = Result.Success(2)
            .Bind(_ => Result.Failure<int>(Boom))
            .Bind<int, int>(_ => throw new InvalidOperationException("should not run"));

        // Assert
        using (new AssertionScope())
        {
            ok.Value.Should().Be(7);
            stopped.IsFailure.Should().BeTrue();
            stopped.Error.Should().Be(Boom);
        }
    }

    [Test]
    public void Map_transforms_only_a_success()
    {
        // Arrange & Act & Assert
        Result.Success(10).Map(x => x * 2).Value.Should().Be(20);
        Result.Failure<int>(Boom).Map(x => x * 2).Error.Should().Be(Boom);
    }

    [Test]
    public void Ensure_fails_a_success_that_breaks_the_predicate()
    {
        // Arrange & Act & Assert
        Result.Success(5).Ensure(x => x > 0, Boom).IsSuccess.Should().BeTrue();
        Result.Success(-1).Ensure(x => x > 0, Boom).Error.Should().Be(Boom);
    }

    [Test]
    public void Tap_and_TapError_run_the_matching_side_effect_only()
    {
        // Arrange
        List<string> log = [];

        // Act
        Result.Success(1).Tap(_ => log.Add("ok")).TapError(_ => log.Add("err"));
        Result.Failure<int>(Boom).Tap(_ => log.Add("ok")).TapError(_ => log.Add("err"));

        // Assert
        log.Should().Equal("ok", "err");
    }

    [Test]
    public void Match_collapses_a_non_generic_result()
    {
        // Arrange & Act & Assert
        Result.Success().Match(() => "ok", e => $"err {e.Code}").Should().Be("ok");
        ((Result) Boom).Match(() => "ok", e => $"err {e.Code}").Should().Be("err X.Boom");
    }

    [Test]
    public void Bind_overloads_for_a_non_generic_Result_chain_or_short_circuit()
    {
        // Arrange & Act & Assert — Result<TIn> -> Result
        Result.Success(2).Bind(x => x > 0 ? Result.Success() : Boom).IsSuccess.Should().BeTrue();
        Result.Failure<int>(Boom).Bind(_ => Result.Success()).Error.Should().Be(Boom);

        // Result -> Result
        Result.Success().Bind(() => Result.Success()).IsSuccess.Should().BeTrue();
        ((Result) Boom).Bind(() => throw new InvalidOperationException("should not run")).Error.Should().Be(Boom);

        // Result -> Result<TOut>
        Result.Success().Bind(() => Result.Success(1)).Value.Should().Be(1);
        ((Result) Boom).Bind(() => Result.Success(1)).Error.Should().Be(Boom);
    }

    [Test]
    public void Tap_and_TapError_run_the_matching_side_effect_only_for_a_non_generic_Result()
    {
        // Arrange
        List<string> log = [];

        // Act
        Result.Success().Tap(() => log.Add("ok")).TapError(_ => log.Add("err"));
        ((Result) Boom).Tap(() => log.Add("ok")).TapError(_ => log.Add("err"));

        // Assert
        log.Should().Equal("ok", "err");
    }
}

[TestFixture]
public class ResultAsyncCompositionTests
{
    private static readonly Error Boom = Error.Failure("X.Boom", "boom");

    private static Task<Result<int>> OkAsync(int v) => Task.FromResult(Result.Success(v));

    private static Task<Result<int>> FailAsync() => Task.FromResult(Result.Failure<int>(Boom));

    [Test]
    public async Task Async_chain_threads_the_value_through_await_boundaries()
    {
        // Arrange & Act
        Result<string> result = await OkAsync(2)
            .BindAsync(x => OkAsync(x * 5))                 // Task<Result> + async func
            .MapAsync(x => x + 1)                           // Task<Result> + sync func
            .MapAsync(x => Task.FromResult($"= {x}"));      // Task<Result> + async func

        // Assert
        result.Value.Should().Be("= 11");
    }

    [Test]
    public async Task Async_chain_short_circuits_on_the_first_failure()
    {
        // Arrange
        bool ran = false;

        // Act
        Result<int> result = await FailAsync()
            .BindAsync(_ => OkAsync(99))
            .TapAsync(_ => ran = true);

        // Assert
        using (new AssertionScope())
        {
            result.Error.Should().Be(Boom);
            ran.Should().BeFalse();
        }
    }

    [Test]
    public async Task MatchAsync_collapses_the_awaited_result()
    {
        // Arrange & Act
        string ok = await OkAsync(7).MatchAsync(
            v => Task.FromResult($"ok {v}"),
            e => Task.FromResult($"err {e.Code}"));

        string err = await FailAsync().MatchAsync(
            v => $"ok {v}",
            e => $"err {e.Code}");

        // Assert
        ok.Should().Be("ok 7");
        err.Should().Be("err X.Boom");
    }

    [Test]
    public async Task EnsureAsync_can_fail_a_success_with_an_async_predicate()
    {
        // Arrange & Act
        Result<int> result = await OkAsync(3)
            .EnsureAsync(x => Task.FromResult(x > 10), Boom);

        // Assert
        result.Error.Should().Be(Boom);
    }
}

[TestFixture]
public class ResultUtilitiesTests
{
    [Test]
    public void Combine_returns_the_first_failure()
    {
        // Arrange
        Error first = Error.NotFound("A", "a");

        // Act
        Result combined = ResultUtilities.Combine(
            Result.Success(),
            first,
            Error.Conflict("B", "b"));

        // Assert
        combined.Error.Should().Be(first);
    }

    [Test]
    public void Combine_of_non_generic_results_succeeds_when_all_succeed()
    {
        // Arrange & Act
        Result combined = ResultUtilities.Combine(Result.Success(), Result.Success(), Result.Success());

        // Assert
        combined.IsSuccess.Should().BeTrue();
    }

    [Test]
    public void Combine_of_values_returns_the_first_failure()
    {
        // Arrange
        Error first = Error.NotFound("A", "a");

        // Act
        Result<IReadOnlyList<int>> combined = ResultUtilities.Combine(
            Result.Success(1),
            Result.Failure<int>(first),
            Result.Failure<int>(Error.Conflict("B", "b")));

        // Assert
        combined.Error.Should().Be(first);
    }

    [Test]
    public void CombineAll_aggregates_every_failure_as_a_validation_error()
    {
        // Arrange & Act
        Result combined = ResultUtilities.CombineAll(
            Result.Success(),
            Error.Validation("Field.Name", "required"),
            Error.Validation("Field.Age", "out of range"));

        // Assert
        combined.Error.Should().BeOfType<ValidationError>()
            .Which.Failures.Should().ContainKeys("Field.Name", "Field.Age");
    }

    [Test]
    public void Combine_of_values_collects_them_all_on_success()
    {
        // Arrange & Act
        Result<IReadOnlyList<int>> combined = ResultUtilities.Combine(
            Result.Success(1), Result.Success(2), Result.Success(3));

        // Assert
        combined.Value.Should().Equal(1, 2, 3);
    }

    [Test]
    public void FirstSuccess_returns_the_first_hit_or_the_last_failure()
    {
        // Arrange & Act
        Result<string> hit = ResultUtilities.FirstSuccess(
            [1, 2, 3],
            n => n == 2 ? Result.Success($"found {n}") : Error.NotFound("N", "no"));

        // Assert
        hit.Value.Should().Be("found 2");
    }

    [Test]
    public void FirstSuccess_returns_the_last_failure_when_nothing_succeeds()
    {
        // Arrange
        Error last = Error.NotFound("N", "last");

        // Act
        Result<string> result = ResultUtilities.FirstSuccess(
            [1, 2],
            n => n == 1 ? Error.NotFound("N", "first") : Result.Failure<string>(last));

        // Assert
        result.Error.Should().Be(last);
    }

    [Test]
    public void FirstSuccess_returns_a_NoItems_error_for_an_empty_sequence()
    {
        // Arrange & Act
        Result<string> result = ResultUtilities.FirstSuccess(
            [],
            (int n) => Result.Success($"found {n}"));

        // Assert
        result.Error.Code.Should().Be("Result.NoItems");
    }

    [Test]
    public void Ensure_with_a_single_predicate_wraps_or_fails()
    {
        // Arrange
        Error error = Error.Validation("X", "must be positive");

        // Act & Assert
        ResultUtilities.Ensure(5, x => x > 0, error).Value.Should().Be(5);
        ResultUtilities.Ensure(-1, x => x > 0, error).Error.Should().Be(error);
    }

    [Test]
    public void Ensure_with_multiple_guards_succeeds_when_all_pass()
    {
        // Arrange & Act
        Result<int> result = ResultUtilities.Ensure(
            5,
            (x => x > 0, Error.Validation("X", "must be positive")),
            (x => x < 10, Error.Validation("X", "must be under 10")));

        // Assert
        result.Value.Should().Be(5);
    }

    [Test]
    public void Ensure_with_multiple_guards_returns_the_first_failing_guard()
    {
        // Arrange
        Error tooSmall = Error.Validation("X", "must be positive");
        Error tooBig = Error.Validation("X", "must be under 10");

        // Act
        Result<int> result = ResultUtilities.Ensure(
            15,
            (x => x > 0, tooSmall),
            (x => x < 10, tooBig));

        // Assert — the first guard passes (15 > 0); the second is the one that actually fails.
        result.Error.Should().Be(tooBig);
    }
}
