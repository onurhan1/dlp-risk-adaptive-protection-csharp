# PostgreSQL Yapı Sağlamlık Kontrolü Rehberi

Bu belge, DLP Risk Analyzer veritabanının sağlamlığını kontrol etmek için uzak sunucuda çalıştırılacak SQL sorgularını içerir.

> **Nasıl Kullanılır:** Her bölümdeki SQL'i PostgreSQL'de çalıştırın (pgAdmin veya `psql`), sonuçları gözden geçirin.

---

## 1. Tablo Boyut Analizi

```sql
SELECT 
    schemaname || '.' || tablename AS tablo,
    pg_size_pretty(pg_total_relation_size(schemaname || '.' || tablename)) AS toplam_boyut,
    pg_size_pretty(pg_relation_size(schemaname || '.' || tablename)) AS veri_boyutu,
    pg_size_pretty(pg_total_relation_size(schemaname || '.' || tablename) - pg_relation_size(schemaname || '.' || tablename)) AS index_boyutu,
    (SELECT COUNT(*) FROM information_schema.columns c WHERE c.table_name = tablename AND c.table_schema = schemaname) AS sutun_sayisi
FROM pg_tables 
WHERE schemaname = 'public' 
AND tablename NOT LIKE 'pg_%'
AND tablename NOT LIKE '_timescaledb%'
ORDER BY pg_total_relation_size(schemaname || '.' || tablename) DESC;
```

**Satır Sayıları:**
```sql
SELECT 'incidents' AS tablo, COUNT(*) AS satir_sayisi FROM incidents
UNION ALL SELECT 'daily_summaries', COUNT(*) FROM daily_summaries
UNION ALL SELECT 'department_summaries', COUNT(*) FROM department_summaries
UNION ALL SELECT 'user_risk_trends', COUNT(*) FROM user_risk_trends
UNION ALL SELECT 'ai_behavioral_analyses', COUNT(*) FROM ai_behavioral_analyses
UNION ALL SELECT 'audit_logs', COUNT(*) FROM audit_logs
UNION ALL SELECT 'anomaly_detections', COUNT(*) FROM anomaly_detections
UNION ALL SELECT 'system_settings', COUNT(*) FROM system_settings
ORDER BY satir_sayisi DESC;
```

### ⚠️ Ne Aranmalı
- `incidents` tablosu en büyük tablo olmalı (TimescaleDB hypertable)
- Toplam boyut 10GB+ ise chunk stratejisi gözden geçirilmeli

---

## 2. Sütun Tipleri Kontrolü

```sql
SELECT 
    table_name,
    column_name,
    data_type,
    character_maximum_length,
    is_nullable,
    column_default,
    CASE 
        WHEN data_type = 'integer' AND column_name LIKE '%id%' THEN '⚠️ BIGINT gerekebilir'
        WHEN data_type = 'text' AND column_name IN ('user_email', 'department', 'policy', 'channel', 'action') THEN '💡 VARCHAR(255) daha güvenli olabilir'
        WHEN data_type = 'integer' AND column_name = 'id' AND table_name = 'incidents' THEN '🔴 BIGINT yapılmalı (2.1 milyar limit!)'
        ELSE '✅ OK'
    END AS oneri
FROM information_schema.columns
WHERE table_schema = 'public'
AND table_name IN ('incidents', 'daily_summaries', 'department_summaries', 
                   'user_risk_trends', 'ai_behavioral_analyses', 'audit_logs', 
                   'anomaly_detections', 'system_settings')
ORDER BY table_name, ordinal_position;
```

### ⚠️ Kritik Kontroller
| Sütun | Mevcut Tip | Risk | Çözüm |
|-------|-----------|------|-------|
| `incidents.id` | `integer` | 2.1 milyar satır limiti | `bigint`'e geçiş gerekebilir |
| `audit_logs.id` | `integer` | Aynı risk | Büyüme hızına göre değerlendirilmeli |
| `* TEXT` sütunları | `text` | Kontrolsüz boyut | Performans sorununa yol açabilir |

---

## 3. Index Kullanım Analizi

```sql
SELECT 
    schemaname || '.' || relname AS tablo,
    indexrelname AS index_adi,
    idx_scan AS kullanim_sayisi,
    pg_size_pretty(pg_relation_size(indexrelid)) AS index_boyutu,
    CASE 
        WHEN idx_scan = 0 THEN '🔴 HİÇ KULLANILMAMIŞ - silinebilir'
        WHEN idx_scan < 10 THEN '🟡 Az kullanılıyor'
        ELSE '✅ Aktif'
    END AS durum
FROM pg_stat_user_indexes
WHERE schemaname = 'public'
ORDER BY idx_scan ASC;
```

**Eksik Index Önerileri:**
```sql
-- Bu sorgu sık yapılan sequential scan'ları gösterir
SELECT 
    schemaname || '.' || relname AS tablo,
    seq_scan AS seq_scan_sayisi,
    seq_tup_read AS okunan_satir,
    idx_scan AS index_scan_sayisi,
    CASE 
        WHEN seq_scan > idx_scan AND seq_tup_read > 10000 
        THEN '⚠️ SEQ SCAN çok fazla - index eklenmeli'
        ELSE '✅ OK'
    END AS oneri
FROM pg_stat_user_tables
WHERE schemaname = 'public'
ORDER BY seq_scan DESC;
```

