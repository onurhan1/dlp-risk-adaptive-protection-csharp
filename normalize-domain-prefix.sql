-- Mevcut veritabanındaki domain prefix'leri temizle
-- KUVEYTTURK\username -> username formatına dönüştür

-- user_email alanını güncelle
UPDATE incidents 
SET user_email = SPLIT_PART(user_email, '\', 2)
WHERE user_email LIKE '%\%';

-- login_name alanını güncelle
UPDATE incidents 
SET login_name = SPLIT_PART(login_name, '\', 2)
WHERE login_name LIKE '%\%';

-- Sonuçları kontrol et
SELECT DISTINCT user_email FROM incidents ORDER BY user_email;
