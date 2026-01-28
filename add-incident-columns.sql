-- Add missing columns to incidents table if they don't exist
-- FIXES: 42703: column i.rule_name does not exist
DO $$
BEGIN
    -- 1. rule_name (Primary cause of 500 error)
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'incidents' AND column_name = 'rule_name') THEN
        ALTER TABLE incidents ADD COLUMN rule_name TEXT;
    END IF;

    -- 2. team
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'incidents' AND column_name = 'team') THEN
        ALTER TABLE incidents ADD COLUMN team TEXT;
        CREATE INDEX IF NOT EXISTS ix_incidents_team ON incidents(team);
    END IF;

    -- 3. full_name
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'incidents' AND column_name = 'full_name') THEN
        ALTER TABLE incidents ADD COLUMN full_name TEXT;
    END IF;

    -- 4. max_matches
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'incidents' AND column_name = 'max_matches') THEN
        ALTER TABLE incidents ADD COLUMN max_matches INTEGER DEFAULT 0;
    END IF;

    -- 5. Check other potentially missing fields from recent updates
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'incidents' AND column_name = 'action') THEN
        ALTER TABLE incidents ADD COLUMN action TEXT;
        CREATE INDEX IF NOT EXISTS ix_incidents_action ON incidents(action);
    END IF;

    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'incidents' AND column_name = 'destination') THEN
        ALTER TABLE incidents ADD COLUMN destination TEXT;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'incidents' AND column_name = 'file_name') THEN
        ALTER TABLE incidents ADD COLUMN file_name TEXT;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'incidents' AND column_name = 'login_name') THEN
        ALTER TABLE incidents ADD COLUMN login_name TEXT;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'incidents' AND column_name = 'email_address') THEN
        ALTER TABLE incidents ADD COLUMN email_address TEXT;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'incidents' AND column_name = 'violation_triggers') THEN
        ALTER TABLE incidents ADD COLUMN violation_triggers TEXT;
    END IF;

END $$;
