-- The first statement succeeds, the second fails: the whole script must roll back.
CREATE TABLE half_done (x INTEGER);
INSERT INTO no_such_table (x) VALUES (1);
