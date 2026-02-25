# PowerShell Script: 7-sütunlu CSV'den domain özelliklerini import et
# dest_domain_analysis.csv (yeni format)

param(
    [string]$CsvPath = "$PSScriptRoot\dest_domain_analysis.csv",
    [string]$OutputPath = "$PSScriptRoot\import-domain-features.sql"
)

Write-Host "Domain Features Import Script" -ForegroundColor Cyan
Write-Host "==============================" -ForegroundColor Cyan

# Kişisel domain listesi
$personalDomains = @(
    "gmail.com", "hotmail.com", "outlook.com", "outlook.com.tr",
    "windowslive.com", "icloud.com", "yahoo.com", "mynet.com",
    "msn.com", "live.nl", "yandex.com", "mail.com", "aol.com", "protonmail.com"
)

if (-not (Test-Path $CsvPath)) {
    Write-Host "CSV dosyasi bulunamadi: $CsvPath" -ForegroundColor Red
    exit 1
}

$csvData = Import-Csv -Path $CsvPath -Delimiter ";"
Write-Host "CSV'den $($csvData.Count) domain okundu" -ForegroundColor Green

$sqlContent = @"
-- ============================================================================
-- Domain Features Import (7-Column CSV)
-- Generated: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")
-- ============================================================================

-- Önce yeni sütunları ekle (eğer yoksa)
ALTER TABLE nda_domains 
ADD COLUMN IF NOT EXISTS istirak_domain BOOLEAN DEFAULT FALSE,
ADD COLUMN IF NOT EXISTS egitim BOOLEAN DEFAULT FALSE,
ADD COLUMN IF NOT EXISTS noter BOOLEAN DEFAULT FALSE,
ADD COLUMN IF NOT EXISTS hukuk BOOLEAN DEFAULT FALSE,
ADD COLUMN IF NOT EXISTS denetim BOOLEAN DEFAULT FALSE,
ADD COLUMN IF NOT EXISTS banka BOOLEAN DEFAULT FALSE;

-- Domain verilerini ekle/güncelle
"@

$processedDomains = @{}
$stats = @{ Total = 0; NdaVar = 0; Istirak = 0; Egitim = 0; Noter = 0; Hukuk = 0; Denetim = 0; Banka = 0 }

foreach ($row in $csvData) {
    $domain = $row.domain.Trim().ToLower()
    if ([string]::IsNullOrWhiteSpace($domain)) { continue }
    if ($processedDomains.ContainsKey($domain)) { continue }
    $processedDomains[$domain] = $true
    
    $domain = $domain.Replace("'", "''")
    
    $hasNda = if ($row.gizlilik_sozlesmesi.Trim().ToLower() -eq "evet" -or $row.gizlilik_sozlesmesi.Trim().ToLower() -eq "var") { "true" } else { "false" }
    $istirak = if ($row.istirak_domain.Trim().ToLower() -eq "evet") { "true" } else { "false" }
    $egitim = if ($row.egitim.Trim().ToLower() -eq "evet") { "true" } else { "false" }
    $noter = if ($row.noter.Trim().ToLower() -eq "evet") { "true" } else { "false" }
    $hukuk = if ($row.hukuk.Trim().ToLower() -eq "evet") { "true" } else { "false" }
    $denetim = if ($row.denetim.Trim().ToLower() -eq "evet") { "true" } else { "false" }
    $banka = if ($row.banka.Trim().ToLower() -eq "evet") { "true" } else { "false" }
    $isPersonal = if ($personalDomains -contains $domain) { "true" } else { "false" }
    
    $stats.Total++
    if ($hasNda -eq "true") { $stats.NdaVar++ }
    if ($istirak -eq "true") { $stats.Istirak++ }
    if ($egitim -eq "true") { $stats.Egitim++ }
    if ($noter -eq "true") { $stats.Noter++ }
    if ($hukuk -eq "true") { $stats.Hukuk++ }
    if ($denetim -eq "true") { $stats.Denetim++ }
    if ($banka -eq "true") { $stats.Banka++ }
    
    $sqlContent += @"

INSERT INTO nda_domains (domain, has_nda, is_unknown, is_personal, istirak_domain, egitim, noter, hukuk, denetim, banka)
VALUES ('$domain', $hasNda, false, $isPersonal, $istirak, $egitim, $noter, $hukuk, $denetim, $banka)
ON CONFLICT (domain) DO UPDATE SET 
    has_nda = EXCLUDED.has_nda,
    is_unknown = false,
    istirak_domain = EXCLUDED.istirak_domain,
    egitim = EXCLUDED.egitim,
    noter = EXCLUDED.noter,
    hukuk = EXCLUDED.hukuk,
    denetim = EXCLUDED.denetim,
    banka = EXCLUDED.banka,
    updated_at = NOW();
"@
}

$sqlContent += @"

-- Sonuçları kontrol et
SELECT 
    COUNT(*) as total,
    SUM(CASE WHEN has_nda THEN 1 ELSE 0 END) as nda_var,
    SUM(CASE WHEN istirak_domain THEN 1 ELSE 0 END) as istirak,
    SUM(CASE WHEN egitim THEN 1 ELSE 0 END) as egitim,
    SUM(CASE WHEN noter THEN 1 ELSE 0 END) as noter,
    SUM(CASE WHEN hukuk THEN 1 ELSE 0 END) as hukuk,
    SUM(CASE WHEN denetim THEN 1 ELSE 0 END) as denetim,
    SUM(CASE WHEN banka THEN 1 ELSE 0 END) as banka,
    SUM(CASE WHEN is_personal THEN 1 ELSE 0 END) as kisisel
FROM nda_domains;
"@

$sqlContent | Out-File -FilePath $OutputPath -Encoding UTF8
Write-Host "`nSQL dosyasi olusturuldu: $OutputPath" -ForegroundColor Green
Write-Host "`nIstatistikler:" -ForegroundColor Yellow
Write-Host "  Toplam: $($stats.Total)" -ForegroundColor White
Write-Host "  NDA Var: $($stats.NdaVar)" -ForegroundColor White
Write-Host "  Istirak: $($stats.Istirak)" -ForegroundColor White
Write-Host "  Egitim: $($stats.Egitim)" -ForegroundColor White
Write-Host "  Noter: $($stats.Noter)" -ForegroundColor White
Write-Host "  Hukuk: $($stats.Hukuk)" -ForegroundColor White
Write-Host "  Denetim: $($stats.Denetim)" -ForegroundColor White
Write-Host "  Banka: $($stats.Banka)" -ForegroundColor White

Write-Host "`nKullanim:" -ForegroundColor Cyan
Write-Host "  psql -h localhost -U postgres -d dlp_analyzer -f $OutputPath" -ForegroundColor White
