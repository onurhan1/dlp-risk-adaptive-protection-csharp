-- ============================================================================
-- DLP Risk Analyzer - Duplicate Incident Temizleme Scripti
-- Bu script duplicate incident kayıtlarını temizler
-- PostgreSQL/psql'de çalıştırın
-- ============================================================================

-- 1. Önce duplicate'leri görüntüle
SELECT 
    user_email, 
    timestamp, 
    COUNT(*) as duplicate_count
FROM incidents 
GROUP BY user_email, timestamp 
HAVING COUNT(*) > 1
ORDER BY duplicate_count DESC
LIMIT 20;

-- 2. Duplicate'lerin detaylarını gör
SELECT id, timestamp, user_email, policy, department
FROM incidents 
WHERE (user_email, timestamp) IN (
    SELECT user_email, timestamp 
    FROM incidents 
    GROUP BY user_email, timestamp 
    HAVING COUNT(*) > 1
)
ORDER BY timestamp DESC, user_email
LIMIT 50;

-- 3. Duplicate'leri sil - ID=0 olanları silerek gerçek ID'li kayıtları koru
-- NOT: Bu komutu çalıştırmadan önce yedek alın!
DELETE FROM incidents 
WHERE id = 0 
AND (user_email, timestamp) IN (
    SELECT user_email, timestamp 
    FROM incidents 
    GROUP BY user_email, timestamp 
    HAVING COUNT(*) > 1
);

-- 4. Eğer her ikisi de 0 değilse, en büyük ID'yi tut
-- Bu komut satır satır silme yapar (daha güvenli)
DELETE FROM incidents a
USING incidents b
WHERE a.user_email = b.user_email 
  AND a.timestamp = b.timestamp
  AND a.id < b.id;

-- 5. Sonuç kontrolü
SELECT 
    COUNT(*) as total_incidents,
    COUNT(DISTINCT (user_email, timestamp)) as unique_incidents
FROM incidents;

-- 6. Hala duplicate varsa kontrol et
SELECT 
    user_email, 
    timestamp, 
    COUNT(*) as duplicate_count
FROM incidents 
GROUP BY user_email, timestamp 
HAVING COUNT(*) > 1;
