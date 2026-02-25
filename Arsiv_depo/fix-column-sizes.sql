-- Fix: Increase column sizes for long text fields
-- Some incidents have very long file names and policy lists

-- file_name can have many files listed
ALTER TABLE incidents ALTER COLUMN file_name TYPE TEXT;

-- policy can have multiple policy names
ALTER TABLE incidents ALTER COLUMN policy TYPE TEXT;

-- destination can have long URLs
ALTER TABLE incidents ALTER COLUMN destination TYPE VARCHAR(1000);

-- violation_triggers is JSON and can be very long
ALTER TABLE incidents ALTER COLUMN violation_triggers TYPE TEXT;

-- Verify changes
SELECT column_name, data_type, character_maximum_length 
FROM information_schema.columns 
WHERE table_name = 'incidents' 
AND column_name IN ('file_name', 'policy', 'destination', 'violation_triggers');
