-- Moves every timestamp by the same interval so the fixture always looks recent.
--
-- psql variables:
--   shift   interval added to every timestamp column, e.g. '91 days'
\set ON_ERROR_STOP on

SELECT set_config('fixture.shift', :'shift', false);

DO $$
DECLARE
    shift interval := current_setting('fixture.shift')::interval;
    col record;
BEGIN
    FOR col IN
        SELECT table_name, column_name
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name <> '__EFMigrationsHistory'
          AND data_type IN ('timestamp with time zone', 'timestamp without time zone')
    LOOP
        EXECUTE format('UPDATE %I SET %I = %I + $1', col.table_name, col.column_name, col.column_name)
        USING shift;
    END LOOP;
END $$;
