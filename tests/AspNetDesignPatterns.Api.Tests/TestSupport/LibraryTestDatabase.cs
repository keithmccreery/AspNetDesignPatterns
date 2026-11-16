using System.Data.Common;

using AspNetDesignPatterns.Api.Features.Library;

using KAM.Common.Data;
using KAM.Common.Handlers;
using KAM.Common.Results;

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AspNetDesignPatterns.Api.Tests.TestSupport;

/// <summary>
/// A private in-memory SQLite database with the Library slice's real schema, plus a DI container
/// wired by the slice's real <see cref="LibraryDependencies"/>. <see cref="SendAsync{TRequest,TResponse}"/>
/// (a handler) and <see cref="CallAsync{T}"/> (the service) each run in their own scope — their own
/// connection and unit of work, like a request.
/// Starts with no books (the seed script's rows are deleted) so each test states its own data.
/// </summary>
internal sealed class LibraryTestDatabase : IDbConnectionFactory, IDisposable
{
    /// <summary>The clock every handler sees.</summary>
    public static readonly DateTimeOffset Now = new(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);

    private const string SCHEMA_PREFIX = "AspNetDesignPatterns.Api.Features.Library.Schema.";

    private readonly SqliteConnection _keepAlive;
    private readonly ServiceProvider _services;

    private LibraryTestDatabase(LibraryOptions options, IDbConnectionFactory? connectionFactory)
    {
        ConnectionString = $"Data Source=library-{Guid.NewGuid():N};Mode=Memory;Cache=Shared;Foreign Keys=True";
        _keepAlive = new SqliteConnection(ConnectionString);
        _keepAlive.Open();

        ServiceCollection services = new();
        services.AddSingleton(connectionFactory ?? this);       // registered first: the module's TryAdd keeps it
        services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now));
        services.AddSingleton(Options.Create(options));
        services.AddLogging();
        new LibraryDependencies().RegisterServices(services);
        services.AddRequestHandlers(typeof(LibraryDependencies).Assembly);
        _services = services.BuildServiceProvider();
    }

    public string ConnectionString { get; }

    /// <summary>Creates the database and applies the slice's schema scripts.</summary>
    /// <param name="options">Library settings; the defaults when omitted.</param>
    /// <param name="connectionFactory">Replaces the database for handlers — e.g. one that always fails.</param>
    public static async Task<LibraryTestDatabase> CreateAsync(
        LibraryOptions? options = null,
        IDbConnectionFactory? connectionFactory = null)
    {
        LibraryTestDatabase database = new(options ?? new LibraryOptions(), connectionFactory);
        await SqlScriptMigrator.ApplyAsync(
            database._keepAlive, typeof(LibraryDependencies).Assembly, SCHEMA_PREFIX, TimeProvider.System, CancellationToken.None);
        database.Execute("DELETE FROM books;");
        return database;
    }

    public async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        SqliteConnection connection = new(ConnectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    /// <summary>Runs <typeparamref name="TRequest"/>'s handler in a fresh DI scope.</summary>
    public async Task<Result<TResponse>> SendAsync<TRequest, TResponse>(TRequest request)
    {
        await using AsyncServiceScope scope = _services.CreateAsyncScope();
        IRequestHandler<TRequest, TResponse> handler =
            scope.ServiceProvider.GetRequiredService<IRequestHandler<TRequest, TResponse>>();
        return await handler.HandleAsync(request, CancellationToken.None);
    }

    /// <summary>Calls the real <see cref="ILibraryService"/> in a fresh DI scope.</summary>
    public async Task<T> CallAsync<T>(Func<ILibraryService, Task<T>> call)
    {
        ArgumentNullException.ThrowIfNull(call);
        await using AsyncServiceScope scope = _services.CreateAsyncScope();
        return await call(scope.ServiceProvider.GetRequiredService<ILibraryService>());
    }

    public int AddBook(string title, string author = "An Author", int copies = 1)
    {
        using SqliteCommand command = _keepAlive.CreateCommand();
        command.CommandText = """
            INSERT INTO books (title, author, published_year, total_copies, available_copies)
            VALUES ($title, $author, 1900, $copies, $copies) RETURNING id;
            """;
        command.Parameters.AddWithValue("$title", title);
        command.Parameters.AddWithValue("$author", author);
        command.Parameters.AddWithValue("$copies", copies);
        return Convert.ToInt32(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }

    public int AvailableCopies(int bookId) =>
        Scalar($"SELECT available_copies FROM books WHERE id = {bookId};");

    public int LoanCount() => Scalar("SELECT COUNT(*) FROM loans;");

    public void Execute(string sql)
    {
        using SqliteCommand command = _keepAlive.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    public void Dispose()
    {
        _services.Dispose();
        _keepAlive.Dispose();
    }

    private int Scalar(string sql)
    {
        using SqliteCommand command = _keepAlive.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt32(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }
}

/// <summary>An <see cref="IDbConnectionFactory"/> whose database is always down.</summary>
internal sealed class UnavailableDatabase : IDbConnectionFactory
{
    public Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken) =>
        throw new SqliteException("unable to open database file", 14);
}

/// <summary>A clock stopped at one instant.</summary>
internal sealed class FixedTimeProvider : TimeProvider
{
    private readonly DateTimeOffset _now;

    public FixedTimeProvider(DateTimeOffset now) => _now = now;

    public override DateTimeOffset GetUtcNow() => _now;
}
