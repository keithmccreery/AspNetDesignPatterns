using KAM.Common.Results;

namespace KAM.Common.Tests.Results;

/// <summary>
/// <c>ResultExtensions.Async.cs</c> has 3 shapes per operator (see its own doc comment):
/// <c>Result&lt;T&gt;</c> + async projection, <c>Task&lt;Result&lt;T&gt;&gt;</c> + sync
/// projection, and <c>Task&lt;Result&lt;T&gt;&gt;</c> + async projection — so a chain never
/// has to break out of the fluent style to <c>await</c> an intermediate step.
/// <see cref="ResultAsyncCompositionTests"/> (in <c>ResultCompositionTests.cs</c>) proves the
/// shapes actually used by chains elsewhere in this repo; this file fills in every remaining
/// shape and every non-generic-<see cref="KAM.Common.Results.Result"/> overload, one shape at a
/// time, so the whole matrix is verified rather than just the slice the sample app happens to
/// exercise.
/// </summary>
[TestFixture]
public class ResultExtensionsAsyncTests
{
    private static readonly Error Boom = Error.Failure("X.Boom", "boom");

    private static Task<Result<int>> OkAsync(int v) => Task.FromResult(Result.Success(v));

    private static Task<Result<int>> FailAsync() => Task.FromResult(Result.Failure<int>(Boom));

    private static Task<Result> OkResultAsync() => Task.FromResult(Result.Success());

    private static Task<Result> FailResultAsync() => Task.FromResult((Result) Boom);

    // ---- MatchAsync ----

    [Test]
    public async Task MatchAsync_on_a_generic_Result_with_async_projections()
    {
        // Arrange & Act
        string ok = await Result.Success(7).MatchAsync(v => Task.FromResult($"ok {v}"), e => Task.FromResult($"err {e.Code}"));
        string err = await Result.Failure<int>(Boom).MatchAsync(v => Task.FromResult($"ok {v}"), e => Task.FromResult($"err {e.Code}"));

        // Assert
        ok.Should().Be("ok 7");
        err.Should().Be("err X.Boom");
    }

    [Test]
    public async Task MatchAsync_on_a_non_generic_Result_with_async_projections()
    {
        // Arrange & Act
        string ok = await Result.Success().MatchAsync(() => Task.FromResult("ok"), e => Task.FromResult($"err {e.Code}"));
        string err = await ((Result) Boom).MatchAsync(() => Task.FromResult("ok"), e => Task.FromResult($"err {e.Code}"));

        // Assert
        ok.Should().Be("ok");
        err.Should().Be("err X.Boom");
    }

    [Test]
    public async Task MatchAsync_on_a_non_generic_Result_task_with_sync_projections()
    {
        // Arrange & Act
        string ok = await OkResultAsync().MatchAsync(() => "ok", e => $"err {e.Code}");
        string err = await FailResultAsync().MatchAsync(() => "ok", e => $"err {e.Code}");

        // Assert
        ok.Should().Be("ok");
        err.Should().Be("err X.Boom");
    }

    [Test]
    public async Task MatchAsync_on_a_non_generic_Result_task_with_async_projections()
    {
        // Arrange & Act
        string ok = await OkResultAsync().MatchAsync(() => Task.FromResult("ok"), e => Task.FromResult($"err {e.Code}"));
        string err = await FailResultAsync().MatchAsync(() => Task.FromResult("ok"), e => Task.FromResult($"err {e.Code}"));

        // Assert
        ok.Should().Be("ok");
        err.Should().Be("err X.Boom");
    }

    // ---- MapAsync ----

    [Test]
    public async Task MapAsync_on_a_generic_Result_with_an_async_projection()
    {
        // Arrange & Act
        Result<string> ok = await Result.Success(5).MapAsync(v => Task.FromResult($"= {v}"));
        Result<string> err = await Result.Failure<int>(Boom).MapAsync(v => Task.FromResult($"= {v}"));

        // Assert
        ok.Value.Should().Be("= 5");
        err.Error.Should().Be(Boom);
    }

