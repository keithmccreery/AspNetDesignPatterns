using System.Data.Common;

namespace KAM.Common.Data;

/// <summary>
/// The scoped <see cref="IUnitOfWork"/>: opens one connection lazily, ties the transaction
/// outcome to the <see cref="Result"/>, and disposes the connection with the scope.
/// </summary>
/// <remarks>
/// Both <see cref="IDisposable"/> and <see cref="IAsyncDisposable"/>: a request scope is disposed
/// asynchronously, but a scope created with a plain <c>using</c> (a background job, a startup
/// task) is disposed synchronously, and the container throws if a service in it is only
/// <see cref="IAsyncDisposable"/>.
/// </remarks>
internal sealed class UnitOfWork : IUnitOfWork, IDisposable, IAsyncDisposable
{
    private readonly IDbConnectionFactory _connectionFactory;
    private DbConnection? _connection;

    public UnitOfWork(IDbConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public DbTransaction? Transaction { get; private set; }

    public async Task<DbConnection> GetConnectionAsync(CancellationToken cancellationToken) =>
        _connection ??= await _connectionFactory.OpenConnectionAsync(cancellationToken);

    public Task<Result<T>> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<Result<T>>> work,
        CancellationToken cancellationToken) =>
        RunAsync(work, cancellationToken);

    public Task<Result> ExecuteInTransactionAsync(
        Func<CancellationToken, Task<Result>> work,
        CancellationToken cancellationToken) =>
        RunAsync(work, cancellationToken);

    public void Dispose()
    {
        _connection?.Dispose();
        _connection = null;
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }
    }

    private async Task<TResult> RunAsync<TResult>(
        Func<CancellationToken, Task<TResult>> work,
        CancellationToken cancellationToken)
        where TResult : Result
    {
        // Already inside a transaction: join it. The outermost call owns commit/rollback, so a
        // failure here still rolls back everything when it reaches that call.
        if (Transaction is not null)
        {
            return await work(cancellationToken);
        }

        DbConnection connection = await GetConnectionAsync(cancellationToken);
        DbTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);
        Transaction = transaction;

        try
        {
            TResult result = await work(cancellationToken);

            if (result.IsSuccess)
            {
                await transaction.CommitAsync(cancellationToken);
            }
            else
            {
                // A failed Result is an expected outcome (404, 409, …), not an error in the
                // database conversation, so roll back explicitly rather than leave it to Dispose.
                await transaction.RollbackAsync(CancellationToken.None);
            }

            return result;
        }
        finally
        {
            // If work (or the commit) threw, the transaction is still open: disposing an
            // uncompleted DbTransaction rolls it back. Doing it here rather than calling
            // RollbackAsync in a catch means a failing rollback can't replace the original exception.
            Transaction = null;
            await transaction.DisposeAsync();
        }
    }
}
