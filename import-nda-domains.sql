-- ============================================================================
-- CSV'den NDA Domain'lerini Import Et
-- dest_domain_analysis.csv dosyasından verileri yükler
-- ============================================================================

-- Önce geçici tablo oluştur
CREATE TEMP TABLE IF NOT EXISTS temp_nda_import (
    domain VARCHAR(255),
    gizlilik_sozlesmesi VARCHAR(10)
);

-- CSV'den COPY komutu ile yükle (psql ile çalıştırılmalı)
-- NOT: Bu komutu çalıştırırken CSV dosyasının tam yolunu belirtin
-- \COPY temp_nda_import(domain, gizlilik_sozlesmesi) FROM 'C:\Users\abdul\Desktop\dlp-risk-adaptive-protection-csharp-main\dest_domain_analysis.csv' WITH (FORMAT CSV, HEADER true, DELIMITER ';', ENCODING 'UTF8');

-- Alternatif: Eğer COPY çalışmıyorsa, aşağıdaki INSERT'leri kullanın
-- (Bu dosya CSV'den otomatik generate edilmiştir)

-- Verileri ana tabloya aktar
INSERT INTO nda_domains (domain, has_nda, is_unknown, is_personal)
SELECT 
    LOWER(TRIM(domain)),
    CASE WHEN LOWER(TRIM(gizlilik_sozlesmesi)) = 'var' THEN true ELSE false END,
    false,
    CASE 
        WHEN LOWER(TRIM(domain)) IN ('gmail.com', 'hotmail.com', 'outlook.com', 'outlook.com.tr', 
                                      'windowslive.com', 'icloud.com', 'yahoo.com', 'mynet.com', 
                                      'msn.com', 'live.nl', 'yandex.com', 'mail.com') 
        THEN true 
        ELSE false 
    END
FROM temp_nda_import
WHERE domain IS NOT NULL AND TRIM(domain) != ''
ON CONFLICT (domain) DO UPDATE SET 
    has_nda = EXCLUDED.has_nda,
    is_unknown = false,
    updated_at = NOW();

-- Geçici tabloyu temizle
DROP TABLE IF EXISTS temp_nda_import;

-- Sonuçları kontrol et
SELECT 
    COUNT(*) as total_domains,
    SUM(CASE WHEN has_nda THEN 1 ELSE 0 END) as nda_var,
    SUM(CASE WHEN NOT has_nda AND NOT is_personal THEN 1 ELSE 0 END) as nda_yok,
    SUM(CASE WHEN is_personal THEN 1 ELSE 0 END) as kisisel,
    SUM(CASE WHEN is_unknown THEN 1 ELSE 0 END) as bilinmeyen
FROM nda_domains;