    // ---- BindAsync: Result<TIn> -> Result<TOut> ----

    [Test]
    public async Task BindAsync_on_a_generic_Result_with_an_async_next()
    {
        // Arrange & Act
        Result<int> ok = await Result.Success(2).BindAsync(x => OkAsync(x * 3));
        Result<int> err = await Result.Failure<int>(Boom).BindAsync(x => OkAsync(x * 3));

        // Assert
        ok.Value.Should().Be(6);
        err.Error.Should().Be(Boom);
    }

    [Test]
    public async Task BindAsync_on_a_generic_Result_task_with_a_sync_next()
    {
        // Arrange & Act
        Result<int> ok = await OkAsync(2).BindAsync(x => Result.Success(x * 3));
        Result<int> err = await FailAsync().BindAsync(x => Result.Success(x * 3));

        // Assert
        ok.Value.Should().Be(6);
        err.Error.Should().Be(Boom);
    }

    // ---- BindAsync: Result<TIn> -> Result (non-generic) ----

    [Test]
    public async Task BindAsync_from_a_generic_Result_to_non_generic_with_an_async_next()
    {
        // Arrange & Act
        Result ok = await Result.Success(2).BindAsync(_ => OkResultAsync());
        Result err = await Result.Failure<int>(Boom).BindAsync(_ => OkResultAsync());

        // Assert
        ok.IsSuccess.Should().BeTrue();
        err.Error.Should().Be(Boom);
    }

    [Test]
    public async Task BindAsync_from_a_generic_Result_task_to_non_generic_with_a_sync_next()
    {
        // Arrange & Act
        Result ok = await OkAsync(2).BindAsync(_ => Result.Success());
        Result err = await FailAsync().BindAsync(_ => Result.Success());

        // Assert
        ok.IsSuccess.Should().BeTrue();
        err.Error.Should().Be(Boom);
    }

    [Test]
    public async Task BindAsync_from_a_generic_Result_task_to_non_generic_with_an_async_next()
    {
        // Arrange & Act
        Result ok = await OkAsync(2).BindAsync(_ => OkResultAsync());
        Result err = await FailAsync().BindAsync(_ => OkResultAsync());

        // Assert
        ok.IsSuccess.Should().BeTrue();
        err.Error.Should().Be(Boom);
    }

    // ---- BindAsync: Result (non-generic) -> Result / Result<TOut> ----

    [Test]
    public async Task BindAsync_on_a_non_generic_Result_with_an_async_next()
    {
        // Arrange & Act
        Result ok = await Result.Success().BindAsync(OkResultAsync);
        Result err = await ((Result) Boom).BindAsync(() => throw new InvalidOperationException("should not run"));

        // Assert
        ok.IsSuccess.Should().BeTrue();
        err.Error.Should().Be(Boom);
    }

    [Test]
    public async Task BindAsync_on_a_non_generic_Result_task_with_a_sync_next()
    {
        // Arrange & Act
        Result ok = await OkResultAsync().BindAsync(Result.Success);
        Result err = await FailResultAsync().BindAsync(Result.Success);

        // Assert
        ok.IsSuccess.Should().BeTrue();
        err.Error.Should().Be(Boom);
    }

    [Test]
    public async Task BindAsync_on_a_non_generic_Result_task_with_an_async_next()
    {
        // Arrange & Act
        Result ok = await OkResultAsync().BindAsync(OkResultAsync);
        Result err = await FailResultAsync().BindAsync(OkResultAsync);

        // Assert
        ok.IsSuccess.Should().BeTrue();
        err.Error.Should().Be(Boom);
    }

    [Test]
    public async Task BindAsync_from_a_non_generic_Result_to_generic_with_an_async_next()
    {
        // Arrange & Act
        Result<int> ok = await Result.Success().BindAsync(() => OkAsync(1));
        Result<int> err = await ((Result) Boom).BindAsync(() => OkAsync(1));

        // Assert
        ok.Value.Should().Be(1);
        err.Error.Should().Be(Boom);
    }

