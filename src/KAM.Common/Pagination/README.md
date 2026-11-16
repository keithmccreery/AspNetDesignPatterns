# Pagination

Cursor (keyset) pagination: the request and response shapes, an opaque cursor codec, and the
shared validation rules. `Features/Library/` uses it for both of its lists.

## Files

| File | Type | Purpose |
|---|---|---|
| `ICursorPagedRequest.cs` | `ICursorPagedRequest`, `EffectiveLimit()` | `int? Limit` + `string? Cursor`. Implemented by the feature's own request record. |
| `CursorPagedRequestValidator.cs` | `CursorPagedRequestValidator<TKey>` | `Limit` within 1..max; `Cursor` decodes as `TKey`. `Include`d into the feature's validator. |
| `CursorCodec.cs` | `CursorCodec.Encode` / `TryDecode` | Sort key ↔ opaque string: JSON, then base64url (`System.Buffers.Text.Base64Url`). |
| `CursorPage.cs` | `CursorPage<T>`, `CursorPage.From`, `.Map` | The response (`items`, `nextCursor`), built from `limit + 1` rows. |

## How a cursor works

The slice defines a key that matches its `ORDER BY`, ending in a unique column so the order is
strict:

```csharp
internal sealed record BookCursor(string Title, int Id);   // ORDER BY title, id
```

1. **First page** (`GET /books?limit=20`, no cursor) — the query asks for one row more than
   the page: `ORDER BY title, id LIMIT 21`.
2. **Building the page** — `CursorPage.From(rows, limit: 20, row => new BookCursor(row.Title, row.Id))`:
   21 rows back means there's a next page, so it keeps 20 and encodes the 20th's key as
   `nextCursor` (`{"title":"Moby-Dick","id":20}` → `eyJ0aXRsZSI6…`). 20 or fewer means this is
   the last page: `"nextCursor": null`, always written (it overrides the app-wide
   `WhenWritingNull`), so a client never has to infer the end from a missing field.
3. **Next page** (`?cursor=eyJ0aXRsZSI6…`) — the validator checks it decodes; the handler
   decodes it, and the query continues from exactly that position:
   `WHERE (title, id) > (@Title, @Id) ORDER BY title, id LIMIT 21`.

```csharp
// The request: a flat record that implements the interface (not a base class)
internal sealed record ListBooksRequest(string? Search, int? Limit, string? Cursor) : ICursorPagedRequest;

// Its validator pulls in the shared rules
Include(new CursorPagedRequestValidator<BookCursor>(options.Value.MaxPageSize));

// The handler
int limit = request.EffectiveLimit(_options.DefaultPageSize);
CursorCodec.TryDecode(request.Cursor, out BookCursor? after);   // null → first page
IReadOnlyList<BookRow> rows = await _repository.ListBooksAsync(request.Search, after, limit + 1, ct);
return CursorPage.From(rows, limit, row => new BookCursor(row.Title, row.Id)).Map(BookResponse.From);
```

## Cursor vs offset

| | Cursor (keyset) — this folder | Offset (`page`, `pageSize`, `totalCount`) |
|---|---|---|
| Query | `WHERE (key) > (@last) … LIMIT n` — an index seek | `… LIMIT n OFFSET (page - 1) * n` — reads and discards every earlier row |
| Cost at depth | Constant | Grows with the page number |
| Concurrent inserts/deletes | Stable: a page continues from a fixed position | Rows shift between pages: duplicates or gaps |
| Jump to page N / show "page 3 of 12" | No | Yes (plus a `COUNT(*)` per request) |
| Fits | Feeds, infinite scroll, APIs, large tables | Small admin grids that need page numbers |

To add offset paging, a `PagedResult<T>(Items, Page, PageSize, TotalCount)` and a
`COUNT(*)` alongside the `LIMIT/OFFSET` query is all it takes; nothing here would change.

## The cursor's contract

- **Opaque, not secret.** Clients pass it back unchanged; they shouldn't parse it, and the
  server is free to change its contents. It is not encrypted or signed: it only holds a
  position in a list the caller can already read, so editing one just starts somewhere else.
  If a cursor ever needs to carry something a client mustn't forge (a tenant, a permission, a
  snapshot), sign it with an HMAC.
- **Strict decoding.** A cursor that isn't base64url, isn't JSON, is missing a key member or
  has a `null` one, or is longer than `CursorCodec.MaxLength` (512), is rejected: a 400
  `ValidationProblemDetails` on `Cursor`. A cursor from a different list fails too, when its key
  has a different shape (`LoanCursor(int Id)` isn't a `BookCursor`).
- **Tied to the query, by convention.** A cursor is a position in one ordering. Reusing it with
  a different `search` is still a valid position, just in different results. To enforce it,
  add a hash of the filters to the key and reject a mismatch.

## Tests

`tests/KAM.Common.Tests/Pagination/` —

- `CursorCodecTests` — round trip to a URL-safe string; rejects null/empty/non-base64url,
  non-JSON, JSON `null`, missing or null members, wrong member types, and over-long cursors.
- `CursorPageTests` — fewer than `limit + 1` rows is the last page; the extra row is dropped
  and the last kept row's key becomes the cursor; `limit < 1` throws; `Map` keeps the cursor;
  `nextCursor: null` is written under `WhenWritingNull`; `EffectiveLimit`.
- `CursorPagedRequestValidatorTests` — `Include`d in a feature-style validator: limits in and
  out of range, an encoded cursor accepted, a tampered one rejected on `Cursor`.
