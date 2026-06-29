-- ============================================
-- FIX NULL RISK SCORES
-- Tarih: 6 Mart 2026 sonrası ve 27 Eylül 2025 öncesi
-- ============================================
-- Risk Score Formula:
-- RiskScore = MIN(100, ((MaxMatchesTier × ChannelMultiplier) + DestinationScore) × ActionMultiplier)
-- ============================================

BEGIN;

-- Tablolar "dlp" şemasına taşındı; şemasız referanslar dlp'ye çözümlensin (yedek tabloları da dlp'de oluşur).
SET search_path TO dlp, public;

-- 1. BACKUP (CRITICAL!)
CREATE TABLE IF NOT EXISTS incidents_backup_null_scores_20260313 AS 
SELECT * FROM incidents WHERE risk_score IS NULL;

-- 2. CALCULATE MISSING RISK SCORES
-- Formula: ((MaxMatchesTier × ChannelMultiplier) + DestinationScore) × ActionMultiplier

UPDATE incidents 
SET risk_score = LEAST(100, ROUND(
    -- ((MaxMatchesTier × ChannelMultiplier) + DestinationScore) × ActionMultiplier
    (
        -- MaxMatchesTier logic
        (CASE 
            WHEN COALESCE(max_matches, 0) <= 15 THEN 7
            WHEN max_matches <= 30 THEN 14
            WHEN max_matches <= 50 THEN 25
            WHEN max_matches <= 100 THEN 40
            WHEN max_matches <= 250 THEN 55
            WHEN max_matches <= 500 THEN 70
            ELSE 85
        END)
        * 
        -- ChannelMultiplier logic
        (CASE 
            WHEN UPPER(COALESCE(channel, '')) LIKE '%ENDPOINT_LAN%' THEN 0.2
            WHEN UPPER(COALESCE(channel, '')) LIKE '%ENDPOINT_PRINTING%' 
                 OR UPPER(COALESCE(channel, '')) LIKE '%PRINTER%' THEN 0.4
            ELSE 1.0
        END)
        +
        -- DestinationScore logic
        (CASE 
            -- SPL = 1 (bizim ortamı)
            WHEN LOWER(COALESCE(destination, '')) LIKE '%spl%' THEN 1
            -- Printer = 3
            WHEN UPPER(COALESCE(channel, '')) LIKE '%PRINT%' 
                 OR LOWER(COALESCE(destination, '')) LIKE '%print%' THEN 3
            -- Personal emails = 10
            WHEN LOWER(COALESCE(destination, '')) LIKE '%gmail.com%'
                 OR LOWER(COALESCE(destination, '')) LIKE '%hotmail.com%'
                 OR LOWER(COALESCE(destination, '')) LIKE '%outlook.com%'
                 OR LOWER(COALESCE(destination, '')) LIKE '%yahoo.com%'
                 OR LOWER(COALESCE(destination, '')) LIKE '%icloud.com%'
                 OR LOWER(COALESCE(destination, '')) LIKE '%windowslive.com%'
                 OR LOWER(COALESCE(destination, '')) LIKE '%mynet.com%' THEN 10
            -- Default/Unknown = 5
            ELSE 5
        END)
    )
    *
    -- ActionMultiplier logic
    (CASE 
        WHEN UPPER(COALESCE(action, '')) IN ('BLOCK', 'BLOCKED') THEN 1.0
        WHEN UPPER(COALESCE(action, '')) IN ('QUARANTINE', 'QUARANTINED') THEN 1.0
        WHEN UPPER(COALESCE(action, '')) IN ('AUTHORIZED', 'PERMIT') THEN 0.2
        WHEN UPPER(COALESCE(action, '')) = 'RELEASED' THEN 0.0
        ELSE 0.2  -- default for null/unknown
    END)
)::INTEGER)
WHERE risk_score IS NULL;

-- 3. SET risk_level for newly calculated risks
UPDATE incidents 
SET "RiskLevel" = CASE 
    WHEN risk_score <= 25 THEN 'Low'
    WHEN risk_score <= 50 THEN 'Medium'
    WHEN risk_score <= 75 THEN 'High'
    ELSE 'Critical'
END
WHERE risk_score IS NOT NULL AND "RiskLevel" IS NULL;

-- 4. VERIFY - Check how many were fixed
SELECT 
    'incidents - AFTER FIX' as status,
    COUNT(*) as total_records,
    COUNT(*) FILTER (WHERE risk_score IS NULL) as still_null,
    COUNT(*) FILTER (WHERE risk_score IS NOT NULL) as now_calculated,
    MIN(risk_score) as min_score,
    MAX(risk_score) as max_score,
    ROUND(AVG(risk_score)::numeric, 2) as avg_score
FROM incidents;

