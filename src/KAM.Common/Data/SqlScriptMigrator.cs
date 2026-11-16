using System.Data.Common;
using System.Globalization;
using System.Reflection;

namespace KAM.Common.Data;

/// <summary>
/// Applies numbered <c>.sql</c> scripts, embedded in an assembly, that haven't been applied
/// yet — the smallest useful schema-migration runner, with no package and no provider-specific
/// code. Each script runs in its own transaction together with the <c>schema_versions</c> row
/// that records it, so a failing script leaves no trace and is retried on the next start.
/// </summary>
/// <remarks>
/// Scripts are found by resource-name prefix (e.g. <c>MyApp.Features.Library.Schema.</c>) and
/// applied in ordinal order of the remaining name, so number them with leading zeros
/// (<c>001_…</c>, <c>002_…</c>). The recorded name is that remainder, not the full resource
/// name, so renaming the namespace doesn't re-run anything. Never edit a script once it has been
/// applied anywhere — add a new one. <c>CREATE TABLE IF NOT EXISTS</c> (for
/// <c>schema_versions</c>) is supported by SQLite, PostgreSQL and MySQL; SQL Server would need
/// its own bootstrap statement.
/// </remarks>
public static class SqlScriptMigrator
{
    private const string VERSIONS_TABLE = "schema_versions";

    /// <summary>Applies every unapplied script and returns the names of those it applied.</summary>
    /// <remarks>
    /// Not <c>async</c> itself, so a bad argument throws at the call rather than when the task is
    /// awaited (ErrorProne.NET EPC37); the work happens in the <c>static</c> local function.
    /// </remarks>
    public static Task<IReadOnlyList<string>> ApplyAsync(
        DbConnection connection,
        Assembly assembly,
        string resourcePrefix,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentException.ThrowIfNullOrWhiteSpace(resourcePrefix);
        ArgumentNullException.ThrowIfNull(timeProvider);

        return ApplyCoreAsync(connection, assembly, resourcePrefix, timeProvider, cancellationToken);

        // A static local function: only this method can call it, and it can use only what is passed in.
        static async Task<IReadOnlyList<string>> ApplyCoreAsync(
            DbConnection connection,
            Assembly assembly,
            string resourcePrefix,
            TimeProvider timeProvider,
            CancellationToken cancellationToken)
        {
            await ExecuteAsync(
                connection,
                transaction: null,
                $"CREATE TABLE IF NOT EXISTS {VERSIONS_TABLE} (script TEXT NOT NULL PRIMARY KEY, applied_at TEXT NOT NULL);",
                cancellationToken);

            HashSet<string> applied = await ReadAppliedAsync(connection, cancellationToken);

            IEnumerable<string> pending = assembly.GetManifestResourceNames()
                .Where(name => name.StartsWith(resourcePrefix, StringComparison.Ordinal)
                            && name.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
                .Select(name => name[resourcePrefix.Length..])
                .Where(script => !applied.Contains(script))
                .Order(StringComparer.Ordinal);

            List<string> newlyApplied = [];
            foreach (string script in pending)
            {
                string sql = await ReadScriptAsync(assembly, resourcePrefix + script, cancellationToken);

                DbTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);
                await using (transaction)
                {
                    await ExecuteAsync(connection, transaction, sql, cancellationToken);
                    await RecordAsync(connection, transaction, script, timeProvider.GetUtcNow(), cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                }

                newlyApplied.Add(script);
            }

            return newlyApplied;
        }
    }

    private static async Task<HashSet<string>> ReadAppliedAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await using DbCommand command = connection.CreateCommand();
#pragma warning disable CA2100 // constant table name, no user input
        command.CommandText = $"SELECT script FROM {VERSIONS_TABLE};";
#pragma warning restore CA2100

        HashSet<string> applied = new(StringComparer.Ordinal);
        await using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            applied.Add(reader.GetString(0));
        }

        return applied;
    }

    private static async Task<string> ReadScriptAsync(Assembly assembly, string resourceName, CancellationToken cancellationToken)
    {
        await using Stream stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded schema script '{resourceName}' could not be opened.");
        using StreamReader reader = new(stream);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    private static async Task RecordAsync(
        DbConnection connection,
        DbTransaction transaction,
        string script,
        DateTimeOffset appliedAt,
        CancellationToken cancellationToken)
    {
        await using DbCommand command = connection.CreateCommand();
        command.Transaction = transaction;
#pragma warning disable CA2100 // constant table name; values are parameters
        command.CommandText = $"INSERT INTO {VERSIONS_TABLE} (script, applied_at) VALUES (@script, @applied_at);";
#pragma warning restore CA2100
        command.Parameters.Add(Parameter(command, "@script", script));
        command.Parameters.Add(Parameter(command, "@applied_at", appliedAt.ToString("O", CultureInfo.InvariantCulture)));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task ExecuteAsync(
        DbConnection connection,
        DbTransaction? transaction,
        string sql,
        CancellationToken cancellationToken)
    {
        await using DbCommand command = connection.CreateCommand();
        command.Transaction = transaction;
#pragma warning disable CA2100 // the SQL is a trusted, embedded schema script
        command.CommandText = sql;
#pragma warning restore CA2100
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static DbParameter Parameter(DbCommand command, string name, string value)
    {
        DbParameter parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        return parameter;
    }
}
