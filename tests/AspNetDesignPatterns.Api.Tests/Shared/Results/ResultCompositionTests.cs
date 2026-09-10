using AspNetDesignPatterns.Api.Shared.Results;

namespace AspNetDesignPatterns.Api.Tests.Shared;

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
}
