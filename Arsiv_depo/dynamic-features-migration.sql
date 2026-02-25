-- ============================================================================
-- Dynamic Domain Features Schema
-- Supports adding custom columns from UI without changing table schema
-- ============================================================================

-- 1. Feature Definitions (Sütun Tanımları)
CREATE TABLE IF NOT EXISTS domain_feature_definitions (
    id SERIAL PRIMARY KEY,
    key_name VARCHAR(50) NOT NULL UNIQUE,      -- kod tarafında kullanılacak key (örn: saglik_sektoru)
    display_name VARCHAR(100) NOT NULL,        -- ekranda görünecek isim (örn: Sağlık Sektörü)
    is_active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- 2. Feature Values (Değerler)
CREATE TABLE IF NOT EXISTS domain_feature_values (
    id SERIAL PRIMARY KEY,
    domain_id INTEGER NOT NULL REFERENCES nda_domains(id) ON DELETE CASCADE,
    feature_id INTEGER NOT NULL REFERENCES domain_feature_definitions(id) ON DELETE CASCADE,
    is_enabled BOOLEAN DEFAULT FALSE,          -- Evet/Hayır değeri
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    UNIQUE(domain_id, feature_id)              -- Her domain-feature ikilisi tek olmalı
);

-- 3. Indexes
CREATE INDEX IF NOT EXISTS idx_domain_feature_values_domain ON domain_feature_values(domain_id);
CREATE INDEX IF NOT EXISTS idx_domain_feature_values_feature ON domain_feature_values(feature_id);

-- 4. Initial Seed (Opsiyonel: Eğer mevcut sabit kolonları da buraya taşımak istersek)
-- Şimdilik sabit kolonlar nda_domains tablosunda kalacak, burası EKSTRA kolonlar için.
