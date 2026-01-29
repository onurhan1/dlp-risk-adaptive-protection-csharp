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
                FROM jsonb_array_elements(
                    CASE 
                        WHEN trigger->'classifiers' IS NOT NULL THEN trigger->'classifiers'
                        WHEN trigger->'Classifiers' IS NOT NULL THEN trigger->'Classifiers'
                        ELSE '[]'::jsonb
                    END
                ) AS classifier
            )
        )
    )::text
    FROM jsonb_array_elements(violation_triggers::jsonb) AS trigger
)
WHERE violation_triggers IS NOT NULL
  AND violation_triggers LIKE '%PolicyNameSnake%';

-- Verify the cleanup
SELECT id, 
       LEFT(violation_triggers, 200) as vt_preview
FROM incidents 
WHERE violation_triggers IS NOT NULL 
ORDER BY timestamp DESC
LIMIT 5;
