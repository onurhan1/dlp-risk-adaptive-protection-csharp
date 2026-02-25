-- Rename PascalCase/CamelCase columns to snake_case to match Entity Framework configuration
-- PostgreSQL requires quoted identifiers for mixed-case column names if they were created that way.

DO $$
BEGIN
    -- 1. RuleName -> rule_name
    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'incidents' AND column_name = 'RuleName') THEN
        ALTER TABLE incidents RENAME COLUMN "RuleName" TO rule_name;
    ELSIF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'incidents' AND column_name = 'rulename') THEN
        ALTER TABLE incidents RENAME COLUMN "rulename" TO rule_name;
    END IF;

    -- 2. Team -> team
    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'incidents' AND column_name = 'Team') THEN
        ALTER TABLE incidents RENAME COLUMN "Team" TO team;
    END IF;

    -- 3. FullName -> full_name
    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'incidents' AND column_name = 'FullName') THEN
        ALTER TABLE incidents RENAME COLUMN "FullName" TO full_name;
    END IF;

     -- 4. Action -> action (Just in case)
    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'incidents' AND column_name = 'Action') THEN
        ALTER TABLE incidents RENAME COLUMN "Action" TO action;
    END IF;

    -- 5. Destination -> destination
    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'incidents' AND column_name = 'Destination') THEN
        ALTER TABLE incidents RENAME COLUMN "Destination" TO destination;
    END IF;

    -- 6. MaxMatches -> max_matches
    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'incidents' AND column_name = 'MaxMatches') THEN
         ALTER TABLE incidents RENAME COLUMN "MaxMatches" TO max_matches;
    END IF;

     -- 7. ViolationTriggers -> violation_triggers
    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'incidents' AND column_name = 'ViolationTriggers') THEN
         ALTER TABLE incidents RENAME COLUMN "ViolationTriggers" TO violation_triggers;
    END IF;

    -- 8. LoginName -> login_name
    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'incidents' AND column_name = 'LoginName') THEN
         ALTER TABLE incidents RENAME COLUMN "LoginName" TO login_name;
    END IF;

    -- 9. EmailAddress -> email_address
    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'incidents' AND column_name = 'EmailAddress') THEN
         ALTER TABLE incidents RENAME COLUMN "EmailAddress" TO email_address;
    END IF;

    -- 10. FileName -> file_name
    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'incidents' AND column_name = 'FileName') THEN
         ALTER TABLE incidents RENAME COLUMN "FileName" TO file_name;
    END IF;

END $$;
