# Feature slice: Library

A small lending library: browse and search the catalogue, borrow a copy, return it, and list
your own loans. The second slice, and a different kind than `Weather`: it owns its data
(**SQLite + Dapper**), writes in **transactions** (the reusable unit of work in
[`KAM.Common/Data`](../../../KAM.Common/Data/README.md)), and pages its lists with **cursors**
([`KAM.Common/Pagination`](../../../KAM.Common/Pagination/README.md)). Everything else —
`Result`, `<Name>Errors`, config-driven policies, the scans — works exactly as in `Weather`.

| Route (`/api/v1/library/…`) | Policy | Success | Errors |
|---|---|---|---|
| `GET books?search=&limit=&cursor=` | `LibraryRead` | 200 `CursorPage<BookResponse>` | 400 |
| `GET books/{bookId}` | `LibraryRead` | 200 `BookResponse` | 404 |
| `POST books/{bookId}/loans` | `LibraryWrite` | 201 `LoanResponse` + `Location` | 404, 409 |
| `GET loans?limit=&cursor=` | `LibraryRead` | 200 `CursorPage<LoanResponse>` (yours, newest first) | 400 |
| `GET loans/{loanId}` | `LibraryRead` | 200 `LoanResponse` | 404 |
| `POST loans/{loanId}/return` | `LibraryWrite` | 200 `LoanResponse` | 404, 409 |

Plus 401/403 from authorization, and 500 `Library.DatabaseUnavailable` if the database fails.
The policies require the `library:read` / `library:write` scopes; in Development, dev tokens
carry both (`Jwt:DevTokenScopes`).

## Files

| File | Role |
|---|---|
| `*Endpoint.cs` (6) | `IEndpoint`s — bind (`[AsParameters]`), validate the list requests, delegate to the handler, `ToHttpResultAsync()`. `BorrowBookEndpoint` returns `201 Created` via `TypedResults.CreatedAtRoute` to `GetLoanEndpoint`. |
| `ListBooksRequest.cs`, `ListLoansRequest.cs` | Paged requests (`ICursorPagedRequest`), their cursor keys (`BookCursor`, `LoanCursor`) and validators (`Include` the shared paging rules). |
| `LibraryRequests.cs` | The single-resource requests. No validators: an id that matches nothing is already a 404. |
| `MemberId.cs` | The caller, from the token's `sub` claim — bound into request records by `BindAsync`, so handlers never see a `ClaimsPrincipal`. |
| `*Handler.cs` (6) | The use cases, as seen by the API: call `ILibraryService`, map rows to responses, and (for the lists) own the paging contract — decode the cursor, ask for `limit + 1`, build the next cursor. No exception handling, no SQL, no transactions. |
| `ILibraryService.cs`, `LibraryService.cs` | The service layer, the counterpart of `IWeatherService`: business rules, the order of repository calls, the transaction boundary (`ExecuteInTransactionAsync`), and `DbException` → `LibraryErrors.DatabaseUnavailable` via `Result.TryAsync`. Returns `Result<BookRow/LoanRow>`. |
| `ILibraryRepository.cs` | The gateway, the counterpart of `IWeatherClient`: one database operation per method, plus the `BookRow` / `LoanRow` it returns. No business rules; may throw `DbException`. |
| `LibraryRepository.cs` | The implementation: every SQL statement in the slice, Dapper over `IUnitOfWork`. |
| `BookResponse.cs`, `LoanResponse.cs` | The API contract, mapped from rows. |
| `LibraryErrors.cs` | Every `Error` the slice returns. |
| `LibraryAuthorization.cs` | `LibraryPolicies` / `LibraryScopes`. |
| `LibraryOptions.cs` | `ConnectionString`, `DefaultPageSize` (20), `MaxPageSize` (100), `LoanPeriod` (14 days) + validator. |
| `LibraryDependencies.cs` | `IDependency`: connection factory, `AddUnitOfWork()`, `ILibraryService` → `LibraryService`, `ILibraryRepository` → `LibraryRepository`, Dapper type handler, migrations, health check. |
| `SqliteConnectionFactory.cs` | The app's `IDbConnectionFactory`; resolves a relative `Data Source` against the content root. |
| `DateTimeOffsetTypeHandler.cs` | Dapper ↔ ISO-8601 UTC text, since SQLite has no date type. |
| `LibraryDatabaseInitializer.cs` | `IHostedService` that runs `SqlScriptMigrator` on `Schema/*.sql` before the server starts listening. |
| `Schema/001_create_tables.sql`, `002_seed_books.sql` | The schema, and 50 public-domain books to page through. |
| `LibraryDatabaseHealthCheck.cs` | `SELECT 1`, tagged `ready`; `Unhealthy` on failure (the app's own database, unlike Weather's `Degraded` upstream). |

