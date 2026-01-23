# PowerShell Script: CSV'den NDA domain'lerini veritabanına import et
# Bu script dest_domain_analysis.csv dosyasından INSERT statement'ları oluşturur

param(
    [string]$CsvPath = "$PSScriptRoot\dest_domain_analysis.csv",
    [string]$OutputPath = "$PSScriptRoot\import-nda-domains-data.sql",
    [string]$ConnectionString = ""
)

Write-Host "NDA Domain Import Script" -ForegroundColor Cyan
Write-Host "========================" -ForegroundColor Cyan

# Kişisel domain listesi
$personalDomains = @(
    "gmail.com", "hotmail.com", "outlook.com", "outlook.com.tr",
    "windowslive.com", "icloud.com", "yahoo.com", "mynet.com",
    "msn.com", "live.nl", "yandex.com", "mail.com", "aol.com", "protonmail.com"
)

# CSV dosyasını oku
if (-not (Test-Path $CsvPath)) {
    Write-Host "CSV dosyasi bulunamadi: $CsvPath" -ForegroundColor Red
    exit 1
}

$csvData = Import-Csv -Path $CsvPath -Delimiter ";"
Write-Host "CSV'den $($csvData.Count) domain okundu" -ForegroundColor Green

# SQL dosyasını oluştur
$sqlContent = @"
-- ============================================================================
-- NDA Domains Data Import
-- Generated from dest_domain_analysis.csv
-- Date: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")
-- ============================================================================

-- Önce kişisel domain'leri ekle
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

-- CSV'den domain'leri ekle
INSERT INTO nda_domains (domain, has_nda, is_unknown, is_personal) VALUES
"@

$insertValues = @()
$ndaVarCount = 0
$ndaYokCount = 0
$kisiselCount = 0

$processedDomains = @{}

foreach ($row in $csvData) {
    $domain = $row.domain.Trim().ToLower()
    if ([string]::IsNullOrWhiteSpace($domain)) { continue }
    
    # Skip duplicates
    if ($processedDomains.ContainsKey($domain)) { continue }
    $processedDomains[$domain] = $true
    
    # Escape single quotes
    $domain = $domain.Replace("'", "''")
    
    $hasNda = if ($row.gizlilik_sozlesmesi.Trim().ToLower() -eq "var") { "true" } else { "false" }
    $isPersonal = if ($personalDomains -contains $domain) { "true" } else { "false" }
    
    if ($hasNda -eq "true") { $ndaVarCount++ }
    elseif ($isPersonal -eq "true") { $kisiselCount++ }
    else { $ndaYokCount++ }
    
    $insertValues += "    ('$domain', $hasNda, false, $isPersonal)"
}

$sqlContent += $insertValues -join ",`n"
$sqlContent += @"

ON CONFLICT (domain) DO UPDATE SET 
    has_nda = EXCLUDED.has_nda,
    is_unknown = false,
    updated_at = NOW();

-- Sonuçları kontrol et
SELECT 
    COUNT(*) as total_domains,
    SUM(CASE WHEN has_nda THEN 1 ELSE 0 END) as nda_var,
    SUM(CASE WHEN NOT has_nda AND NOT is_personal THEN 1 ELSE 0 END) as nda_yok,
    SUM(CASE WHEN is_personal THEN 1 ELSE 0 END) as kisisel,
    SUM(CASE WHEN is_unknown THEN 1 ELSE 0 END) as bilinmeyen
FROM nda_domains;
"@

# SQL dosyasına yaz
$sqlContent | Out-File -FilePath $OutputPath -Encoding UTF8
Write-Host "`nSQL dosyasi olusturuldu: $OutputPath" -ForegroundColor Green
Write-Host "  - NDA Var: $ndaVarCount" -ForegroundColor Yellow
Write-Host "  - NDA Yok: $ndaYokCount" -ForegroundColor Yellow  
Write-Host "  - Kisisel: $kisiselCount" -ForegroundColor Yellow

# Eğer connection string verilmişse doğrudan çalıştır
if (-not [string]::IsNullOrWhiteSpace($ConnectionString)) {
    Write-Host "`nVeritabanina import ediliyor..." -ForegroundColor Cyan
    try {
        # psql ile çalıştır
        $env:PGPASSWORD = ($ConnectionString -split "Password=")[1].Split(";")[0]
        $host = ($ConnectionString -split "Host=")[1].Split(";")[0]
        $database = ($ConnectionString -split "Database=")[1].Split(";")[0]
        $user = ($ConnectionString -split "Username=")[1].Split(";")[0]
        
        & psql -h $host -U $user -d $database -f $OutputPath
        Write-Host "Import basarili!" -ForegroundColor Green
    }
    catch {
        Write-Host "Import hatasi: $_" -ForegroundColor Red
    }
}

Write-Host "`nKullanim:" -ForegroundColor Cyan
Write-Host "  psql -h localhost -U postgres -d dlp_analyzer -f $OutputPath" -ForegroundColor White