-- 5. UPDATE user_daily_risk_scores based on recalculated incidents
-- First, find all dates that were affected
WITH affected_dates AS (
    SELECT DISTINCT DATE(timestamp) as date FROM incidents_backup_null_scores_20260313
),
daily_stats AS (
    SELECT 
        COALESCE(i.user_email, i.login_name, 'unknown') as user_email,
        DATE(i.timestamp) as date,
        COUNT(*) as incident_count,
        ROUND(AVG(i.risk_score)::numeric, 2) as avg_risk_score,
        MAX(i.risk_score) as max_risk_score,
        SUM(CASE WHEN UPPER(i.action) IN ('BLOCK', 'BLOCKED') THEN 1 ELSE 0 END) as block_count,
        SUM(CASE WHEN UPPER(i.action) IN ('PERMIT', 'PERMITTED', 'AUTHORIZED') THEN 1 ELSE 0 END) as permit_count,
        SUM(CASE WHEN UPPER(i.action) IN ('QUARANTINE', 'QUARANTINED') THEN 1 ELSE 0 END) as quarantine_count,
        SUM(CASE WHEN UPPER(i.action) IN ('RELEASE', 'RELEASED') THEN 1 ELSE 0 END) as released_count,
        ROUND(MAX(i.max_matches)::numeric, 2) as max_max_matches,
        ROUND(AVG(i.max_matches)::numeric, 2) as avg_max_matches
    FROM incidents i
    INNER JOIN affected_dates ad ON DATE(i.timestamp) = ad.date
    GROUP BY COALESCE(i.user_email, i.login_name, 'unknown'), DATE(i.timestamp)
)
INSERT INTO user_daily_risk_scores (
    user_email, date, daily_risk_score, incident_count, 
    max_risk_score, avg_risk_score, block_count, permit_count, 
    quarantine_count, released_count, max_max_matches, avg_max_matches,
    created_at
)
SELECT 
    ds.user_email,
    ds.date,
    LEAST(100, ROUND((
        (COALESCE(ds.avg_risk_score, 0) * 0.50) +
        (COALESCE(ds.max_risk_score, 0) * 0.30) +
        LEAST(20, LN(COALESCE(ds.incident_count, 0) + 1) * 10)
    )::numeric, 2)) as daily_risk_score,
    ds.incident_count,
    ds.max_risk_score,
    ds.avg_risk_score,
    ds.block_count,
    ds.permit_count,
    ds.quarantine_count,
    ds.released_count,
    ds.max_max_matches,
    ds.avg_max_matches,
    NOW() as created_at
FROM daily_stats ds
ON CONFLICT (user_email, date) 
DO UPDATE SET
    daily_risk_score = EXCLUDED.daily_risk_score,
    incident_count = EXCLUDED.incident_count,
    max_risk_score = EXCLUDED.max_risk_score,
    avg_risk_score = EXCLUDED.avg_risk_score,
    block_count = EXCLUDED.block_count,
    permit_count = EXCLUDED.permit_count,
    quarantine_count = EXCLUDED.quarantine_count,
    released_count = EXCLUDED.released_count,
    max_max_matches = EXCLUDED.max_max_matches,
    avg_max_matches = EXCLUDED.avg_max_matches;

-- 6. VERIFY daily scores were updated
SELECT 
    'user_daily_risk_scores - AFTER UPDATE' as status,
    COUNT(*) as total_users,
    ROUND(AVG(daily_risk_score)::numeric, 2) as avg_daily_score,
    MAX(daily_risk_score) as max_daily_score,
    MIN(daily_risk_score) as min_daily_score
FROM user_daily_risk_scores;

-- 7. DISTRIBUTION CHECK
SELECT 
    'Risk Distribution' as metric,
    CASE 
        WHEN risk_score <= 25 THEN '🟢 Low (0-25)'
        WHEN risk_score <= 50 THEN '🟡 Medium (26-50)'
        WHEN risk_score <= 75 THEN '🟠 High (51-75)'
        ELSE '🔴 Critical (76-100)'
    END as risk_level,
    COUNT(*) as incident_count,
    ROUND(COUNT(*) * 100.0 / (SELECT COUNT(*) FROM incidents WHERE risk_score IS NOT NULL), 2) as percentage
FROM incidents
WHERE risk_score IS NOT NULL
GROUP BY CASE 
    WHEN risk_score <= 25 THEN '🟢 Low (0-25)'
    WHEN risk_score <= 50 THEN '🟡 Medium (26-50)'
    WHEN risk_score <= 75 THEN '🟠 High (51-75)'
    ELSE '🔴 Critical (76-100)'
END
ORDER BY risk_level;

-- 8. NULL CHECK - ensure no NULLs remain
SELECT 
    'Final Null Check' as check_name,
    COUNT(*) as null_count
FROM incidents
WHERE risk_score IS NULL;

COMMIT;
