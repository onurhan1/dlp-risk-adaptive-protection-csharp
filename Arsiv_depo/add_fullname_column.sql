-- Migration: AddFullNameToIncidents
-- Description: Adds full_name column to incidents table for storing user real names (from Manager field)

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 
        FROM information_schema.columns 
        WHERE table_name = 'incidents' 
        AND column_name = 'full_name'
    ) THEN
        ALTER TABLE incidents ADD COLUMN full_name text;
    END IF;
END $$;
