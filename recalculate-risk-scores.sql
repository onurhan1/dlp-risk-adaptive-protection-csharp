-- Risk Score Recalculation Script
-- Formula: (Severity × 2.5) + (RepeatCount × 1.5) + (DataSensitivity × 2) + (MaxMatches × 4)
-- Max: 1000

-- Step 1: First, update max_matches from violation_triggers JSON where it's 0 or null
UPDATE incidents
SET max_matches = COALESCE(
    (
        SELECT MAX((classifier->>'NumberMatches')::int)
        FROM jsonb_array_elements(violation_triggers::jsonb) AS trigger,
             jsonb_array_elements(COALESCE(trigger->'Classifiers', '[]'::jsonb)) AS classifier
        WHERE (classifier->>'NumberMatches') IS NOT NULL
    ),
    0
)
WHERE violation_triggers IS NOT NULL 
  AND violation_triggers != ''
  AND (max_matches IS NULL OR max_matches = 0);

-- Step 2: Recalculate risk_score for ALL incidents
UPDATE incidents
SET risk_score = LEAST(
    1000,
    (
        (COALESCE(severity, 0) * 2.5) +
        (COALESCE(repeat_count, 0) * 1.5) +
        (COALESCE(data_sensitivity, 0) * 2) +
        (COALESCE(max_matches, 0) * 4)
    )::int
);

-- Step 3: Verify the update
SELECT 
    COUNT(*) as total_incidents,
    MIN(risk_score) as min_score,
    MAX(risk_score) as max_score,
    AVG(risk_score)::int as avg_score,
    COUNT(CASE WHEN risk_score >= 500 THEN 1 END) as high_risk_count,
    COUNT(CASE WHEN risk_score >= 250 AND risk_score < 500 THEN 1 END) as medium_risk_count,
    COUNT(CASE WHEN risk_score < 250 THEN 1 END) as low_risk_count
FROM incidents;
