-- Applied once by KAM.Common's SqlScriptMigrator (see LibraryDatabaseInitializer). Never edit a
-- script that has been applied anywhere; add the next numbered one instead.

CREATE TABLE books (
    id               INTEGER PRIMARY KEY,
    title            TEXT    NOT NULL,
    author           TEXT    NOT NULL,
    published_year   INTEGER NOT NULL,
    total_copies     INTEGER NOT NULL CHECK (total_copies >= 0),
    available_copies INTEGER NOT NULL,
    CHECK (available_copies BETWEEN 0 AND total_copies)
);

-- Keyset paging (ORDER BY title, id) reads this index in order instead of sorting the table.
CREATE INDEX ix_books_title_id ON books (title, id);

CREATE TABLE loans (
    id          INTEGER PRIMARY KEY,
    book_id     INTEGER NOT NULL REFERENCES books (id),
    member_id   TEXT    NOT NULL,
    borrowed_at TEXT    NOT NULL,  -- ISO-8601 UTC, written by DateTimeOffsetTypeHandler
    due_at      TEXT    NOT NULL,
    returned_at TEXT    NULL
);

-- At most one open loan per member per book. BorrowBookHandler checks first and returns a 409;
-- this index is the backstop that makes a duplicate impossible rather than merely unlikely.
CREATE UNIQUE INDEX ux_loans_open_per_member_book ON loans (member_id, book_id) WHERE returned_at IS NULL;

-- "My loans", newest first: keyset paging on (member_id, id).
CREATE INDEX ix_loans_member_id ON loans (member_id, id);
