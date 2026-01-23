-- Fix MaxMatches column from ViolationTriggers JSON
-- This script calculates and updates max_matches for all incidents where it is 0

-- Step 1: Check current state
SELECT 
    'Before Update' as status,
    COUNT(*) as total_incidents,
    SUM(CASE WHEN max_matches = 0 THEN 1 ELSE 0 END) as zero_max_matches,
    SUM(CASE WHEN max_matches > 0 THEN 1 ELSE 0 END) as has_max_matches
FROM incidents;

-- Step 2: Show records that need fixing (sorted by date - newest first)
SELECT 
    id, 
    timestamp,
    user_email,
    max_matches as current_value,
    LEFT(violation_triggers, 100) as json_preview
FROM incidents
WHERE max_matches = 0 
  AND violation_triggers IS NOT NULL 
  AND violation_triggers != ''
  AND violation_triggers != '[]'
  AND violation_triggers LIKE '%NumberMatches%'
ORDER BY timestamp DESC
LIMIT 20;

-- Step 3: Update max_matches from violation_triggers JSON where max_matches is 0
UPDATE incidents
SET max_matches = COALESCE(
    (
        SELECT MAX((classifier->>'NumberMatches')::int)
        FROM jsonb_array_elements(violation_triggers::jsonb) AS trigger,
             jsonb_array_elements(COALESCE(trigger->'Classifiers', '[]'::jsonb)) AS classifier
        WHERE classifier->>'NumberMatches' IS NOT NULL
          AND (classifier->>'NumberMatches')::int > 0
    ),
    0
)
WHERE max_matches = 0 
  AND violation_triggers IS NOT NULL 
  AND violation_triggers != ''
  AND violation_triggers != '[]'
  AND violation_triggers LIKE '%NumberMatches%';

-- Step 4: Verify the update
SELECT 
    'After Update' as status,
    COUNT(*) as total_incidents,
    SUM(CASE WHEN max_matches = 0 THEN 1 ELSE 0 END) as zero_max_matches,
    SUM(CASE WHEN max_matches > 0 THEN 1 ELSE 0 END) as has_max_matches
FROM incidents;

-- Step 5: Show updated records by date (newest first)
SELECT id, timestamp, user_email, max_matches, channel, action
FROM incidents
WHERE max_matches > 0
ORDER BY timestamp DESC
LIMIT 20;

-- Step 6: Show updated records by max_matches (highest first)
SELECT id, timestamp, user_email, max_matches, channel, action
FROM incidents
WHERE max_matches > 0
ORDER BY max_matches DESC
LIMIT 20;