    [Test]
    public async Task BindAsync_from_a_non_generic_Result_task_to_generic_with_a_sync_next()
    {
        // Arrange & Act
        Result<int> ok = await OkResultAsync().BindAsync(() => Result.Success(1));
        Result<int> err = await FailResultAsync().BindAsync(() => Result.Success(1));

        // Assert
        ok.Value.Should().Be(1);
        err.Error.Should().Be(Boom);
    }

    [Test]
    public async Task BindAsync_from_a_non_generic_Result_task_to_generic_with_an_async_next()
    {
        // Arrange & Act
        Result<int> ok = await OkResultAsync().BindAsync(() => OkAsync(1));
        Result<int> err = await FailResultAsync().BindAsync(() => OkAsync(1));

        // Assert
        ok.Value.Should().Be(1);
        err.Error.Should().Be(Boom);
    }

    // ---- EnsureAsync ----

    [Test]
    public async Task EnsureAsync_on_a_generic_Result_with_an_async_predicate()
    {
        // Arrange & Act
        Result<int> ok = await Result.Success(5).EnsureAsync(x => Task.FromResult(x > 0), Boom);
        Result<int> failed = await Result.Success(-1).EnsureAsync(x => Task.FromResult(x > 0), Boom);

        // Assert
        ok.Value.Should().Be(5);
        failed.Error.Should().Be(Boom);
    }

    [Test]
    public async Task EnsureAsync_on_a_generic_Result_task_with_a_sync_predicate()
    {
        // Arrange & Act
        Result<int> ok = await OkAsync(5).EnsureAsync(x => x > 0, Boom);
        Result<int> failed = await OkAsync(-1).EnsureAsync(x => x > 0, Boom);

        // Assert
        ok.Value.Should().Be(5);
        failed.Error.Should().Be(Boom);
    }

    // ---- TapAsync ----

    [Test]
    public async Task TapAsync_on_a_generic_Result_runs_only_on_success()
    {
        // Arrange
        List<string> log = [];

        // Act
        await Result.Success(1).TapAsync(_ => { log.Add("ok"); return Task.CompletedTask; });
        await Result.Failure<int>(Boom).TapAsync(_ => { log.Add("ok"); return Task.CompletedTask; });

        // Assert
        log.Should().Equal("ok");
    }

    [Test]
    public async Task TapAsync_on_a_generic_Result_task_with_a_sync_action_runs_only_on_success()
    {
        // Arrange
        List<string> log = [];

        // Act
        await OkAsync(1).TapAsync(_ => log.Add("ok"));
        await FailAsync().TapAsync(_ => log.Add("ok"));

        // Assert
        log.Should().Equal("ok");
    }

    [Test]
    public async Task TapAsync_on_a_generic_Result_task_with_an_async_action_runs_only_on_success()
    {
        // Arrange
        List<string> log = [];

        // Act
        await OkAsync(1).TapAsync(_ => { log.Add("ok"); return Task.CompletedTask; });
        await FailAsync().TapAsync(_ => { log.Add("ok"); return Task.CompletedTask; });

        // Assert
        log.Should().Equal("ok");
    }

    [Test]
    public async Task TapAsync_on_a_non_generic_Result_runs_only_on_success()
    {
        // Arrange
        List<string> log = [];

        // Act
        await Result.Success().TapAsync(() => { log.Add("ok"); return Task.CompletedTask; });
        await ((Result) Boom).TapAsync(() => { log.Add("ok"); return Task.CompletedTask; });

        // Assert
        log.Should().Equal("ok");
    }

