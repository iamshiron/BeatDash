-- Ensure proper encoding
SET client_encoding = 'UTF8';

-- Create the dedicated database
CREATE DATABASE beatdash;

-- Setup BeatDash Database
\c beatdash
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pgcrypto";
GRANT ALL PRIVILEGES ON SCHEMA public TO current_user;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES TO current_user;

-- Log initialization
DO $$
BEGIN
    RAISE NOTICE 'BeatDash database initialized successfully';
END $$;
