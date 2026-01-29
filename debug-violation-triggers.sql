-- Debug: Check what format the data is in
SELECT id, 
       pg_typeof(violation_triggers) as column_type,
       LEFT(violation_triggers, 300) as vt_preview
FROM incidents 
WHERE violation_triggers IS NOT NULL 
  AND violation_triggers LIKE '%PolicyNameSnake%'
ORDER BY timestamp DESC
LIMIT 3;

-- Check if violation_triggers has escaped quotes (double serialized)
SELECT id,
       CASE 
           WHEN violation_triggers LIKE '"%' THEN 'Double quoted string'
           WHEN violation_triggers LIKE '[%' THEN 'Starts with array'
           WHEN violation_triggers LIKE '{%' THEN 'Starts with object'
           ELSE 'Other format'
       END as format_type,
       LEFT(violation_triggers, 100) as preview
FROM incidents 
WHERE violation_triggers IS NOT NULL 
  AND violation_triggers LIKE '%PolicyNameSnake%'
LIMIT 10;

-- If it's double-serialized (string with escaped quotes), we need to unescape first
-- Check for this pattern: "\"[{...}]\""
SELECT COUNT(*) as double_serialized_count
FROM incidents 
WHERE violation_triggers IS NOT NULL 
  AND (violation_triggers LIKE '"%' OR violation_triggers LIKE '"[%');
