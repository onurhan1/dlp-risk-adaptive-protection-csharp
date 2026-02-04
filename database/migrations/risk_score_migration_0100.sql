-- Risk Score Migration Script
-- Converts risk scores from 0-1000 scale to 0-100 scale using NEW FORMULA
-- Date: 2026-02-04
-- WARNING: BACKUP YOUR DATABASE BEFORE RUNNING THIS SCRIPT!

-- ============================================
-- 1. BACKUP TABLES (REQUIRED!)
-- ============================================

-- Uncomment to create backup:
-- CREATE TABLE incidents_backup_20260204 AS SELECT * FROM incidents;
-- CREATE TABLE user_daily_risk_scores_backup_20260204 AS SELECT * FROM user_daily_risk_scores;

-- ============================================
-- 2. RECALCULATE incidents TABLE WITH NEW FORMULA
-- ============================================

-- New Formula:
-- IncidentScore = ((MaxMatchesTier * ChannelMultiplier) + DestinationScore) * ActionMultiplier
-- MaxMatchesTier: max 85
-- DestinationScore: max 15
-- Total: max 100

UPDATE incidents 
SET risk_score = LEAST(100, ROUND((
    -- MaxMatchesTier (max 85)
    CASE 
        WHEN COALESCE(max_matches, 0) <= 15 THEN 7
        WHEN max_matches <= 30 THEN 14
        WHEN max_matches <= 50 THEN 25
        WHEN max_matches <= 100 THEN 40
        WHEN max_matches <= 250 THEN 55
        WHEN max_matches <= 500 THEN 70
        ELSE 85
    END 
    * 
    -- ChannelMultiplier
    CASE 
        WHEN UPPER(COALESCE(channel, '')) LIKE '%ENDPOINT_LAN%' THEN 0.2
        WHEN UPPER(COALESCE(channel, '')) LIKE '%ENDPOINT_PRINTING%' 
             OR UPPER(COALESCE(channel, '')) LIKE '%PRINTER%' THEN 0.4
        ELSE 1.0
    END
    +
    -- DestinationScore (max 15)
    CASE 
        -- Personal email domains (highest risk)
        WHEN LOWER(COALESCE(destination, '')) LIKE '%gmail%' 
             OR LOWER(COALESCE(destination, '')) LIKE '%hotmail%' 
             OR LOWER(COALESCE(destination, '')) LIKE '%yahoo%'
             OR LOWER(COALESCE(destination, '')) LIKE '%outlook.com%'
             OR LOWER(COALESCE(destination, '')) LIKE '%yandex%' THEN 15
        -- Printer
        WHEN UPPER(COALESCE(channel, '')) LIKE '%PRINT%' THEN 5
        -- Unknown/default
        ELSE 8
    END
) *
    -- ActionMultiplier
    CASE 
        WHEN UPPER(COALESCE(action, '')) IN ('BLOCK', 'BLOCKED', 'QUARANTINE', 'QUARANTINED') THEN 1.0
        WHEN UPPER(COALESCE(action, '')) IN ('AUTHORIZED', 'PERMIT') THEN 0.2
        WHEN UPPER(COALESCE(action, '')) = 'RELEASED' THEN 0.0
        ELSE 0.2
    END
));

-- Verify incidents update
SELECT 
    'incidents - AFTER' as table_name,
    COUNT(*) as total_records,
    MIN(risk_score) as min_score,
    MAX(risk_score) as max_score,
    ROUND(AVG(risk_score)::numeric, 2) as avg_score
FROM incidents;

-- ============================================
-- 3. RECALCULATE user_daily_risk_scores TABLE
-- ============================================

-- First, recalculate avg_risk_score and max_risk_score from incidents
UPDATE user_daily_risk_scores udrs
SET 
    avg_risk_score = sub.avg_score,
    max_risk_score = sub.max_score,
    incident_count = sub.cnt
FROM (
    SELECT 
        login_name as user_email,
        DATE(timestamp) as date,
        ROUND(AVG(risk_score)::numeric, 2) as avg_score,
        MAX(risk_score) as max_score,
        COUNT(*) as cnt
    FROM incidents
    GROUP BY login_name, DATE(timestamp)
) sub
WHERE udrs.user_email = sub.user_email 
  AND udrs.date = sub.date;

-- Verify avg/max update
SELECT 
    'user_daily_risk_scores - avg/max updated' as status,
    COUNT(*) as total_records,
    ROUND(AVG(avg_risk_score)::numeric, 2) as avg_avg,
    MAX(max_risk_score) as max_max
FROM user_daily_risk_scores;

-- ============================================
-- 4. RECALCULATE daily_risk_score WITH NEW FORMULA
-- ============================================

-- New Daily Formula:
-- DailyScore = (Avg * 0.50) + (Max * 0.30) + MIN(20, LOG10(Count+1) * 10)

UPDATE user_daily_risk_scores 
SET daily_risk_score = LEAST(100, ROUND(
    (COALESCE(avg_risk_score, 0) * 0.50) +
    (COALESCE(max_risk_score, 0) * 0.30) +
    LEAST(20, LOG(COALESCE(incident_count, 0) + 1) * 10)
, 2));

-- Verify daily_risk_score update
SELECT 
    'daily_risk_score - RECALCULATED' as status,
    COUNT(*) as total_records,
    MIN(daily_risk_score) as min_daily,
    MAX(daily_risk_score) as max_daily,
    ROUND(AVG(daily_risk_score)::numeric, 2) as avg_daily
FROM user_daily_risk_scores;

-- ============================================
-- 5. DISTRIBUTION CHECK
-- ============================================

SELECT 
    CASE 
        WHEN daily_risk_score <= 25 THEN '🟢 Low (0-25)'
        WHEN daily_risk_score <= 50 THEN '🟡 Medium (26-50)'
        WHEN daily_risk_score <= 75 THEN '🟠 High (51-75)'
        ELSE '🔴 Critical (76-100)'
    END as risk_level,
    COUNT(*) as user_count,
    ROUND(COUNT(*) * 100.0 / (SELECT COUNT(*) FROM user_daily_risk_scores), 2) as percentage
FROM user_daily_risk_scores
GROUP BY 1
ORDER BY 1;

-- ============================================
-- 6. COMPLETION
-- ============================================

SELECT 
    'Migration Complete' as status,
    NOW() as completed_at,
    '0-100 scale with new formula' as description;
