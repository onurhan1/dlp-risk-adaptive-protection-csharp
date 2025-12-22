# ============================================================================
# DLP Risk Analyzer - Auto Start Setup Script
# Bu script sunucu restart sonrası servislerin otomatik başlamasını sağlar
# Administrator olarak çalıştırılmalıdır!
# ============================================================================

# Renk kodları
$Green = "Green"
$Yellow = "Yellow"
$Red = "Red"
$Cyan = "Cyan"

Write-Host ""
Write-Host "============================================================" -ForegroundColor $Cyan
Write-Host "    DLP Risk Analyzer - Auto Start Setup" -ForegroundColor $Cyan
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

# Proje dizini
$ProjectRoot = "C:\Users\abdul\Desktop\dlp-risk-adaptive-protection-csharp-main"

# Servislerin konumları (her biri kendi dizininde çalışacak)
$AnalyzerPath = Join-Path $ProjectRoot "DLP.RiskAnalyzer.Analyzer"
$CollectorPath = Join-Path $ProjectRoot "DLP.RiskAnalyzer.Collector"
$DashboardPath = Join-Path $ProjectRoot "dashboard"

# Var olan görevleri sil (güncelleme için)
Write-Host ""
Write-Host "[*] Mevcut gorevler kontrol ediliyor..." -ForegroundColor $Yellow

$tasksToRemove = @("DLP-Analyzer", "DLP-Collector", "DLP-Dashboard")
foreach ($task in $tasksToRemove) {
    $existingTask = Get-ScheduledTask -TaskName $task -ErrorAction SilentlyContinue
    if ($existingTask) {
        Write-Host "    Mevcut gorev siliniyor: $task" -ForegroundColor $Yellow
        Unregister-ScheduledTask -TaskName $task -Confirm:$false
    }
}

# ============================================================================
# 1. DLP Analyzer Görevi (Kendi dizininde çalışır)
# ============================================================================
Write-Host ""
Write-Host "[1/3] DLP Analyzer gorevi olusturuluyor..." -ForegroundColor $Cyan

$AnalyzerAction = New-ScheduledTaskAction `
    -Execute "dotnet" `
    -Argument "run" `
    -WorkingDirectory $AnalyzerPath

$AnalyzerTrigger = New-ScheduledTaskTrigger -AtStartup
$AnalyzerTrigger.Delay = "PT30S"  # 30 saniye gecikme (PostgreSQL/Redis başlaması için)

$AnalyzerSettings = New-ScheduledTaskSettingsSet `
    -AllowStartIfOnBatteries `
    -DontStopIfGoingOnBatteries `
    -StartWhenAvailable `
    -RestartInterval (New-TimeSpan -Minutes 1) `
    -RestartCount 3 `
    -ExecutionTimeLimit (New-TimeSpan -Days 365)

$AnalyzerPrincipal = New-ScheduledTaskPrincipal `
    -UserId "SYSTEM" `
    -LogonType ServiceAccount `
    -RunLevel Highest

Register-ScheduledTask `
    -TaskName "DLP-Analyzer" `
    -Action $AnalyzerAction `
    -Trigger $AnalyzerTrigger `
    -Settings $AnalyzerSettings `
    -Principal $AnalyzerPrincipal `
    -Description "DLP Risk Analyzer API Servisi - Otomatik Baslatma" | Out-Null

Write-Host "    [OK] DLP-Analyzer gorevi olusturuldu" -ForegroundColor $Green

# ============================================================================
# 2. DLP Collector Görevi (Kendi dizininde çalışır)
# ============================================================================
Write-Host ""
Write-Host "[2/3] DLP Collector gorevi olusturuluyor..." -ForegroundColor $Cyan

$CollectorAction = New-ScheduledTaskAction `
    -Execute "dotnet" `
    -Argument "run" `
    -WorkingDirectory $CollectorPath

$CollectorTrigger = New-ScheduledTaskTrigger -AtStartup
$CollectorTrigger.Delay = "PT45S"  # 45 saniye gecikme (Analyzer'dan sonra)

$CollectorSettings = New-ScheduledTaskSettingsSet `
    -AllowStartIfOnBatteries `
    -DontStopIfGoingOnBatteries `
    -StartWhenAvailable `
    -RestartInterval (New-TimeSpan -Minutes 1) `
    -RestartCount 3 `
    -ExecutionTimeLimit (New-TimeSpan -Days 365)

$CollectorPrincipal = New-ScheduledTaskPrincipal `
    -UserId "SYSTEM" `
    -LogonType ServiceAccount `
    -RunLevel Highest

