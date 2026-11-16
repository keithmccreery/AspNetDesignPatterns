using System.Data.Common;

using KAM.Common.Data;

namespace AspNetDesignPatterns.Api.Features.Library;

/// <summary>
/// Brings the database schema up to date at startup by applying this slice's embedded
/// <c>Schema/*.sql</c> scripts with <see cref="SqlScriptMigrator"/>. A hosted service's
/// <c>StartAsync</c> completes before the server starts accepting requests, so no request can
/// see a missing table; a failing script fails startup.
/// </summary>
internal sealed partial class LibraryDatabaseInitializer : IHostedService
{
    private static readonly string SchemaResourcePrefix = typeof(LibraryDatabaseInitializer).Namespace + ".Schema.";

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<LibraryDatabaseInitializer> _logger;

    public LibraryDatabaseInitializer(
        IDbConnectionFactory connectionFactory,
        TimeProvider timeProvider,
        ILogger<LibraryDatabaseInitializer> logger)
    {
        _connectionFactory = connectionFactory;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using DbConnection connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);

        IReadOnlyList<string> applied = await SqlScriptMigrator.ApplyAsync(
            connection,
            typeof(LibraryDatabaseInitializer).Assembly,
            SchemaResourcePrefix,
            _timeProvider,
            cancellationToken);

        LogSchemaApplied(applied.Count, applied);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(Level = LogLevel.Information, Message = "Library schema: applied {Count} script(s) {Scripts}")]
    private partial void LogSchemaApplied(int count, IReadOnlyList<string> scripts);
}
