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
| `*Handler.cs` (6) | The use cases. Each owns its logic directly (no service layer — see below), catches `DbException` → `LibraryErrors.DatabaseUnavailable`. `BorrowBookHandler` and `ReturnLoanHandler` run in a transaction. |
| `LibraryRepository.cs` | Dapper SQL over `IUnitOfWork`; `BookRow` / `LoanRow`. The slice's "client" layer: no business rules, may throw `DbException`. |
| `BookResponse.cs`, `LoanResponse.cs` | The API contract, mapped from rows. |
| `LibraryErrors.cs` | Every `Error` the slice returns. |
| `LibraryAuthorization.cs` | `LibraryPolicies` / `LibraryScopes`. |
| `LibraryOptions.cs` | `ConnectionString`, `DefaultPageSize` (20), `MaxPageSize` (100), `LoanPeriod` (14 days) + validator. |
| `LibraryDependencies.cs` | `IDependency`: connection factory, `AddUnitOfWork()`, repository, Dapper type handler, migrations, health check. |
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
BorrowBookHandler
  │  unitOfWork.ExecuteInTransactionAsync(…)   BEGIN IMMEDIATE (SQLite's write lock)
  │    1. TryTakeCopyAsync      UPDATE books SET available_copies = available_copies - 1
  │                             WHERE id = 9 AND available_copies > 0
  │       0 rows → book missing? 404 BookNotFound
  │                member already holds it? 409 AlreadyBorrowed
  │                otherwise 409 NoCopiesAvailable
  │    2. HasOpenLoanAsync      → 409 AlreadyBorrowed: ROLLBACK puts step 1's copy back
  │    3. InsertLoanAsync       INSERT … RETURNING id
  │  success → COMMIT
  │  catch (DbException) → 500 Library.DatabaseUnavailable (with the exception attached for the log)
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

`ReturnLoanHandler` is the same shape: load the caller's loan (404 if missing or someone
else's — no hint that the id exists), 409 if already returned, then close the loan and put the
copy back in one transaction.

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
  `SqlScriptMigrator`, the handlers and the paging code don't change. A hosted database's
  connection string is a secret: it would come from `.env`, like `Jwt__SigningKey`.

## Not every slice needs every layer

`Weather` has endpoint → handler → pipeline → service → client. This slice has
endpoint → handler → repository: no pipeline (nothing to compose) and **no service**, because
each use case is used by exactly one handler. The handler owns the use case, including the
transaction and the `DbException` → `Error` translation that `WeatherService` does for
`Weather`. If several handlers ever need the same rule, that's the moment to extract a
service. "Only the client layer throws" still holds: the repository throws (via Dapper), the
handlers catch.

## Tests

`tests/AspNetDesignPatterns.Api.Tests/Features/Library/` — handlers run against a real
in-memory database with this slice's schema, wired by the real `LibraryDependencies`
(`TestSupport/LibraryTestDatabase`, one DI scope per call, like a request):

- `ListBooksHandlerTests` — first page + cursor; following cursors visits every book once
  (equal titles across a page boundary); an insert behind the cursor doesn't shift the next
  page; search on title or author, case-insensitive, with `%` taken literally; database
  failure → `DatabaseUnavailable`.
- `BorrowBookHandlerTests` — success (copy taken, due date from `LoanPeriod`); unknown book;
  no copy left (nothing changes); borrowing twice rolls back the copy it took; holding the last
  copy yourself is `AlreadyBorrowed`; database failure.
- `ReturnLoanHandlerTests` — return puts the copy back; returning twice; someone else's loan
  is `LoanNotFound`; unknown loan.
- `LibraryQueryHandlerTests` — get book, get loan (own vs other), loans paged newest first.
- `LibraryRequestValidatorTests` — configured max page size, a loan cursor rejected by the
  book list, search length.
- `LibraryInfrastructureTests` — `MemberId` binding with and without `sub`, data-source path
  resolution, health check healthy/unhealthy.

`tests/…/Integration/LibraryEndpointTests` — through the real host and the database it
migrated and seeded at startup: 401 without a token; paging the whole catalogue with
`nextCursor` (50 books, 8 pages, no repeats); explicit `"nextCursor": null`; 400 on a tampered
cursor; borrow → 201 + `Location` → GET it → return; 409 `ProblemDetails` with `errorCode`;
loans private to their member.
