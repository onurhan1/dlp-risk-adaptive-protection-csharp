# Forcepoint Risk Adaptive Protection - Windows Dependencies Installer
# Bu script, Windows ortamında gerekli tüm bağımlılıkları otomatik olarak kurar
# Python'daki pip install -r requirements.txt komutuna benzer işlev görür

param(
    [switch]$SkipDotNet,
    [switch]$SkipPostgreSQL,
    [switch]$SkipRedis,
    [switch]$SkipNodeJS,
    [switch]$Force
)

$ErrorActionPreference = "Stop"

Write-Host "=== Forcepoint Risk Adaptive Protection - Dependency Installer ===" -ForegroundColor Green
Write-Host ""

# Admin kontrolü
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "⚠️  Bazı kurulumlar için yönetici hakları gerekebilir." -ForegroundColor Yellow
    Write-Host ""
}

# ============================================================================
# 1. .NET SDK 8.0 KURULUMU
# ============================================================================
if (-not $SkipDotNet) {
    Write-Host "[1/5] .NET SDK 8.0 Kontrolü..." -ForegroundColor Cyan
    
    $dotnetVersion = dotnet --version 2>$null
    if ($LASTEXITCODE -eq 0) {
        $versionParts = $dotnetVersion -split '\.'
        $majorVersion = [int]$versionParts[0]
        $minorVersion = [int]$versionParts[1]
        
        if ($majorVersion -eq 8) {
            Write-Host "✅ .NET SDK $dotnetVersion zaten kurulu" -ForegroundColor Green
        } else {
            Write-Host "⚠️  .NET SDK 8.0 bulunamadı (Mevcut: $dotnetVersion)" -ForegroundColor Yellow
            Write-Host "📥 .NET SDK 8.0 kuruluyor..." -ForegroundColor Cyan
            
            # Winget ile kurulum
            if (Get-Command winget -ErrorAction SilentlyContinue) {
                winget install Microsoft.DotNet.SDK.8 --accept-package-agreements --accept-source-agreements
                if ($LASTEXITCODE -eq 0) {
                    Write-Host "✅ .NET SDK 8.0 kuruldu" -ForegroundColor Green
                } else {
                    Write-Host "❌ .NET SDK kurulumu başarısız. Manuel kurulum gerekebilir." -ForegroundColor Red
                    Write-Host "   URL: https://dotnet.microsoft.com/download/dotnet/8.0" -ForegroundColor Yellow
                }
            } else {
                Write-Host "⚠️  Winget bulunamadı. Manuel kurulum gerekli:" -ForegroundColor Yellow
                Write-Host "   https://dotnet.microsoft.com/download/dotnet/8.0" -ForegroundColor Cyan
            }
        }
    } else {
        Write-Host "⚠️  .NET SDK bulunamadı. Kuruluyor..." -ForegroundColor Yellow
        if (Get-Command winget -ErrorAction SilentlyContinue) {
            winget install Microsoft.DotNet.SDK.8 --accept-package-agreements --accept-source-agreements
        } else {
            Write-Host "❌ Winget bulunamadı. Lütfen manuel olarak kurun:" -ForegroundColor Red
            Write-Host "   https://dotnet.microsoft.com/download/dotnet/8.0" -ForegroundColor Yellow
        }
    }
    Write-Host ""
}

