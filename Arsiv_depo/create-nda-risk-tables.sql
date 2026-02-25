-- ============================================================================
-- NDA Domains Table - Gizlilik Sözleşmesi Domain Yönetimi
-- Risk Score Algorithm Overhaul için oluşturuldu
-- ============================================================================

-- 1. NDA Domains tablosu oluştur
CREATE TABLE IF NOT EXISTS nda_domains (
    id SERIAL PRIMARY KEY,
    domain VARCHAR(255) NOT NULL,
    has_nda BOOLEAN NOT NULL DEFAULT FALSE,  -- true: Gizlilik sözleşmesi VAR (skor 1), false: YOK (skor 5)
    is_unknown BOOLEAN NOT NULL DEFAULT FALSE, -- true: Yeni keşfedilmiş, henüz sınıflandırılmamış
    is_personal BOOLEAN NOT NULL DEFAULT FALSE, -- true: Kişisel domain (gmail, hotmail vb.) (skor 10)
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW(),
    CONSTRAINT unique_domain UNIQUE (domain)
);

-- 2. Index oluştur
CREATE INDEX IF NOT EXISTS idx_nda_domains_domain ON nda_domains (LOWER(domain));
CREATE INDEX IF NOT EXISTS idx_nda_domains_has_nda ON nda_domains (has_nda);
CREATE INDEX IF NOT EXISTS idx_nda_domains_is_unknown ON nda_domains (is_unknown);
CREATE INDEX IF NOT EXISTS idx_nda_domains_is_personal ON nda_domains (is_personal);

-- 3. Kişisel domain'leri ekle (bunlar CSV'de var ama özel olarak işaretlenmeli)
INSERT INTO nda_domains (domain, has_nda, is_unknown, is_personal) VALUES
    ('gmail.com', false, false, true),
    ('hotmail.com', false, false, true),
    ('outlook.com', false, false, true),
    ('outlook.com.tr', false, false, true),
    ('windowslive.com', false, false, true),
    ('icloud.com', false, false, true),
    ('yahoo.com', false, false, true),
    ('mynet.com', false, false, true),
    ('msn.com', false, false, true),
    ('live.nl', false, false, true),
    ('yandex.com', false, false, true),
    ('mail.com', false, false, true),
    ('aol.com', false, false, true),
    ('protonmail.com', false, false, true)
ON CONFLICT (domain) DO UPDATE SET 
    is_personal = true,
    updated_at = NOW();

-- ============================================================================
-- User Daily Risk Scores Table - Günlük Risk Skorları
-- ============================================================================

CREATE TABLE IF NOT EXISTS user_daily_risk_scores (
    id SERIAL PRIMARY KEY,
    user_email VARCHAR(255) NOT NULL,
    date DATE NOT NULL,
    daily_risk_score DOUBLE PRECISION NOT NULL DEFAULT 0,
    incident_count INT NOT NULL DEFAULT 0,
    max_risk_score INT NOT NULL DEFAULT 0,
    avg_risk_score DOUBLE PRECISION NOT NULL DEFAULT 0,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    CONSTRAINT unique_user_date UNIQUE (user_email, date)
);

-- Index oluştur
CREATE INDEX IF NOT EXISTS idx_user_daily_scores_email ON user_daily_risk_scores (user_email);
CREATE INDEX IF NOT EXISTS idx_user_daily_scores_date ON user_daily_risk_scores (date);
CREATE INDEX IF NOT EXISTS idx_user_daily_scores_email_date ON user_daily_risk_scores (user_email, date);

-- ============================================================================
-- Doğrulama sorguları
-- ============================================================================
-- Tabloların oluşturulduğunu kontrol et:
-- SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' AND table_name IN ('nda_domains', 'user_daily_risk_scores');

-- Kişisel domain'leri kontrol et:
-- SELECT * FROM nda_domains WHERE is_personal = true;