## Request flow: borrowing a book

```
POST /api/v1/library/books/9/loans   (Bearer token, sub = "alice")
  │
  ├─ [AsParameters] BorrowBookRequest(BookId: 9, Member: MemberId.BindAsync → "alice")
  ├─ RequireAuthorization(LibraryWrite)  → 401 / 403
  ▼
BorrowBookHandler           → ILibraryService.BorrowBookAsync(9, alice), then LoanRow → LoanResponse
  ▼
LibraryService.BorrowBookAsync
  │  Result.TryAsync(…, (DbException ex) → 500 Library.DatabaseUnavailable, exception attached for the log)
  │  unitOfWork.ExecuteInTransactionAsync(…)   BEGIN IMMEDIATE (SQLite's write lock)
  │    1. TryTakeCopyAsync      UPDATE books SET available_copies = available_copies - 1
  │                             WHERE id = 9 AND available_copies > 0
  │       0 rows → book missing? 404 BookNotFound
  │                member already holds it? 409 AlreadyBorrowed
  │                otherwise 409 NoCopiesAvailable
  │    2. HasOpenLoanAsync      → 409 AlreadyBorrowed: ROLLBACK puts step 1's copy back
  │    3. InsertLoanAsync       INSERT … RETURNING id
  │  success → COMMIT
  ▼
.LogOnFailureAsync(logger) → .ToHttpResultAsync(loan => CreatedAtRoute("GetLibraryLoan", { loanId }))
  → 201 Created, Location: /api/v1/library/loans/{id}
```

**Why take the copy first?** The conditional `UPDATE` is the step that's safe under
concurrency: the check and the decrement are one statement, so two borrowers can't both take
the last copy. Checking first (`SELECT available_copies`, then `UPDATE`) would leave a gap
between the two. Putting it first also means a later rule that fails (step 2) has a write to
undo — which is exactly what the unit of work does, with no compensating code. The unique
partial index `ux_loans_open_per_member_book` is the backstop that makes a duplicate open loan
impossible, not just unlikely.

`ReturnLoanAsync` is the same shape: load the caller's loan (404 if missing or someone
else's — no hint that the id exists), 409 if already returned, then `CloseLoanAsync` and
`ReturnCopyAsync` — two writes to two tables, in one transaction.

`Result.TryAsync` wraps the transaction, not just the repository calls, on purpose: the unit of
work throws too (opening the connection, `BEGIN IMMEDIATE` timing out on the write lock,
`COMMIT`), and those failures happen outside any repository method.

## Paging

Books are ordered by `(title, id)`; loans by `id` descending (ids only grow, so one column is
already a strict order). Each list fetches `limit + 1` rows and lets `CursorPage.From` decide
whether there's a next page. The `(title, id)` key is why the two `"Poems"` books in the seed
data never collide at a page boundary. See
[`KAM.Common/Pagination/README.md`](../../../KAM.Common/Pagination/README.md) for the cursor
format, its validation, and cursor vs offset.