# ============================================================================
# 2. POSTGRESQL + TIMESCALEDB KURULUMU
# ============================================================================
if (-not $SkipPostgreSQL) {
    Write-Host "[2/5] PostgreSQL Kontrolü..." -ForegroundColor Cyan
    
    # Docker ile kontrol
    $dockerPg = docker ps -a --filter "name=timescaledb" --format "{{.Names}}" 2>$null
    if ($dockerPg) {
        Write-Host "✅ PostgreSQL (Docker) çalışıyor: $dockerPg" -ForegroundColor Green
    } else {
        # Yerel PostgreSQL kontrolü
        $pgService = Get-Service -Name postgresql* -ErrorAction SilentlyContinue
        if ($pgService) {
            Write-Host "✅ PostgreSQL servisi bulundu" -ForegroundColor Green
        } else {
            Write-Host "⚠️  PostgreSQL bulunamadı" -ForegroundColor Yellow
            Write-Host "📋 Seçenekler:" -ForegroundColor Cyan
            Write-Host "   1. Docker ile (Önerilen): docker run -d --name timescaledb -e POSTGRES_PASSWORD=postgres -p 5432:5432 timescale/timescaledb:latest-pg16" -ForegroundColor Gray
            Write-Host "   2. Manuel kurulum: https://www.postgresql.org/download/windows/" -ForegroundColor Gray
            Write-Host ""
        }
    }
    Write-Host ""
}

# ============================================================================
# 3. REDIS KURULUMU
# ============================================================================
if (-not $SkipRedis) {
    Write-Host "[3/5] Redis Kontrolü..." -ForegroundColor Cyan
    
    # Docker ile kontrol
    $dockerRedis = docker ps -a --filter "name=redis" --format "{{.Names}}" 2>$null
    if ($dockerRedis) {
        Write-Host "✅ Redis (Docker) çalışıyor: $dockerRedis" -ForegroundColor Green
    } else {
        # Memurai kontrolü (Windows için)
        $memuraiService = Get-Service -Name Memurai* -ErrorAction SilentlyContinue
        if ($memuraiService) {
            Write-Host "✅ Redis (Memurai) servisi bulundu" -ForegroundColor Green
        } else {
            Write-Host "⚠️  Redis bulunamadı" -ForegroundColor Yellow
            Write-Host "📋 Seçenekler:" -ForegroundColor Cyan
            Write-Host "   1. Docker ile: docker run -d --name redis -p 6379:6379 redis:7-alpine" -ForegroundColor Gray
            Write-Host "   2. Memurai (Windows): https://www.memurai.com/get-memurai" -ForegroundColor Gray
            Write-Host ""
        }
    }
    Write-Host ""
}

# ============================================================================
# 4. NODE.JS KURULUMU (Dashboard için)
# ============================================================================
if (-not $SkipNodeJS) {
    Write-Host "[4/5] Node.js Kontrolü..." -ForegroundColor Cyan
    
    $nodeVersion = node --version 2>$null
    if ($LASTEXITCODE -eq 0) {
        $versionNum = [int]($nodeVersion -replace 'v(\d+)\..*', '$1')
        if ($versionNum -ge 18) {
            Write-Host "✅ Node.js $nodeVersion zaten kurulu" -ForegroundColor Green
        } else {
            Write-Host "⚠️  Node.js 18+ gerekli (Mevcut: $nodeVersion)" -ForegroundColor Yellow
            Write-Host "📥 Node.js güncelleniyor..." -ForegroundColor Cyan
            if (Get-Command winget -ErrorAction SilentlyContinue) {
                winget install OpenJS.NodeJS.LTS --accept-package-agreements --accept-source-agreements
            }
        }
    } else {
        Write-Host "⚠️  Node.js bulunamadı. Kuruluyor..." -ForegroundColor Yellow
        if (Get-Command winget -ErrorAction SilentlyContinue) {
            winget install OpenJS.NodeJS.LTS --accept-package-agreements --accept-source-agreements
            Write-Host "✅ Node.js kuruldu" -ForegroundColor Green
        } else {
            Write-Host "❌ Winget bulunamadı. Lütfen manuel olarak kurun:" -ForegroundColor Red
            Write-Host "   https://nodejs.org/" -ForegroundColor Yellow
        }
    }
    Write-Host ""
}

# ============================================================================
# 5. NUGET PAKET RESTORE
# ============================================================================
Write-Host "[5/5] NuGet Paket Restore..." -ForegroundColor Cyan

