-- Mevcut veritabanındaki domain prefix'leri temizle
-- KUVEYTTURK\username -> username formatına dönüştür

-- user_email alanını güncelle (PostgreSQL escape syntax)
UPDATE incidents 
SET user_email = SPLIT_PART(user_email, E'\\', 2)
WHERE user_email LIKE '%\\%';

-- login_name alanını güncelle
UPDATE incidents 
SET login_name = SPLIT_PART(login_name, E'\\', 2)
WHERE login_name LIKE '%\\%';

-- Kaç satır güncellendi kontrol et
SELECT 'Updated records check:' as status;
SELECT COUNT(*) as total_records FROM incidents;
SELECT COUNT(*) as still_has_prefix FROM incidents WHERE user_email LIKE '%\\%';

-- Sonuçları kontrol et
SELECT DISTINCT user_email FROM incidents ORDER BY user_email LIMIT 20;
