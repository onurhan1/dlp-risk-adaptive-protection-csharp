# ============================================================================
# DLP Risk Analyzer - PostgreSQL Backup Script
# Bu script PostgreSQL veritabanının yedeğini alır
# Administrator olarak çalıştırılmalıdır!
# ============================================================================

# Yapılandırma
$BackupDir = "D:\DLP-Backups"
$DatabaseName = "dlp_analyzer"
$PostgresUser = "postgres"
$RetentionDays = 30  # Kaç günlük backup tutulsun

# Renk kodları
$Green = "Green"
$Yellow = "Yellow"
$Red = "Red"
$Cyan = "Cyan"

Write-Host ""
Write-Host "============================================================" -ForegroundColor $Cyan
Write-Host "    DLP Risk Analyzer - PostgreSQL Backup" -ForegroundColor $Cyan
Write-Host "============================================================" -ForegroundColor $Cyan
Write-Host ""

# Backup dizinini oluştur
if (-not (Test-Path $BackupDir)) {
    New-Item -ItemType Directory -Path $BackupDir -Force | Out-Null
    Write-Host "[OK] Backup dizini olusturuldu: $BackupDir" -ForegroundColor $Green
}

# Tarih damgası
$Timestamp = Get-Date -Format "yyyy-MM-dd_HH-mm-ss"
$BackupFile = Join-Path $BackupDir "dlp_backup_$Timestamp.sql"

Write-Host "[*] Backup baslatiliyor..." -ForegroundColor $Yellow
Write-Host "    Veritabani: $DatabaseName" -ForegroundColor White
Write-Host "    Hedef: $BackupFile" -ForegroundColor White
Write-Host ""

# pg_dump ile backup al
try {
    $env:PGPASSWORD = "postgres"  # Şifre gerekiyorsa buraya yazın veya pgpass.conf kullanın
    
    # pg_dump komutunu bul
    $pgDumpPath = $null
    
    # PostgreSQL kurulum dizinlerini kontrol et
    $possiblePaths = @(
        "C:\Program Files\PostgreSQL\16\bin\pg_dump.exe",
        "C:\Program Files\PostgreSQL\16\pgAdmin 4\runtime\pg_dump.exe"
    )
    
    foreach ($path in $possiblePaths) {
        if (Test-Path $path) {
            $pgDumpPath = $path
            Write-Host "    pg_dump bulundu: $path" -ForegroundColor $Green
            break
        }
    }
    
    if (-not $pgDumpPath) {
        Write-Host "[HATA] pg_dump bulunamadi! PostgreSQL kurulu mu?" -ForegroundColor $Red
        Write-Host "Kontrol edilen yollar:" -ForegroundColor $Yellow
        $possiblePaths | ForEach-Object { Write-Host "  - $_" }
        exit 1
    }
    
    & $pgDumpPath -U $PostgresUser -d $DatabaseName -f $BackupFile -F p --verbose 2>&1 | ForEach-Object {
        if ($_ -match "error|fatal") {
            Write-Host "    [HATA] $_" -ForegroundColor $Red
        }
    }
    
    if (Test-Path $BackupFile) {
        $fileSize = (Get-Item $BackupFile).Length / 1MB
        Write-Host ""
        Write-Host "[OK] Backup basarili!" -ForegroundColor $Green
        Write-Host "    Dosya: $BackupFile" -ForegroundColor $Green
        Write-Host "    Boyut: $([math]::Round($fileSize, 2)) MB" -ForegroundColor $Green
    } else {
        Write-Host "[HATA] Backup dosyasi olusturulamadi!" -ForegroundColor $Red
    }
} catch {
    Write-Host "[HATA] Backup sirasinda hata olustu: $_" -ForegroundColor $Red
}

# Eski backupları temizle
Write-Host ""
Write-Host "[*] Eski backuplar temizleniyor ($RetentionDays gunden eski)..." -ForegroundColor $Yellow

$cutoffDate = (Get-Date).AddDays(-$RetentionDays)
$oldBackups = Get-ChildItem -Path $BackupDir -Filter "dlp_backup_*.sql" | Where-Object { $_.LastWriteTime -lt $cutoffDate }

if ($oldBackups.Count -gt 0) {
    foreach ($old in $oldBackups) {
        Remove-Item $old.FullName -Force
        Write-Host "    Silindi: $($old.Name)" -ForegroundColor $Yellow
    }
    Write-Host "    [OK] $($oldBackups.Count) eski backup silindi" -ForegroundColor $Green
} else {
    Write-Host "    Silinecek eski backup yok" -ForegroundColor $Green
}

# Mevcut backupları listele
Write-Host ""
Write-Host "Mevcut backuplar:" -ForegroundColor $Yellow
$currentBackups = Get-ChildItem -Path $BackupDir -Filter "dlp_backup_*.sql" | Sort-Object LastWriteTime -Descending | Select-Object -First 10
foreach ($b in $currentBackups) {
    $size = [math]::Round($b.Length / 1MB, 2)
    Write-Host "  - $($b.Name) ($size MB)" -ForegroundColor $Green
}

Write-Host ""
Write-Host "============================================================" -ForegroundColor $Cyan
Write-Host "    BACKUP TAMAMLANDI" -ForegroundColor $Green
Write-Host "============================================================" -ForegroundColor $Cyan
Write-Host ""