---

## 4. TimescaleDB Hypertable Durumu

```sql
-- Hypertable olup olmadığını kontrol et
SELECT hypertable_name, num_chunks, compression_enabled
FROM timescaledb_information.hypertables;

-- Chunk boyutları
SELECT 
    hypertable_name,
    chunk_name,
    range_start,
    range_end,
    pg_size_pretty(total_bytes) AS boyut,
    pg_size_pretty(table_bytes) AS veri,
    pg_size_pretty(index_bytes) AS index
FROM timescaledb_information.chunks
WHERE hypertable_name = 'incidents'
ORDER BY range_start DESC
LIMIT 20;
```

### ⚠️ Ne Aranmalı
- Chunk interval: Varsayılan 7 gün. Günlük veri çoksa 1 gün yapılabilir
- Compression enabled mı? Eski veriler sıkıştırılabilir
- Retention policy var mı? Eski veri silinmeli mi?

**Compression Aktifleştirme (opsiyonel):**
```sql
-- 30 günden eski verileri sıkıştır
ALTER TABLE incidents SET (
    timescaledb.compress, 
    timescaledb.compress_segmentby = 'user_email'
);
SELECT add_compression_policy('incidents', INTERVAL '30 days');
```

---

## 5. Incidents Tablosu Büyüme Tahmini

```sql
-- Günlük ortalama eklenen satır sayısı
SELECT 
    DATE(timestamp) AS gun,
    COUNT(*) AS satir_sayisi
FROM incidents
WHERE timestamp >= NOW() - INTERVAL '30 days'
GROUP BY DATE(timestamp)
ORDER BY gun DESC;

-- Ortalama hesaplama
SELECT 
    COUNT(*) AS toplam_30_gunluk,
    ROUND(COUNT(*) / 30.0) AS gunluk_ortalama,
    ROUND(COUNT(*) / 30.0 * 365) AS yillik_tahmin,
    pg_size_pretty(pg_total_relation_size('incidents')) AS mevcut_boyut
FROM incidents
WHERE timestamp >= NOW() - INTERVAL '30 days';
```

### Büyüme Tablosu
| Günlük Satır | 1 Yıl | 3 Yıl | `integer` ID Yeterli mi? |
|-------------|-------|-------|------------------------|
| 100 | 36.5K | 109K | ✅ Evet |
| 1,000 | 365K | 1.1M | ✅ Evet |
| 10,000 | 3.6M | 10.9M | ✅ Evet |
| 100,000 | 36.5M | 109M | ⚠️ Dikkat |
| 1,000,000 | 365M | 1.09B | 🔴 Geçiş gerekli |

---

## 6. VACUUM & ANALYZE Durumu

```sql
SELECT 
    schemaname || '.' || relname AS tablo,
    last_vacuum,
    last_autovacuum,
    last_analyze,
    last_autoanalyze,
    n_dead_tup AS olmus_satirlar,
    n_live_tup AS canli_satirlar,
    CASE 
        WHEN n_dead_tup > n_live_tup * 0.1 THEN '🔴 VACUUM gerekli!'
        WHEN n_dead_tup > n_live_tup * 0.05 THEN '🟡 Yakında VACUUM yapılmalı'
        ELSE '✅ OK'
    END AS durum
FROM pg_stat_user_tables
WHERE schemaname = 'public'
ORDER BY n_dead_tup DESC;
```

### Auto-vacuum Ayarları
```sql
SHOW autovacuum;
SHOW autovacuum_vacuum_threshold;
SHOW autovacuum_analyze_threshold;
```

---

## 7. Foreign Key & Constraint Kontrolü

```sql
-- Mevcut constraint'ler
SELECT 
    tc.table_name,
    tc.constraint_name,
    tc.constraint_type,
    kcu.column_name
FROM information_schema.table_constraints tc
JOIN information_schema.key_column_usage kcu 
    ON tc.constraint_name = kcu.constraint_name
WHERE tc.table_schema = 'public'
ORDER BY tc.table_name, tc.constraint_type;
```

---

## 8. Genel Sağlık Özeti

```sql
SELECT 
    'PostgreSQL Version' AS metrik, version() AS deger
UNION ALL
SELECT 'Database Size', pg_size_pretty(pg_database_size(current_database()))
UNION ALL
SELECT 'Active Connections', count(*)::text FROM pg_stat_activity
UNION ALL
SELECT 'TimescaleDB Version', extversion FROM pg_extension WHERE extname = 'timescaledb'
UNION ALL
SELECT 'Uptime', (now() - pg_postmaster_start_time())::text;
```

---

## Sonuçları Değerlendirme

Yukarıdaki sorguların çıktılarını bana iletin. Değerlendirme sonucunda:
1. Gerekli migration SQL'leri hazırlanacak (örn: `integer` → `bigint`)
2. Eksik index'ler eklenecek
3. TimescaleDB optimizasyonları yapılacak
4. Compression/retention politikaları önerilecek
