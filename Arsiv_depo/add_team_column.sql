-- Migration: AddTeamToIncidents
-- Description: Adds team column to incidents table for storing department/team info (from Manager field part 2)

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 
        FROM information_schema.columns 
        WHERE table_name = 'incidents' 
        AND column_name = 'team'
    ) THEN
        ALTER TABLE incidents ADD COLUMN team text;
    END IF;
END $$;
