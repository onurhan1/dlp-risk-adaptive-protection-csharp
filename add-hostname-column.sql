DO $$ 
BEGIN 
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'incidents' AND column_name = 'host_name') THEN 
        ALTER TABLE incidents ADD COLUMN host_name TEXT; 
    END IF; 
END $$;