Register-ScheduledTask `
    -TaskName "DLP-Collector" `
    -Action $CollectorAction `
    -Trigger $CollectorTrigger `
    -Settings $CollectorSettings `
    -Principal $CollectorPrincipal `
    -Description "DLP Incident Collector Servisi - Otomatik Baslatma" | Out-Null

Write-Host "    [OK] DLP-Collector gorevi olusturuldu" -ForegroundColor $Green

# ============================================================================
# 3. DLP Dashboard Görevi
# ============================================================================
Write-Host ""
Write-Host "[3/3] DLP Dashboard gorevi olusturuluyor..." -ForegroundColor $Cyan

# Dashboard için batch dosyası oluştur (npm run dev için)
$DashboardBatchPath = Join-Path $ProjectRoot "start-dashboard-service.bat"
$DashboardBatchContent = @"
@echo off
cd /d "$DashboardPath"
call npm run dev
"@
Set-Content -Path $DashboardBatchPath -Value $DashboardBatchContent -Encoding ASCII

$DashboardAction = New-ScheduledTaskAction `
    -Execute $DashboardBatchPath `
    -WorkingDirectory $DashboardPath

$DashboardTrigger = New-ScheduledTaskTrigger -AtStartup
$DashboardTrigger.Delay = "PT60S"  # 60 saniye gecikme (API'ler başladıktan sonra)

$DashboardSettings = New-ScheduledTaskSettingsSet `
    -AllowStartIfOnBatteries `
    -DontStopIfGoingOnBatteries `
    -StartWhenAvailable `
    -RestartInterval (New-TimeSpan -Minutes 1) `
    -RestartCount 3 `
    -ExecutionTimeLimit (New-TimeSpan -Days 365)

$DashboardPrincipal = New-ScheduledTaskPrincipal `
    -UserId "SYSTEM" `
    -LogonType ServiceAccount `
    -RunLevel Highest

Register-ScheduledTask `
    -TaskName "DLP-Dashboard" `
    -Action $DashboardAction `
    -Trigger $DashboardTrigger `
    -Settings $DashboardSettings `
    -Principal $DashboardPrincipal `
    -Description "DLP Dashboard (Next.js) - Otomatik Baslatma" | Out-Null

Write-Host "    [OK] DLP-Dashboard gorevi olusturuldu" -ForegroundColor $Green

# ============================================================================
# Özet
# ============================================================================
Write-Host ""
Write-Host "============================================================" -ForegroundColor $Cyan
Write-Host "    KURULUM TAMAMLANDI!" -ForegroundColor $Green
Write-Host "============================================================" -ForegroundColor $Cyan
Write-Host ""
Write-Host "Olusturulan gorevler:" -ForegroundColor $Yellow
Write-Host ""

# Görevleri listele
$tasks = Get-ScheduledTask -TaskName "DLP-*" | Select-Object TaskName, State
$tasks | ForEach-Object {
    $status = if ($_.State -eq "Ready") { "[Hazir]" } else { "[$($_.State)]" }
    Write-Host "  - $($_.TaskName) $status" -ForegroundColor $Green
}

Write-Host ""
Write-Host "Baslama sirasi:" -ForegroundColor $Yellow
Write-Host "  1. PostgreSQL ve Redis (zaten Automatic)" -ForegroundColor White
Write-Host "  2. DLP-Analyzer (30 sn sonra)" -ForegroundColor White
Write-Host "  3. DLP-Collector (45 sn sonra)" -ForegroundColor White
Write-Host "  4. DLP-Dashboard (60 sn sonra)" -ForegroundColor White
Write-Host ""
Write-Host "Gorevleri yonetmek icin:" -ForegroundColor $Yellow
Write-Host "  Task Scheduler: taskschd.msc" -ForegroundColor White
Write-Host ""
Write-Host "Simdi test etmek icin:" -ForegroundColor $Yellow
Write-Host "  Start-ScheduledTask -TaskName 'DLP-Analyzer'" -ForegroundColor White
Write-Host "  Start-ScheduledTask -TaskName 'DLP-Collector'" -ForegroundColor White
Write-Host "  Start-ScheduledTask -TaskName 'DLP-Dashboard'" -ForegroundColor White
Write-Host ""

Read-Host "Cikis icin Enter'a basin"
