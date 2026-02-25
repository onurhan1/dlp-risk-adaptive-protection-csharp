-- Create a table to store raw JSON data from Forcepoint DLP API for debugging
-- This table helps inspect the 'source' object for missing fields like user_email, login_name etc.

CREATE TABLE IF NOT EXISTS raw_dlp_data (
    id SERIAL PRIMARY KEY,
    incident_id TEXT,
    fetched_at TIMESTAMP WITHOUT TIME ZONE DEFAULT NOW(),
    source_json JSONB, -- The 'source' object from the API response
    full_json JSONB    -- The complete incident JSON for context
);

-- Index for faster searching if needed
CREATE INDEX IF NOT EXISTS idx_raw_dlp_source ON raw_dlp_data USING gin (source_json);

COMMENT ON TABLE raw_dlp_data IS 'Temporary table for debugging Forcepoint DLP API raw responses';
