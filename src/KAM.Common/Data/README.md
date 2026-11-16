# Data

Relational data access building blocks: one connection and transaction per request, a
transaction boundary tied to `Result`, and startup schema migrations. **Plain ADO.NET**
(`System.Data.Common`): nothing here references a provider (SQLite, Npgsql, …) or a mapper
(Dapper, EF Core), so the app picks both. `Features/Library/` is the worked example: SQLite +
Dapper.

## Files

| File | Type | Purpose |
|---|---|---|
| `IDbConnectionFactory.cs` | `IDbConnectionFactory` | Opens a connection. The one provider-specific seam; the app implements it (`SqliteConnectionFactory`). |
| `IUnitOfWork.cs` / `UnitOfWork.cs` | `IUnitOfWork` (scoped) | The request's connection (opened lazily, disposed with the scope), its current `Transaction`, and `ExecuteInTransactionAsync`. |
| `UnitOfWorkServiceCollectionExtensions.cs` | `AddUnitOfWork()` | Registers `IUnitOfWork` as scoped. Idempotent (`TryAdd`), so every data slice calls it from its own `IDependency`. |
| `SqlScriptMigrator.cs` | `SqlScriptMigrator.ApplyAsync(...)` | Applies embedded, numbered `.sql` scripts not yet recorded in `schema_versions`, each in its own transaction. |

## The unit of work

A repository asks the unit of work for the connection and passes its transaction to every
command:

```csharp
DbConnection connection = await _unitOfWork.GetConnectionAsync(cancellationToken);
await connection.ExecuteAsync(new CommandDefinition(sql, parameters, _unitOfWork.Transaction, cancellationToken: cancellationToken));
```

The service draws the transaction boundary (simplified from `LibraryService.BorrowBookAsync`):

```csharp
return Result.TryAsync(
    () => _unitOfWork.ExecuteInTransactionAsync<LoanRow>(async token =>
    {
        if (!await _repository.TryTakeCopyAsync(bookId, token)) return LibraryErrors.NoCopiesAvailable;
        if (await _repository.HasOpenLoanAsync(bookId, member, token)) return LibraryErrors.AlreadyBorrowed; // rolls back the copy taken above
        return await _repository.InsertLoanAsync(bookId, member, now, due, token);
    }, cancellationToken),
    (DbException ex) => LibraryErrors.DatabaseUnavailable.WithException(ex));
```

The outcome decides the transaction:

| `work` … | Transaction |
|---|---|
| returns a successful `Result` | committed |
| returns a failed `Result` (404, 409, …) | rolled back; the failure is returned |
| throws | rolled back (by disposing it); the exception propagates, to the service's `Result.TryAsync` |

So a business rule that fails after a write needs no compensating code: returning the `Error`
undoes the write. And because the result decides, it composes with the rest of `Results/` —
the service returns the same `Result<T>` either way. `Result.TryAsync` wraps the whole call, not
just the repository calls inside it, because the unit of work throws too: opening the
connection, `BEGIN`, `COMMIT`.

**Nested calls join the outer transaction.** A call made inside `work` runs in the same
transaction; only the outermost call commits or rolls back. There are no savepoints: an inner
failure that the outer code ignores still commits with everything else.

**One connection per scope, used sequentially.** Like the `DbConnection` it wraps, a unit of
work isn't thread-safe: don't run two queries on it concurrently (`Task.WhenAll`).

### Why not a repository-owned transaction, or `TransactionScope`?

- A repository can't know where a use case's transaction should end: "take a copy" and "record
  the loan" are separate repository methods, and only the borrow use case knows they belong
  together.
- `System.Transactions.TransactionScope` hides the boundary in ambient state, needs
  `TransactionScopeAsyncFlowOption.Enabled` to survive an `await`, and can't see a `Result`, so
  a failed `Result` would still commit.

## Migrations: `SqlScriptMigrator`

```csharp
await SqlScriptMigrator.ApplyAsync(connection, typeof(LibraryDatabaseInitializer).Assembly,
    "AspNetDesignPatterns.Api.Features.Library.Schema.", timeProvider, cancellationToken);
```

- Scripts are `EmbeddedResource`s (`<EmbeddedResource Include="Features\**\Schema\*.sql" />`),
  found by resource-name prefix and applied in ordinal order: number them `001_…`, `002_…`.
- Each script runs in a transaction together with its `schema_versions` row, so a failing
  script leaves nothing behind and fails startup; the next start retries it.
- The recorded name is the part after the prefix, so moving the namespace doesn't re-run anything.
- **Never edit an applied script** — add the next number. (There's no checksum; this is the
  smallest runner that's still safe. DbUp or EF Core migrations are the next step up.)
- `CREATE TABLE IF NOT EXISTS schema_versions` works on SQLite, PostgreSQL and MySQL; SQL
  Server would need its own bootstrap statement.
- Run it once, before requests are served: the Library slice does it in an `IHostedService`,
  whose `StartAsync` completes before the server starts listening. With several instances
  starting at once, run migrations as a separate deployment step instead.

## Tests

`tests/KAM.Common.Tests/Data/` runs everything against a real in-memory SQLite database
(`TestSupport/InMemorySqliteDatabase`), reading results on a separate connection the way
another request would:

- `UnitOfWorkTests` — commit on success; rollback on a failed `Result` and on an exception
  (rethrown); the non-generic overload; a nested call joining the outer transaction (an outer
  failure undoes the inner success); `Transaction` set only inside the work; one lazily
  opened connection, closed on dispose; `AddUnitOfWork` idempotent and scoped.
- `SqlScriptMigratorTests` — applies `.sql` scripts in order (the second depends on the first)
  and ignores other files; a re-run applies nothing; a script that fails halfway is rolled
  back whole and not recorded.