Search is `LIKE '%…%'` on title or author, case-insensitive for ASCII, with `%`, `_` and `\`
in the input escaped so they match literally. It can't use an index; for a large catalogue,
SQLite's FTS5 (or the database's full-text search) is the next step.

## SQLite notes

- **One writer at a time.** SQLite locks the whole database for a write. Microsoft.Data.Sqlite
  starts transactions with `BEGIN IMMEDIATE`, taking the write lock up front so two
  transactions can't both read and then deadlock upgrading; `Default Timeout` in the
  connection string is how long a writer waits for the lock (its busy timeout).
- **Foreign keys are off by default** in SQLite; `Foreign Keys=True` in the connection string
  turns them on for every connection.
- **No date type.** Timestamps are ISO-8601 text in UTC (`DateTimeOffsetTypeHandler`), which
  also sorts correctly as text.
- **`INTEGER PRIMARY KEY` ids** are SQLite's rowid: compact, ordered, and what `RETURNING id` gives
  back. They're guessable, which is fine here because every loan lookup is scoped to its member.
- **The file** is `library.db` in the content root (git-ignored), created and seeded on first
  run. Delete it to start over.
- **Moving to PostgreSQL** means a new `IDbConnectionFactory` (Npgsql) and the SQL dialect
  (`ILIKE`, identity columns, `timestamptz` instead of the type handler); `IUnitOfWork`,
  `SqlScriptMigrator`, the service, the handlers and the paging code don't change. A hosted
  database's connection string is a secret: it would come from `.env`, like `Jwt__SigningKey`.

## Layers

The same layers as `Weather`, minus the pipeline (nothing to compose):

| Layer | Here | Owns |
|---|---|---|
| Endpoint | `BorrowBookEndpoint`, … | HTTP: binding, auth, validation, `201 Created` |
| Handler | `BorrowBookHandler`, … | the API's view: rows → responses, the paging contract |
| Service | `ILibraryService` → `LibraryService` | the rules, the order of calls, the transaction, `DbException` → `Error` |
| Gateway | `ILibraryRepository` → `LibraryRepository` | one database operation per method; may throw |

Several service methods are pass-throughs today (`GetBookAsync` is a repository call plus
"null means 404"). They still go through the service, because the service is where exceptions
stop: an architecture test keeps handlers off the repository and `IUnitOfWork`, so a handler
can't forget a catch. When a use case grows (borrowing, returning), the new logic goes into
the service.

**One operation per repository method** is what keeps the line between gateway and service
clear. `TryTakeCopyAsync` is one conditional `UPDATE`; `InsertLoanAsync` is an `INSERT` plus a
read-back of the row it wrote. Returning a loan touches two tables, so it's two methods
(`CloseLoanAsync`, `ReturnCopyAsync`) that the service sequences inside its transaction.

### Why interfaces, if the tests use the real repository?

Consistent DI (`AddScoped<ILibraryService, LibraryService>()`, like `IWeatherService` and
`IWeatherClient`), and dependencies that read as contracts. They also split the tests by what
each layer owns: `LibraryServiceTests` run the real service and repository on in-memory SQLite,
because the conditional `UPDATE`, the rollback and the unique index are the behavior under
test and a substitute would fake them away. `LibraryHandlerTests` substitute `ILibraryService`,
because mapping and cursor handling don't need a database.

## Tests

`tests/AspNetDesignPatterns.Api.Tests/Features/Library/` — the database tests use a real
in-memory database with this slice's schema, wired by the real `LibraryDependencies`
(`TestSupport/LibraryTestDatabase`; `CallAsync` for the service, `SendAsync` for a handler, each
in its own DI scope, like a request):

- `LibraryServiceTests` — borrow: success (copy taken, due date from `LoanPeriod`), unknown
  book, no copy left (nothing changes), borrowing twice rolls back the copy it took, holding the
  last copy yourself is `AlreadyBorrowed`; return: puts the copy back, returning twice, someone
  else's loan is `LoanNotFound`, unknown loan; get book / get loan (own vs other); a database
  failure on a read and on opening a transaction → `DatabaseUnavailable`.
- `LibraryHandlerTests` — with a substitute `ILibraryService`: row → response mapping, a
  failure passed through unchanged, `limit + 1` turned into the next cursor, a decoded cursor
  handed to the service.
- `ListBooksHandlerTests` — paging and search end to end: first page + cursor; following
  cursors visits every book once (equal titles across a page boundary); an insert behind the
  cursor doesn't shift the next page; search on title or author, case-insensitive, with `%`
  taken literally.
- `ListLoansHandlerTests` — the caller's loans paged newest first, end to end.
- `LibraryRequestValidatorTests` — configured max page size, a loan cursor rejected by the
  book list, search length.
- `LibraryInfrastructureTests` — `MemberId` binding with and without `sub`, data-source path
  resolution, health check healthy/unhealthy.

`tests/…/Integration/LibraryEndpointTests` — through the real host and the database it
migrated and seeded at startup: 401 without a token; paging the whole catalogue with
`nextCursor` (50 books, 8 pages, no repeats); explicit `"nextCursor": null`; 400 on a tampered
cursor; borrow → 201 + `Location` → GET it → return; 409 `ProblemDetails` with `errorCode`;
loans private to their member.
