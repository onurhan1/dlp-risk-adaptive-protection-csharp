-- Debug script to test JSON extraction logic on a single row
-- Replace 12345 with a valid incident ID that has violation_triggers

SELECT 
    id, 
    violation_triggers,
    jsonb_typeof(violation_triggers::jsonb) as json_type,
    (
        SELECT MAX(
            GREATEST(
                COALESCE((classifier->>'NumberMatches')::int, 0),
                COALESCE((classifier->>'NumberMatchesSnake')::int, 0),
                COALESCE((classifier->>'number_matches')::int, 0),
                COALESCE((classifier->>'numberMatches')::int, 0)
            )
        )
        FROM jsonb_array_elements(violation_triggers::jsonb) AS trigger
        CROSS JOIN LATERAL jsonb_array_elements(
            CASE 
                WHEN jsonb_typeof(trigger->'Classifiers') = 'array' THEN trigger->'Classifiers'
                WHEN jsonb_typeof(trigger->'classifiers') = 'array' THEN trigger->'classifiers'
                ELSE '[]'::jsonb 
            END
        ) AS classifier
    ) as calculated_max_matches,
    (
        SELECT jsonb_agg(classifier)
        FROM jsonb_array_elements(violation_triggers::jsonb) AS trigger
        CROSS JOIN LATERAL jsonb_array_elements(
            CASE 
                WHEN jsonb_typeof(trigger->'Classifiers') = 'array' THEN trigger->'Classifiers'
                WHEN jsonb_typeof(trigger->'classifiers') = 'array' THEN trigger->'classifiers'
                ELSE '[]'::jsonb 
            END
        ) AS classifier
    ) as raw_classifiers
FROM incidents
WHERE violation_triggers IS NOT NULL 
AND violation_triggers != ''
AND violation_triggers != '[]'
AND violation_triggers::text LIKE '%NumberMatchesSnake%' 
LIMIT 3;