if (Test-Path "DLP.RiskAnalyzer.Solution.sln") {
    Write-Host "📦 Solution restore ediliyor..." -ForegroundColor Cyan
    dotnet restore DLP.RiskAnalyzer.Solution.sln
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ NuGet paketleri restore edildi" -ForegroundColor Green
    } else {
        Write-Host "⚠️  NuGet restore sırasında uyarılar olabilir" -ForegroundColor Yellow
    }
} else {
    Write-Host "⚠️  Solution dosyası bulunamadı. Proje klasöründe olduğunuzdan emin olun." -ForegroundColor Yellow
}
Write-Host ""

# ============================================================================
# 6. NPM PAKETLERİ (Dashboard için)
# ============================================================================
if (Test-Path "dashboard\package.json") {
    Write-Host "[6/6] NPM Paket Kurulumu (Dashboard)..." -ForegroundColor Cyan
    Push-Location dashboard
    
    if (-not (Test-Path "node_modules")) {
        Write-Host "📦 npm install çalıştırılıyor..." -ForegroundColor Cyan
        npm install
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ NPM paketleri kuruldu" -ForegroundColor Green
        } else {
            Write-Host "⚠️  NPM install sırasında hatalar olabilir" -ForegroundColor Yellow
        }
    } else {
        Write-Host "✅ node_modules zaten mevcut" -ForegroundColor Green
    }
    
    Pop-Location
    Write-Host ""
}

# ============================================================================
# ÖZET
# ============================================================================
Write-Host "=== Kurulum Özeti ===" -ForegroundColor Green
Write-Host ""

# Servis durum kontrolü
Write-Host "📊 Servis Durumları:" -ForegroundColor Cyan

# .NET SDK
$dotnetCheck = dotnet --version 2>$null
if ($LASTEXITCODE -eq 0) {
    Write-Host "   ✅ .NET SDK: $dotnetCheck" -ForegroundColor Green
} else {
    Write-Host "   ❌ .NET SDK: Kurulu değil" -ForegroundColor Red
}

# PostgreSQL
$pgCheck = docker ps --filter "name=timescaledb" --format "{{.Names}}" 2>$null
if (-not $pgCheck) {
    $pgCheck = Get-Service -Name postgresql* -ErrorAction SilentlyContinue
}
if ($pgCheck) {
    Write-Host "   ✅ PostgreSQL: Çalışıyor" -ForegroundColor Green
} else {
    Write-Host "   ⚠️  PostgreSQL: Kontrol edilmeli" -ForegroundColor Yellow
}

# Redis
$redisCheck = docker ps --filter "name=redis" --format "{{.Names}}" 2>$null
if (-not $redisCheck) {
    $redisCheck = Get-Service -Name Memurai* -ErrorAction SilentlyContinue
}
if ($redisCheck) {
    Write-Host "   ✅ Redis: Çalışıyor" -ForegroundColor Green
} else {
    Write-Host "   ⚠️  Redis: Kontrol edilmeli" -ForegroundColor Yellow
}

# Node.js
$nodeCheck = node --version 2>$null
if ($LASTEXITCODE -eq 0) {
    Write-Host "   ✅ Node.js: $nodeCheck" -ForegroundColor Green
} else {
    Write-Host "   ❌ Node.js: Kurulu değil" -ForegroundColor Red
}

# npm
$npmCheck = npm --version 2>$null
if ($LASTEXITCODE -eq 0) {
    Write-Host "   ✅ npm: $npmCheck" -ForegroundColor Green
} else {
    Write-Host "   ❌ npm: Kurulu değil" -ForegroundColor Red
}

Write-Host ""
Write-Host "✅ Kurulum tamamlandı!" -ForegroundColor Green
Write-Host ""
Write-Host "📋 Sonraki Adımlar:" -ForegroundColor Cyan
Write-Host "   1. appsettings.json dosyalarını yapılandırın" -ForegroundColor Gray
Write-Host "   2. Veritabanı migration'larını çalıştırın: dotnet ef database update" -ForegroundColor Gray
Write-Host "   3. Servisleri başlatın (WINDOWS_INSTALLATION.md'ye bakın)" -ForegroundColor Gray
Write-Host ""

