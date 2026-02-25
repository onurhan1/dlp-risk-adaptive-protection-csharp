-- Fix double-serialized violation_triggers
-- Pattern: """[{\"PolicyNameSnake\":...}]""" 
-- Need to: 1) Remove outer quotes 2) Replace \" with "

-- Step 1: Count double-serialized records
SELECT COUNT(*) as double_serialized_count
FROM incidents 
WHERE violation_triggers LIKE '%PolicyNameSnake%'
  AND violation_triggers LIKE '%\"%';

-- Step 2: Preview what the unescape will look like
SELECT id, 
       LEFT(violation_triggers, 100) as original,
       LEFT(
           REPLACE(
               TRIM(BOTH '"' FROM violation_triggers),
               '\"', '"'
           ), 
           100
       ) as unescaped
FROM incidents 
WHERE violation_triggers LIKE '%PolicyNameSnake%'
  AND violation_triggers LIKE '%\"%'
LIMIT 3;

-- Step 3: Unescape and clean double-serialized records
UPDATE incidents
SET violation_triggers = (
    SELECT jsonb_agg(
        jsonb_build_object(
            'policy_name', COALESCE(trigger->>'policy_name', trigger->>'PolicyName', trigger->>'PolicyNameSnake'),
            'rule_name', COALESCE(trigger->>'rule_name', trigger->>'RuleName', trigger->>'RuleNameSnake'),
            'classifiers', (
                CASE 
                    WHEN trigger->'classifiers' IS NOT NULL AND jsonb_typeof(trigger->'classifiers') = 'array' THEN (
                        SELECT jsonb_agg(jsonb_build_object(
                            'classifier_name', COALESCE(c->>'classifier_name', c->>'ClassifierName', c->>'ClassifierNameSnake'),
                            'number_matches', COALESCE((c->>'number_matches')::int, (c->>'NumberMatches')::int, (c->>'NumberMatchesSnake')::int, 0)
                        )) FROM jsonb_array_elements(trigger->'classifiers') AS c
                    )
                    WHEN trigger->'Classifiers' IS NOT NULL AND jsonb_typeof(trigger->'Classifiers') = 'array' THEN (
                        SELECT jsonb_agg(jsonb_build_object(
                            'classifier_name', COALESCE(c->>'classifier_name', c->>'ClassifierName', c->>'ClassifierNameSnake'),
                            'number_matches', COALESCE((c->>'number_matches')::int, (c->>'NumberMatches')::int, (c->>'NumberMatchesSnake')::int, 0)
                        )) FROM jsonb_array_elements(trigger->'Classifiers') AS c
                    )
                    ELSE '[]'::jsonb
                END
            )
        )
    )::text
    FROM jsonb_array_elements(
        -- Unescape: remove outer quotes and replace \" with "
        REPLACE(
            TRIM(BOTH '"' FROM violation_triggers),
            '\"', '"'
        )::jsonb
    ) AS trigger
)
WHERE violation_triggers IS NOT NULL
  AND violation_triggers LIKE '%PolicyNameSnake%'
  AND violation_triggers LIKE '%\"%';

-- Step 4: Verify remaining
SELECT COUNT(*) as remaining_old_format
FROM incidents 
WHERE violation_triggers LIKE '%PolicyNameSnake%';

-- Step 5: Show sample of remaining (if any)
SELECT id, LEFT(violation_triggers, 200) as sample
FROM incidents 
WHERE violation_triggers LIKE '%PolicyNameSnake%'
LIMIT 3;
