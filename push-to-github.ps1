# GitHub Push Script (PowerShell - Windows)
# Bu script projeyi GitHub'a push eder

Write-Host "=== GitHub Push Script ===" -ForegroundColor Cyan
Write-Host ""

$ErrorActionPreference = "Continue"

# Remote kontrolü
$remoteUrl = git remote get-url origin 2>$null
if (-not $remoteUrl) {
    Write-Host "⚠️  Remote repository bulunamadı. Ekleniyor..." -ForegroundColor Yellow
    git remote add origin https://github.com/onurhan1/dlp-risk-adaptive-protection-csharp.git
}

Write-Host "📋 Remote Repository:" -ForegroundColor Cyan
git remote -v
Write-Host ""

# Repository var mı kontrol et
Write-Host "🔍 Repository durumu kontrol ediliyor..." -ForegroundColor Cyan
$repoCheck = git ls-remote --heads origin main 2>$null
if ($LASTEXITCODE -ne 0) {
    Write-Host "⚠️  Repository GitHub'da bulunamadı!" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "📝 Lütfen önce GitHub'da repository oluşturun:" -ForegroundColor Cyan
    Write-Host "   1. https://github.com/new adresine gidin"
    Write-Host "   2. Repository name: dlp-risk-adaptive-protection-csharp"
    Write-Host "   3. Description: Forcepoint Risk Adaptive Protection - C# Implementation"
    Write-Host "   4. ✅ Private seçin"
    Write-Host "   5. ❌ README, .gitignore, license işaretlemeyin"
    Write-Host "   6. 'Create repository' tıklayın"
    Write-Host ""
    Write-Host "Repository oluşturduktan sonra tekrar bu script'i çalıştırın." -ForegroundColor Cyan
    exit 1
}

Write-Host "✅ Repository bulundu!" -ForegroundColor Green
Write-Host ""

# Değişiklikleri kontrol et
$changes = git status --porcelain
if ($changes) {
    Write-Host "📦 Değişiklikler bulundu, commit ediliyor..." -ForegroundColor Cyan
    git add .
    
    $commitMsg = Read-Host "Commit mesajı girin (Enter için default)"
    if ([string]::IsNullOrWhiteSpace($commitMsg)) {
        $commitMsg = "Update: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
    }
    
    git commit -m $commitMsg
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Commit başarısız!" -ForegroundColor Red
        exit 1
    }
    
    Write-Host "✅ Commit oluşturuldu" -ForegroundColor Green
    Write-Host ""
}

# Push et
Write-Host "🚀 GitHub'a push ediliyor..." -ForegroundColor Cyan
Write-Host "Not: GitHub kullanıcı adı ve şifreniz istenebilir" -ForegroundColor Yellow
Write-Host ""

git push -u origin main

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "╔════════════════════════════════════════════════╗" -ForegroundColor Green
    Write-Host "║   ✅ BAŞARILIYLA PUSH EDİLDİ!                  ║" -ForegroundColor Green
    Write-Host "╚════════════════════════════════════════════════╝" -ForegroundColor Green
    Write-Host ""
    Write-Host "📦 Repository URL:" -ForegroundColor Cyan
    Write-Host "   https://github.com/onurhan1/dlp-risk-adaptive-protection-csharp" -ForegroundColor Green
    Write-Host ""
} else {
    Write-Host ""
    Write-Host "❌ Push başarısız!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Olası nedenler:" -ForegroundColor Yellow
    Write-Host "   1. GitHub kimlik doğrulama hatası"
    Write-Host "   2. Repository henüz oluşturulmamış"
    Write-Host "   3. İnternet bağlantısı sorunu"
    Write-Host ""
    Write-Host "Çözümler:" -ForegroundColor Cyan
    Write-Host "   - SSH key kullanın: git remote set-url origin git@github.com:onurhan1/dlp-risk-adaptive-protection-csharp.git"
    Write-Host "   - Personal Access Token kullanın"
    Write-Host "   - Repository'nin oluşturulduğundan emin olun"
    exit 1
}

