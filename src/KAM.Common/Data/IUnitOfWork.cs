using System.Data.Common;

namespace KAM.Common.Data;

/// <summary>
/// One database connection, and at most one transaction, shared by everything in a DI scope
/// (an HTTP request). Repositories pass <see cref="GetConnectionAsync"/> and
/// <see cref="Transaction"/> to their queries; use-case code decides the transaction boundary
/// with <see cref="ExecuteInTransactionAsync{T}"/>.
/// </summary>
/// <remarks>
/// Not thread-safe, like the <see cref="DbConnection"/> it wraps: within a scope, run database
/// calls one after another, not concurrently.
/// </remarks>
public interface IUnitOfWork
{
    /// <summary>
    /// The scope's connection, opened on first use. Not to be disposed by the caller — the unit
    /// of work owns it and disposes it with the scope.
    /// </summary>
    Task<DbConnection> GetConnectionAsync(CancellationToken cancellationToken);

    /// <summary>
    /// The transaction a query must enlist in, or <see langword="null"/> outside
    /// <see cref="ExecuteInTransactionAsync{T}"/>. Pass it to every command (Dapper's
    /// <c>transaction:</c> argument) — a provider rejects a command on a connection with an
    /// open transaction that doesn't name it.
    /// </summary>
    DbTransaction? Transaction { get; }

    /// <summary>
    /// Runs <paramref name="work"/> in a transaction that is committed if it returns a
    /// successful <see cref="Result{T}"/> and rolled back if it returns a failure or throws
    /// (the exception is rethrown). A call made while a transaction is already open joins it:
    /// the outermost call alone commits or rolls back.
    /// </summary>
    Task<Result<T>> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<Result<T>>> work,
        CancellationToken cancellationToken);

    /// <inheritdoc cref="ExecuteInTransactionAsync{T}"/>
    Task<Result> ExecuteInTransactionAsync(
        Func<CancellationToken, Task<Result>> work,
        CancellationToken cancellationToken);
}
