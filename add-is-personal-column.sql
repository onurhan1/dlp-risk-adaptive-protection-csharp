-- Add is_personal column to nda_domains table if it doesn't exist
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_name = 'nda_domains'
        AND column_name = 'is_personal'
    ) THEN
        ALTER TABLE nda_domains ADD COLUMN is_personal BOOLEAN NOT NULL DEFAULT FALSE;
        RAISE NOTICE 'Column is_personal added to nda_domains';
    ELSE
        RAISE NOTICE 'Column is_personal already exists in nda_domains';
    END IF;
END $$;

-- Create index for the new column
CREATE INDEX IF NOT EXISTS idx_nda_domains_is_personal ON nda_domains (is_personal);
