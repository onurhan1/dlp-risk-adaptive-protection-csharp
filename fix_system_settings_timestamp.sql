-- Fix system_settings table updated_at column type
-- Run this on your PostgreSQL database to fix the DateTime UTC error

-- First check the current column type
SELECT column_name, data_type 
FROM information_schema.columns 
WHERE table_name = 'system_settings' AND column_name = 'updated_at';

-- Fix the column type (will convert existing timestamps to UTC)
ALTER TABLE system_settings 
ALTER COLUMN updated_at TYPE TIMESTAMP WITH TIME ZONE 
USING updated_at AT TIME ZONE 'UTC';

-- Verify the fix
SELECT column_name, data_type 
FROM information_schema.columns 
WHERE table_name = 'system_settings' AND column_name = 'updated_at';
