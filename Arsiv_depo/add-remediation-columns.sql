-- Add remediation columns to incidents table
-- Run this script in PostgreSQL

-- Add remediation columns
ALTER TABLE incidents 
ADD COLUMN IF NOT EXISTS is_remediated BOOLEAN DEFAULT FALSE;

ALTER TABLE incidents 
ADD COLUMN IF NOT EXISTS remediated_at TIMESTAMP;

ALTER TABLE incidents 
ADD COLUMN IF NOT EXISTS remediated_by VARCHAR(255);

ALTER TABLE incidents 
ADD COLUMN IF NOT EXISTS remediation_action VARCHAR(50);

ALTER TABLE incidents 
ADD COLUMN IF NOT EXISTS remediation_notes TEXT;

-- Create index for faster remediation queries
CREATE INDEX IF NOT EXISTS idx_incidents_is_remediated ON incidents(is_remediated);

-- Verify the changes
SELECT column_name, data_type, column_default
FROM information_schema.columns 
WHERE table_name = 'incidents' 
AND column_name LIKE 'remediat%'
ORDER BY ordinal_position;