    [Test]
    public async Task TapAsync_on_a_non_generic_Result_task_with_a_sync_action_runs_only_on_success()
    {
        // Arrange
        List<string> log = [];

        // Act
        await OkResultAsync().TapAsync(() => log.Add("ok"));
        await FailResultAsync().TapAsync(() => log.Add("ok"));

        // Assert
        log.Should().Equal("ok");
    }

    [Test]
    public async Task TapAsync_on_a_non_generic_Result_task_with_an_async_action_runs_only_on_success()
    {
        // Arrange
        List<string> log = [];

        // Act
        await OkResultAsync().TapAsync(() => { log.Add("ok"); return Task.CompletedTask; });
        await FailResultAsync().TapAsync(() => { log.Add("ok"); return Task.CompletedTask; });

        // Assert
        log.Should().Equal("ok");
    }

    // ---- TapErrorAsync ----

    [Test]
    public async Task TapErrorAsync_on_a_generic_Result_runs_only_on_failure()
    {
        // Arrange
        List<string> log = [];

        // Act
        await Result.Success(1).TapErrorAsync(_ => { log.Add("err"); return Task.CompletedTask; });
        await Result.Failure<int>(Boom).TapErrorAsync(_ => { log.Add("err"); return Task.CompletedTask; });

        // Assert
        log.Should().Equal("err");
    }

    [Test]
    public async Task TapErrorAsync_on_a_generic_Result_task_with_a_sync_action_runs_only_on_failure()
    {
        // Arrange
        List<string> log = [];

        // Act
        await OkAsync(1).TapErrorAsync(_ => log.Add("err"));
        await FailAsync().TapErrorAsync(_ => log.Add("err"));

        // Assert
        log.Should().Equal("err");
    }

    [Test]
    public async Task TapErrorAsync_on_a_generic_Result_task_with_an_async_action_runs_only_on_failure()
    {
        // Arrange
        List<string> log = [];

        // Act
        await OkAsync(1).TapErrorAsync(_ => { log.Add("err"); return Task.CompletedTask; });
        await FailAsync().TapErrorAsync(_ => { log.Add("err"); return Task.CompletedTask; });

        // Assert
        log.Should().Equal("err");
    }

    [Test]
    public async Task TapErrorAsync_on_a_non_generic_Result_runs_only_on_failure()
    {
        // Arrange
        List<string> log = [];

        // Act
        await Result.Success().TapErrorAsync(_ => { log.Add("err"); return Task.CompletedTask; });
        await ((Result) Boom).TapErrorAsync(_ => { log.Add("err"); return Task.CompletedTask; });

        // Assert
        log.Should().Equal("err");
    }

    [Test]
    public async Task TapErrorAsync_on_a_non_generic_Result_task_with_a_sync_action_runs_only_on_failure()
    {
        // Arrange
        List<string> log = [];

        // Act
        await OkResultAsync().TapErrorAsync(_ => log.Add("err"));
        await FailResultAsync().TapErrorAsync(_ => log.Add("err"));

        // Assert
        log.Should().Equal("err");
    }

    [Test]
    public async Task TapErrorAsync_on_a_non_generic_Result_task_with_an_async_action_runs_only_on_failure()
    {
        // Arrange
        List<string> log = [];

        // Act
        await OkResultAsync().TapErrorAsync(_ => { log.Add("err"); return Task.CompletedTask; });
        await FailResultAsync().TapErrorAsync(_ => { log.Add("err"); return Task.CompletedTask; });

        // Assert
        log.Should().Equal("err");
    }

    // ---- ToHttpResultAsync ----

    [Test]
    public async Task ToHttpResultAsync_on_a_non_generic_Result_task()
    {
        // Arrange & Act
        Microsoft.AspNetCore.Http.IResult ok = await OkResultAsync().ToHttpResultAsync();
        Microsoft.AspNetCore.Http.IResult err = await FailResultAsync().ToHttpResultAsync();

        // Assert
        ok.Should().BeOfType<Microsoft.AspNetCore.Http.HttpResults.NoContent>();
        err.Should().BeOfType<Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult>();
    }
}
