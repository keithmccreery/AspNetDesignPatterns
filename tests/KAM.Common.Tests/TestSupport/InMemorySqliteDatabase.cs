using System.Data.Common;

using KAM.Common.Data;

using Microsoft.Data.Sqlite;

namespace KAM.Common.Tests.TestSupport;

/// <summary>
/// A private, shared-cache in-memory SQLite database for one test: real transactions and real
/// SQL, with nothing on disk. An in-memory database lives only while a connection to it is open,
/// so this keeps one open for its own lifetime. Also an <see cref="IDbConnectionFactory"/> that
/// counts the connections it opens.
/// </summary>
public sealed class InMemorySqliteDatabase : IDbConnectionFactory, IDisposable
{
    private readonly SqliteConnection _keepAlive;

    public InMemorySqliteDatabase()
    {
        ConnectionString = $"Data Source=test-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        _keepAlive = new SqliteConnection(ConnectionString);
        _keepAlive.Open();
    }

    public string ConnectionString { get; }

    public int ConnectionsOpened { get; private set; }

    public async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        SqliteConnection connection = new(ConnectionString);
        await connection.OpenAsync(cancellationToken);
        ConnectionsOpened++;
        return connection;
    }

    /// <summary>Runs SQL on a connection of its own, outside any unit of work under test.</summary>
    public void Execute(string sql)
    {
        using SqliteCommand command = _keepAlive.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    /// <summary>A single value, read on a connection of its own — what another request would see.</summary>
    public T Scalar<T>(string sql)
    {
        using SqliteCommand command = _keepAlive.CreateCommand();
        command.CommandText = sql;
        return (T) Convert.ChangeType(command.ExecuteScalar()!, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
    }

    public void Dispose() => _keepAlive.Dispose();
}
