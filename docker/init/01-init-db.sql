-- Initialize database with required extensions
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

-- Ensure proper encoding
SET client_encoding = 'UTF8';

-- Grant privileges to mutils user (if not superuser)
-- These are typically handled by POSTGRES_USER, but adding for safety
GRANT ALL PRIVILEGES ON DATABASE mutils TO mutils;
GRANT ALL PRIVILEGES ON SCHEMA public TO mutils;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES TO mutils;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON SEQUENCES TO mutils;

-- Log initialization
DO $$
BEGIN
    RAISE NOTICE 'Mutils database initialized successfully';
END $$;
