-- 1. Add new columns to user_daily_risk_scores if they don't exist
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'user_daily_risk_scores' AND column_name = 'team') THEN
        ALTER TABLE user_daily_risk_scores ADD COLUMN team TEXT;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'user_daily_risk_scores' AND column_name = 'full_name') THEN
        ALTER TABLE user_daily_risk_scores ADD COLUMN full_name TEXT;
    END IF;
END $$;

-- 2. Backfill/Recalculate daily scores for ALL historical incidents
-- This query aggregates risks per user per day and inserts/updates the daily summary table
-- First pass: Insert aggregated scores without team/full_name
INSERT INTO user_daily_risk_scores (
    user_email, 
    date, 
    daily_risk_score, 
    incident_count, 
    max_risk_score, 
    avg_risk_score, 
    created_at
)
SELECT 
    i.user_email,
    i.timestamp::date as date,
    -- Normalized daily score (1-100 scale)
    -- Formula: MIN(100, (Avg/500*50) + (Max/500*30) + MIN(20, LOG10(Count+1)*10))
    LEAST(100, 
        (COALESCE(AVG(i.risk_score), 0) / 500.0 * 50) + 
        (COALESCE(MAX(i.risk_score), 0) / 500.0 * 30) + 
        LEAST(20, LOG(COUNT(*) + 1) * 10)
    ) as daily_risk_score,
    COUNT(*) as incident_count,
    COALESCE(MAX(i.risk_score), 0) as max_risk_score,
    COALESCE(AVG(i.risk_score), 0) as avg_risk_score,
    NOW() as created_at
FROM incidents i
GROUP BY i.user_email, i.timestamp::date
ON CONFLICT (user_email, date) 
DO UPDATE SET
    daily_risk_score = EXCLUDED.daily_risk_score,
    incident_count = EXCLUDED.incident_count,
    max_risk_score = EXCLUDED.max_risk_score,
    avg_risk_score = EXCLUDED.avg_risk_score;

-- 2b. Fill team/full_name from incidents for each user+date combination
UPDATE user_daily_risk_scores u
SET 
    team = sub.team,
    full_name = sub.full_name
FROM (
    SELECT DISTINCT ON (user_email, timestamp::date)
        user_email,
        timestamp::date as date,
        department as team,
        full_name
    FROM incidents
    WHERE department IS NOT NULL OR full_name IS NOT NULL
    ORDER BY user_email, timestamp::date, timestamp DESC
) sub
WHERE u.user_email = sub.user_email 
  AND u.date = sub.date
  AND (u.team IS NULL OR u.full_name IS NULL);

-- 3. If there are still missing team/full_name (e.g. some days had nulls), try to fill from ANY record for that user
UPDATE user_daily_risk_scores u
SET 
    team = COALESCE(u.team, info.team),
    full_name = COALESCE(u.full_name, info.full_name)
FROM (
    SELECT DISTINCT ON (user_email) 
        user_email, 
        department as team, 
        full_name
    FROM incidents
    WHERE department IS NOT NULL OR full_name IS NOT NULL
    ORDER BY user_email, timestamp DESC
) info
WHERE u.user_email = info.user_email 
  AND (u.team IS NULL OR u.full_name IS NULL);

-- Verification
SELECT COUNT(*) as total_daily_records, 
       COUNT(team) as with_team, 
       COUNT(full_name) as with_full_name 
FROM user_daily_risk_scores;
