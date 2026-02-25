-- Fix max_matches column by extracting max NumberMatches from violation_triggers JSON
-- Handles multiple casing: NumberMatches, number_matches, numberMatches

DO $$
BEGIN
    RAISE NOTICE 'Updating max_matches from violation_triggers...';
    
    UPDATE incidents
    SET max_matches = COALESCE((
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
    ), 0)
    WHERE violation_triggers IS NOT NULL 
    AND violation_triggers != '' 
    AND violation_triggers != '[]'
    AND jsonb_typeof(violation_triggers::jsonb) = 'array';

    RAISE NOTICE 'Update completed.';
END $$;
