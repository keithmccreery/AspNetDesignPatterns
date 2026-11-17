-- Full-text search over books.title and books.author: an FTS5 index with the trigram tokenizer,
-- so a search matches anywhere inside a word ("acu" finds "Dracula"), like the LIKE '%...%' it
-- replaces, but from an index. See the Library README for the requirements and limitations
-- (searches under 3 characters match nothing; the index's size).
--
-- content='books' makes this an "external content" table: it stores only the index and reads
-- the text back from books, so the titles and authors aren't stored twice. The price is keeping
-- it in sync by hand -- the three triggers below.

CREATE VIRTUAL TABLE books_search USING fts5(
    title,
    author,
    content = 'books',
    content_rowid = 'id',
    tokenize = 'trigram remove_diacritics 1'  -- "bronte" finds "Brontë"; case-insensitive by default
);

-- Index the books that already exist (the seed, or anything added before this script).
INSERT INTO books_search (books_search) VALUES ('rebuild');

CREATE TRIGGER books_search_after_insert AFTER INSERT ON books BEGIN
    INSERT INTO books_search (rowid, title, author) VALUES (new.id, new.title, new.author);
END;

-- An external-content index is told to forget a row by inserting the special 'delete' command
-- with the row's *old* values.
CREATE TRIGGER books_search_after_delete AFTER DELETE ON books BEGIN
    INSERT INTO books_search (books_search, rowid, title, author) VALUES ('delete', old.id, old.title, old.author);
END;

-- Only when the indexed text changes: borrowing and returning update available_copies on every
-- call and must not re-index the book each time.
CREATE TRIGGER books_search_after_update AFTER UPDATE OF title, author ON books BEGIN
    INSERT INTO books_search (books_search, rowid, title, author) VALUES ('delete', old.id, old.title, old.author);
    INSERT INTO books_search (rowid, title, author) VALUES (new.id, new.title, new.author);
END;
