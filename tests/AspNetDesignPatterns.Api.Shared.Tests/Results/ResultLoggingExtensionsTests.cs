using AspNetDesignPatterns.Api.Shared.Results;

using Microsoft.Extensions.Logging;

namespace AspNetDesignPatterns.Api.Shared.Tests.Results;

[TestFixture]
public class ResultLoggingExtensionsTests
{
    private RecordingLogger _logger = null!;

    [SetUp]
    public void SetUp() => _logger = new RecordingLogger();

    [Test]
    public void LogOnFailure_logs_once_at_Error_and_returns_the_result_unchanged()
    {
        // Arrange
        Result<int> result = Result.Failure<int>(Error.NotFound("X.Missing", "not here"));

        // Act
        Result<int> returned = result.LogOnFailure(_logger, operation: "FindWidget");

        // Assert
        using (new AssertionScope())
        {
            returned.Should().BeSameAs(result);
            _logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Error);
            _logger.Entries[0].Message.Should().Contain("FindWidget").And.Contain("X.Missing");
        }
    }

    [Test]
    public void LogOnFailure_is_a_no_op_for_a_success()
    {
        // Arrange & Act
        Result.Success(1).LogOnFailure(_logger);

        // Assert
        _logger.Entries.Should().BeEmpty();
    }

    [Test]
    public void LogOnFailure_logs_the_attached_exception_and_context_separately()
    {
        // Arrange
        Error error = Error.Upstream("Weather.Down", "unavailable")
            .WithException(new HttpRequestException("no route"))
            .WithContext(new { City = "Berlin" });

        // Act
        Result.Failure<int>(error).LogOnFailure(_logger, operation: "GetForecast");

        // Assert
        using (new AssertionScope())
        {
            _logger.Entries.Should().HaveCount(2);
            _logger.Entries[0].Exception.Should().BeOfType<HttpRequestException>();
            _logger.Entries[1].Message.Should().Contain("context");
        }
    }

    [Test]
    public void LogOnSuccess_logs_at_the_requested_level_only_for_a_success()
    {
        // Arrange & Act
        Result.Success(1).LogOnSuccess(_logger, LogLevel.Debug, operation: "Save");
        Result.Failure<int>(Error.Failure("E", "e")).LogOnSuccess(_logger);

        // Assert
        using (new AssertionScope())
        {
            _logger.Entries.Should().ContainSingle();
            _logger.Entries[0].Level.Should().Be(LogLevel.Debug);
        }
    }

    [Test]
    public void LogResult_routes_success_and_failure_to_the_right_level()
    {
        // Arrange & Act
        Result.Success(1).LogResult(_logger, operation: "A");
        Result.Failure<int>(Error.Failure("E", "e")).LogResult(_logger, operation: "B");

        // Assert
        _logger.Entries.Select(e => e.Level).Should().Equal(LogLevel.Information, LogLevel.Error);
    }

    [Test]
    public void LogError_includes_the_caller_site()
    {
        // Arrange & Act
        Result.Failure<int>(Error.Failure("E.Code", "msg")).LogError(_logger);

        // Assert
        _logger.Entries[0].Message.Should()
            .Contain(nameof(LogError_includes_the_caller_site))
            .And.Contain("E.Code");
    }

    [Test]
    public async Task LogOnFailureAsync_awaits_then_logs()
    {
        // Arrange
        Task<Result<int>> task = Task.FromResult(Result.Failure<int>(Error.Failure("E", "e")));

        // Act
        await task.LogOnFailureAsync(_logger, operation: "AsyncOp");

        // Assert
        _logger.Entries.Should().ContainSingle(e => e.Message.Contains("AsyncOp"));
    }

    private sealed class RecordingLogger : ILogger
    {
        public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception), exception));

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
