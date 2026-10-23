-- Ensure proper encoding
SET client_encoding = 'UTF8';

-- The PostgreSQL image creates and connects to POSTGRES_DB before running this script.
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pgcrypto";
GRANT ALL PRIVILEGES ON SCHEMA public TO current_user;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES TO current_user;

-- Log initialization
DO $$
BEGIN
    RAISE NOTICE 'BeatDash database initialized successfully';
END $$;
