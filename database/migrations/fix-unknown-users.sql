-- ============================================================================
-- FIX: Unknown User Bug
-- Tarih: 2026-03-18
-- Açıklama: 
--   1. user_daily_risk_scores tablosuna email_address kolonu ekler
--   2. incidents tablosundaki email_address/full_name bilgisini backfill eder
--   3. user_email = 'unknown' olan kayıtları düzeltir (email_address veya login_name ile)
--   4. user_daily_risk_scores tablosundaki unknown kayıtları düzeltir
-- ============================================================================

BEGIN;

-- ─── ADIM 1: user_daily_risk_scores tablosuna email_address kolonu ekle ───────
ALTER TABLE user_daily_risk_scores 
ADD COLUMN IF NOT EXISTS email_address VARCHAR(255);

-- ─── ADIM 2: Mevcut daily score kayıtlarını incidents tablosundan backfill et ──
-- Her user_email + date kombinasyonu için incidents tablosundaki en güncel email_address'i al
UPDATE user_daily_risk_scores udrs
SET email_address = sub.email_address
FROM (
    SELECT 
        i.user_email,
        DATE(i."timestamp") AS incident_date,
        MAX(i.email_address) AS email_address
    FROM incidents i
    WHERE i.email_address IS NOT NULL 
      AND i.email_address != ''
    GROUP BY i.user_email, DATE(i."timestamp")
) sub
WHERE udrs.user_email = sub.user_email
  AND udrs.date = sub.incident_date
  AND (udrs.email_address IS NULL OR udrs.email_address = '');

-- ─── ADIM 3: full_name boş olan daily score kayıtlarını da backfill et ────────
UPDATE user_daily_risk_scores udrs
SET full_name = sub.full_name
FROM (
    SELECT 
        i.user_email,
        DATE(i."timestamp") AS incident_date,
        MAX(i.full_name) AS full_name
    FROM incidents i
    WHERE i.full_name IS NOT NULL 
      AND i.full_name != ''
    GROUP BY i.user_email, DATE(i."timestamp")
) sub
WHERE udrs.user_email = sub.user_email
  AND udrs.date = sub.incident_date
  AND (udrs.full_name IS NULL OR udrs.full_name = '');

-- ─── ADIM 4: incidents tablosunda user_email = 'unknown' olanları düzelt ──────
-- Öncelik: email_address → login_name (domain prefix kaldırılmış hali)
-- 4a: email_address doluysa onu kullan
UPDATE incidents
SET user_email = email_address
WHERE user_email = 'unknown'
  AND email_address IS NOT NULL 
  AND email_address != '';

-- 4b: Hâlâ unknown olanlar için login_name kullan
UPDATE incidents
SET user_email = login_name
WHERE user_email = 'unknown'
  AND login_name IS NOT NULL 
  AND login_name != '';

-- ─── ADIM 5: user_daily_risk_scores tablosundaki unknown kayıtları düzelt ─────
-- 5a: Önce email_address doluysa onu user_email olarak kullan
UPDATE user_daily_risk_scores
SET user_email = email_address
WHERE user_email = 'unknown'
  AND email_address IS NOT NULL 
  AND email_address != '';

-- 5b: Hâlâ unknown olanlar için full_name kullan
UPDATE user_daily_risk_scores
SET user_email = full_name
WHERE user_email = 'unknown'
  AND full_name IS NOT NULL 
  AND full_name != '';

-- ─── ADIM 6: Kontrol sorguları ────────────────────────────────────────────────
-- Aşağıdaki sorgularla sonuçları kontrol edebilirsiniz:

-- Hâlâ unknown olan incident sayısı
-- SELECT COUNT(*) AS remaining_unknown_incidents 
-- FROM incidents WHERE user_email = 'unknown';

-- Hâlâ unknown olan daily score sayısı  
-- SELECT COUNT(*) AS remaining_unknown_daily_scores 
-- FROM user_daily_risk_scores WHERE user_email = 'unknown';

-- email_address backfill edilen daily score sayısı
-- SELECT COUNT(*) AS daily_scores_with_email 
-- FROM user_daily_risk_scores WHERE email_address IS NOT NULL AND email_address != '';

COMMIT;
