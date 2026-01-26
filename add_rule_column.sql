-- Migration: AddRuleToIncidents
-- Description: Adds rule column to incidents table for storing rule name from ViolationTriggers

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 
        FROM information_schema.columns 
        WHERE table_name = 'incidents' 
        AND column_name = 'rule'
    ) THEN
        ALTER TABLE incidents ADD COLUMN rule text;
    END IF;
END $$;
