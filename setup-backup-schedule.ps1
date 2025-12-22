# ============================================================================
# DLP Risk Analyzer - Otomatik Backup Kurulum Scripti
# Bu script günlük backup için Task Scheduler görevi oluşturur
# Administrator olarak çalıştırılmalıdır!
# ============================================================================

# Renk kodları
$Green = "Green"
$Yellow = "Yellow"
$Red = "Red"
$Cyan = "Cyan"

Write-Host ""
Write-Host "============================================================" -ForegroundColor $Cyan
Write-Host "    DLP Risk Analyzer - Otomatik Backup Kurulumu" -ForegroundColor $Cyan
Write-Host "============================================================" -ForegroundColor $Cyan
Write-Host ""

# Admin kontrolü
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "[HATA] Bu script Administrator olarak calistirilmalidir!" -ForegroundColor $Red
    Write-Host "Sag tiklayin ve 'Run as Administrator' secin." -ForegroundColor $Yellow
    Read-Host "Devam etmek icin Enter'a basin"
    exit 1
}

Write-Host "[OK] Administrator yetkisi dogrulandi" -ForegroundColor $Green

# Yapılandırma
$ProjectRoot = "C:\Users\abdul\Desktop\dlp-risk-adaptive-protection-csharp-main"
$BackupScriptPath = Join-Path $ProjectRoot "backup-database.ps1"

# Backup dizinini oluştur
$BackupDir = "D:\DLP-Backups"
if (-not (Test-Path $BackupDir)) {
    New-Item -ItemType Directory -Path $BackupDir -Force | Out-Null
    Write-Host "[OK] Backup dizini olusturuldu: $BackupDir" -ForegroundColor $Green
}

# Var olan görevi sil
$existingTask = Get-ScheduledTask -TaskName "DLP-Daily-Backup" -ErrorAction SilentlyContinue
if ($existingTask) {
    Write-Host "[*] Mevcut backup gorevi siliniyor..." -ForegroundColor $Yellow
    Unregister-ScheduledTask -TaskName "DLP-Daily-Backup" -Confirm:$false
}

# Task Scheduler görevi oluştur
Write-Host ""
Write-Host "[*] Gunluk backup gorevi olusturuluyor..." -ForegroundColor $Cyan

$BackupAction = New-ScheduledTaskAction `
    -Execute "powershell.exe" `
    -Argument "-NoProfile -ExecutionPolicy Bypass -File `"$BackupScriptPath`"" `
    -WorkingDirectory $ProjectRoot

# Her gün saat 02:00'de çalışsın (gece yarısı, sistem boştayken)
$BackupTrigger = New-ScheduledTaskTrigger -Daily -At "02:00"

$BackupSettings = New-ScheduledTaskSettingsSet `
    -AllowStartIfOnBatteries `
    -DontStopIfGoingOnBatteries `
    -StartWhenAvailable `
    -ExecutionTimeLimit (New-TimeSpan -Hours 2)

$BackupPrincipal = New-ScheduledTaskPrincipal `
    -UserId "SYSTEM" `
    -LogonType ServiceAccount `
    -RunLevel Highest

Register-ScheduledTask `
    -TaskName "DLP-Daily-Backup" `
    -Action $BackupAction `
    -Trigger $BackupTrigger `
    -Settings $BackupSettings `
    -Principal $BackupPrincipal `
    -Description "DLP PostgreSQL Gunluk Backup - Her gun saat 02:00'de calisir" | Out-Null

Write-Host "[OK] DLP-Daily-Backup gorevi olusturuldu" -ForegroundColor $Green

# Özet
Write-Host ""
Write-Host "============================================================" -ForegroundColor $Cyan
Write-Host "    KURULUM TAMAMLANDI!" -ForegroundColor $Green
Write-Host "============================================================" -ForegroundColor $Cyan
Write-Host ""
Write-Host "Ayarlar:" -ForegroundColor $Yellow
Write-Host "  - Backup saati: Her gun 02:00" -ForegroundColor White
Write-Host "  - Backup dizini: $BackupDir" -ForegroundColor White
Write-Host "  - Saklama suresi: 30 gun (eski backuplar silinir)" -ForegroundColor White
Write-Host ""
Write-Host "Manuel backup almak icin:" -ForegroundColor $Yellow
Write-Host "  .\backup-database.ps1" -ForegroundColor White
Write-Host ""
Write-Host "Gorev yonetimi:" -ForegroundColor $Yellow
Write-Host "  Task Scheduler: taskschd.msc" -ForegroundColor White
Write-Host "  Simdi test: Start-ScheduledTask -TaskName 'DLP-Daily-Backup'" -ForegroundColor White
Write-Host ""

Read-Host "Cikis icin Enter'a basin"
