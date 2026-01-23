-- ============================================================================
-- Domain Features Schema Update
-- Adds new columns to nda_domains table
-- ============================================================================

-- 1. Add new columns to nda_domains
ALTER TABLE nda_domains 
ADD COLUMN IF NOT EXISTS istirak_domain BOOLEAN DEFAULT FALSE,
ADD COLUMN IF NOT EXISTS egitim BOOLEAN DEFAULT FALSE,
ADD COLUMN IF NOT EXISTS noter BOOLEAN DEFAULT FALSE,
ADD COLUMN IF NOT EXISTS hukuk BOOLEAN DEFAULT FALSE,
ADD COLUMN IF NOT EXISTS denetim BOOLEAN DEFAULT FALSE,
ADD COLUMN IF NOT EXISTS banka BOOLEAN DEFAULT FALSE;

-- 2. Create index for faster lookups
CREATE INDEX IF NOT EXISTS idx_nda_domains_features ON nda_domains (banka, hukuk, denetim, egitim);

-- 3. Extract unique domains from incidents table and add missing ones
INSERT INTO nda_domains (domain, has_nda, is_unknown, is_personal)
SELECT DISTINCT 
    LOWER(
        CASE 
            -- If contains @, extract domain after @
            WHEN destination LIKE '%@%' THEN 
                SPLIT_PART(
                    -- Handle multiple emails separated by ;
                    SPLIT_PART(destination, ';', 1), 
                    '@', 
                    2
                )
            ELSE destination
        END
    ) as domain,
    false as has_nda,
    true as is_unknown,  -- Mark as unknown for review
    false as is_personal
FROM incidents
WHERE destination IS NOT NULL 
  AND destination != ''
  AND destination LIKE '%@%'
ON CONFLICT (domain) DO NOTHING;

-- 4. Also handle the second email in semicolon-separated destinations
INSERT INTO nda_domains (domain, has_nda, is_unknown, is_personal)
SELECT DISTINCT 
    LOWER(SPLIT_PART(SPLIT_PART(destination, ';', 2), '@', 2)) as domain,
    false as has_nda,
    true as is_unknown,
    false as is_personal
FROM incidents
WHERE destination LIKE '%;%@%'
  AND SPLIT_PART(SPLIT_PART(destination, ';', 2), '@', 2) != ''
ON CONFLICT (domain) DO NOTHING;

-- 5. Verify results
SELECT 
    COUNT(*) as total_domains,
    SUM(CASE WHEN is_unknown THEN 1 ELSE 0 END) as new_unknown,
    SUM(CASE WHEN has_nda THEN 1 ELSE 0 END) as has_nda,
    SUM(CASE WHEN is_personal THEN 1 ELSE 0 END) as is_personal
FROM nda_domains;
