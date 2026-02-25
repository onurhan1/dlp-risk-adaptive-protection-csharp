-- SQL Script to clean violation_triggers JSON in existing database records
-- Removes duplicate field names (PolicyNameSnake/PolicyName, NumberMatchesSnake/NumberMatches etc.)
-- Keeps only essential fields: policy_name, rule_name, classifiers[{classifier_name, number_matches}]

-- First, let's check the current state
SELECT id, 
       LEFT(violation_triggers, 200) as vt_preview,
       jsonb_typeof(violation_triggers::jsonb) as json_type
FROM incidents 
WHERE violation_triggers IS NOT NULL 
  AND violation_triggers LIKE '%PolicyNameSnake%'
LIMIT 5;

-- Update statement to clean violation_triggers
-- This extracts only the essential fields and removes duplicates
-- Added jsonb_typeof checks to handle scalar values safely
UPDATE incidents
SET violation_triggers = (
    SELECT jsonb_agg(
        jsonb_build_object(
            'policy_name', COALESCE(
                trigger->>'policy_name',
                trigger->>'PolicyName',
                trigger->>'PolicyNameSnake'
            ),
            'rule_name', COALESCE(
                trigger->>'rule_name',
                trigger->>'RuleName',
                trigger->>'RuleNameSnake'
            ),
            'classifiers', (
                CASE 
                    -- Check if classifiers exists and is an array
                    WHEN trigger->'classifiers' IS NOT NULL 
                         AND jsonb_typeof(trigger->'classifiers') = 'array' THEN (
                        SELECT jsonb_agg(
                            jsonb_build_object(
                                'classifier_name', COALESCE(
                                    classifier->>'classifier_name',
                                    classifier->>'ClassifierName',
                                    classifier->>'ClassifierNameSnake'
                                ),
                                'number_matches', COALESCE(
                                    (classifier->>'number_matches')::int,
                                    (classifier->>'NumberMatches')::int,
                                    (classifier->>'NumberMatchesSnake')::int,
                                    0
                                )
                            )
                        )
                        FROM jsonb_array_elements(trigger->'classifiers') AS classifier
                    )
                    -- Check if Classifiers (Pascal case) exists and is an array
                    WHEN trigger->'Classifiers' IS NOT NULL 
                         AND jsonb_typeof(trigger->'Classifiers') = 'array' THEN (
                        SELECT jsonb_agg(
                            jsonb_build_object(
                                'classifier_name', COALESCE(
                                    classifier->>'classifier_name',
                                    classifier->>'ClassifierName',
                                    classifier->>'ClassifierNameSnake'
                                ),
                                'number_matches', COALESCE(
                                    (classifier->>'number_matches')::int,
                                    (classifier->>'NumberMatches')::int,
                                    (classifier->>'NumberMatchesSnake')::int,
                                    0
                                )
                            )
                        )
                        FROM jsonb_array_elements(trigger->'Classifiers') AS classifier
                    )
                    -- Default to empty array if classifiers is not an array
                    ELSE '[]'::jsonb
                END
            )
        )
    )::text
    FROM jsonb_array_elements(violation_triggers::jsonb) AS trigger
)
WHERE violation_triggers IS NOT NULL
  AND violation_triggers LIKE '%PolicyNameSnake%'
  AND jsonb_typeof(violation_triggers::jsonb) = 'array';

-- Verify the cleanup
SELECT id, 
       LEFT(violation_triggers, 200) as vt_preview
FROM incidents 
WHERE violation_triggers IS NOT NULL 
ORDER BY timestamp DESC
LIMIT 5;

-- Count how many records were cleaned
SELECT COUNT(*) as remaining_old_format
FROM incidents 
WHERE violation_triggers IS NOT NULL 
  AND violation_triggers LIKE '%PolicyNameSnake%';
