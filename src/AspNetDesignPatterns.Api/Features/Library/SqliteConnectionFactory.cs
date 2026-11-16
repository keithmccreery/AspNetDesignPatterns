using System.Data.Common;

using KAM.Common.Data;

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace AspNetDesignPatterns.Api.Features.Library;

/// <summary>
/// The app's <see cref="IDbConnectionFactory"/>: SQLite, from <see cref="LibraryOptions.ConnectionString"/>.
/// The only SQLite-specific type outside the SQL itself — moving to PostgreSQL means swapping
/// this class (and the SQL dialect), not the unit of work or the handlers.
/// </summary>
/// <remarks>
/// Connection-level behavior lives in the connection string rather than in code here:
/// <c>Foreign Keys=True</c> (SQLite leaves them off by default) and <c>Default Timeout</c>, which
/// Microsoft.Data.Sqlite also uses as its busy timeout — how long a writer waits for SQLite's
/// single write lock before giving up.
/// </remarks>
internal sealed class SqliteConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public SqliteConnectionFactory(IOptions<LibraryOptions> options, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(environment);
        _connectionString = ResolveDataSource(options.Value.ConnectionString, environment.ContentRootPath);
    }

    public async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        SqliteConnection connection = new(_connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    // A relative file path would otherwise be relative to the process's working directory,
    // which differs between `dotnet run`, an IDE, and a container.
    internal static string ResolveDataSource(string connectionString, string contentRootPath)
    {
        SqliteConnectionStringBuilder builder = new(connectionString);
        bool isFile = builder.Mode != SqliteOpenMode.Memory
            && !string.Equals(builder.DataSource, ":memory:", StringComparison.Ordinal)
            && !string.IsNullOrEmpty(builder.DataSource);

        if (isFile && !Path.IsPathRooted(builder.DataSource))
        {
            builder.DataSource = Path.Combine(contentRootPath, builder.DataSource);
        }

        return builder.ToString();
    }
}
