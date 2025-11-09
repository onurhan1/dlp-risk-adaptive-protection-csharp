# Forcepoint DLP Risk Adaptive Protection - Windows Kurulum Scripti
# Bu script, Windows ortamında gerekli tüm yazılımları kurar ve yapılandırır

# ============================================================================
# YAPILANDIRMA
# ============================================================================
$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

# Renkler
function Write-ColorOutput($ForegroundColor) {
    $fc = $host.UI.RawUI.ForegroundColor
    $host.UI.RawUI.ForegroundColor = $ForegroundColor
    if ($args) {
        Write-Output $args
    }
    $host.UI.RawUI.ForegroundColor = $fc
}

function Write-Success { Write-ColorOutput Green $args }
function Write-Error { Write-ColorOutput Red $args }
function Write-Warning { Write-ColorOutput Yellow $args }
function Write-Info { Write-ColorOutput Cyan $args }

# ============================================================================
# KONTROLLER
# ============================================================================

Write-Info "=========================================="
Write-Info "Forcepoint DLP Risk Adaptive Protection"
Write-Info "Windows Kurulum Scripti"
Write-Info "=========================================="
Write-Info ""

# Yönetici yetkisi kontrolü
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Error "Bu script yönetici yetkisiyle çalıştırılmalıdır!"
    Write-Info "PowerShell'i 'Yönetici olarak çalıştır' seçeneğiyle açın."
    exit 1
}

# ============================================================================
# 1. .NET 8.0 SDK KURULUMU
# ============================================================================

Write-Info "[1/7] .NET 8.0 SDK kontrolü ve kurulumu..."

try {
    $dotnetVersion = dotnet --version 2>&1
    if ($LASTEXITCODE -eq 0 -and $dotnetVersion -match "^8\.0\.") {
        Write-Success "✓ .NET 8.0 SDK zaten kurulu: $dotnetVersion"
    } else {
        Write-Warning ".NET 8.0 SDK bulunamadı. Kurulum başlatılıyor..."
        
        # Winget ile kurulum dene
        if (Get-Command winget -ErrorAction SilentlyContinue) {
            Write-Info "Winget ile .NET 8.0 SDK kuruluyor..."
            winget install Microsoft.DotNet.SDK.8 --accept-package-agreements --accept-source-agreements
            if ($LASTEXITCODE -eq 0) {
                Write-Success "✓ .NET 8.0 SDK başarıyla kuruldu"
            } else {
                Write-Error "✗ Winget ile kurulum başarısız. Manuel kurulum gerekli."
                Write-Info "Lütfen https://dotnet.microsoft.com/download/dotnet/8.0 adresinden .NET 8.0 SDK'yı indirip kurun."
            }
        } else {
            Write-Error "✗ Winget bulunamadı. Manuel kurulum gerekli."
            Write-Info "Lütfen https://dotnet.microsoft.com/download/dotnet/8.0 adresinden .NET 8.0 SDK'yı indirip kurun."
        }
    }
} catch {
    Write-Error "✗ .NET SDK kontrolü başarısız: $_"
}

Write-Info ""

# ============================================================================
# 2. GIT KURULUMU
# ============================================================================

Write-Info "[2/7] Git kontrolü ve kurulumu..."

try {
    $gitVersion = git --version 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Success "✓ Git zaten kurulu: $gitVersion"
    } else {
        Write-Warning "Git bulunamadı. Kurulum başlatılıyor..."
        
        if (Get-Command winget -ErrorAction SilentlyContinue) {
            Write-Info "Winget ile Git kuruluyor..."
            winget install Git.Git --accept-package-agreements --accept-source-agreements
            if ($LASTEXITCODE -eq 0) {
                Write-Success "✓ Git başarıyla kuruldu"
                Write-Warning "Lütfen yeni bir PowerShell penceresi açın veya PATH'i yenileyin."
            } else {
                Write-Error "✗ Winget ile kurulum başarısız. Manuel kurulum gerekli."
                Write-Info "Lütfen https://git-scm.com/download/win adresinden Git'i indirip kurun."
            }
        } else {
            Write-Error "✗ Winget bulunamadı. Manuel kurulum gerekli."
            Write-Info "Lütfen https://git-scm.com/download/win adresinden Git'i indirip kurun."
        }
    }
} catch {
    Write-Error "✗ Git kontrolü başarısız: $_"
}

Write-Info ""

# ============================================================================
# 3. NODE.JS KURULUMU
# ============================================================================

Write-Info "[3/7] Node.js kontrolü ve kurulumu..."

try {
    $nodeVersion = node --version 2>&1
    if ($LASTEXITCODE -eq 0 -and $nodeVersion -match "^v(18|20|22)") {
        Write-Success "✓ Node.js zaten kurulu: $nodeVersion"
    } else {
        Write-Warning "Node.js 18+ bulunamadı. Kurulum başlatılıyor..."
        
        if (Get-Command winget -ErrorAction SilentlyContinue) {
            Write-Info "Winget ile Node.js LTS kuruluyor..."
            winget install OpenJS.NodeJS.LTS --accept-package-agreements --accept-source-agreements
            if ($LASTEXITCODE -eq 0) {
                Write-Success "✓ Node.js başarıyla kuruldu"
                Write-Warning "Lütfen yeni bir PowerShell penceresi açın veya PATH'i yenileyin."
            } else {
                Write-Error "✗ Winget ile kurulum başarısız. Manuel kurulum gerekli."
                Write-Info "Lütfen https://nodejs.org/ adresinden Node.js LTS'yi indirip kurun."
            }
        } else {
            Write-Error "✗ Winget bulunamadı. Manuel kurulum gerekli."
            Write-Info "Lütfen https://nodejs.org/ adresinden Node.js LTS'yi indirip kurun."
        }
    }
} catch {
    Write-Error "✗ Node.js kontrolü başarısız: $_"
}

Write-Info ""

# ============================================================================
# 4. DOCKER DESKTOP KURULUMU (Önerilen)
# ============================================================================

Write-Info "[4/7] Docker Desktop kontrolü..."

try {
    $dockerVersion = docker --version 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Success "✓ Docker zaten kurulu: $dockerVersion"
        
        # Docker servisinin çalıştığını kontrol et
        $dockerRunning = docker ps 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Success "✓ Docker servisi çalışıyor"
        } else {
            Write-Warning "Docker servisi çalışmıyor. Lütfen Docker Desktop'ı başlatın."
        }
    } else {
        Write-Warning "Docker bulunamadı."
        Write-Info "Docker Desktop kurulumu için:"
        Write-Info "1. https://www.docker.com/products/docker-desktop/ adresinden Docker Desktop'ı indirin"
        Write-Info "2. Kurulumu tamamlayın"
        Write-Info "3. Docker Desktop'ı başlatın"
        Write-Info ""
        Write-Info "Alternatif olarak PostgreSQL ve Redis'i manuel olarak kurabilirsiniz."
    }
} catch {
    Write-Warning "Docker kontrolü başarısız. Docker kurulu olmayabilir."
}

Write-Info ""

# ============================================================================
# 5. POSTGRESQL KURULUMU (Docker ile)
# ============================================================================

Write-Info "[5/7] PostgreSQL + TimescaleDB kurulumu (Docker ile)..."

if (Get-Command docker -ErrorAction SilentlyContinue) {
    $dockerRunning = docker ps 2>&1
    if ($LASTEXITCODE -eq 0) {
        # TimescaleDB container'ını kontrol et
        $timescaleContainer = docker ps -a --filter "name=timescaledb" --format "{{.Names}}" 2>&1
        if ($timescaleContainer -eq "timescaledb") {
            $containerRunning = docker ps --filter "name=timescaledb" --format "{{.Names}}" 2>&1
            if ($containerRunning -eq "timescaledb") {
                Write-Success "✓ TimescaleDB container'ı zaten çalışıyor"
            } else {
                Write-Info "TimescaleDB container'ı başlatılıyor..."
                docker start timescaledb
                if ($LASTEXITCODE -eq 0) {
                    Write-Success "✓ TimescaleDB container'ı başlatıldı"
                } else {
                    Write-Error "✗ TimescaleDB container'ı başlatılamadı"
                }
            }
        } else {
            Write-Info "TimescaleDB container'ı oluşturuluyor..."
            docker run -d `
                --name timescaledb `
                -e POSTGRES_PASSWORD=postgres `
                -e POSTGRES_DB=dlp_analyzer `
                -p 5432:5432 `
                timescale/timescaledb:latest-pg16
            
            if ($LASTEXITCODE -eq 0) {
                Write-Success "✓ TimescaleDB container'ı oluşturuldu ve başlatıldı"
                Write-Info "Veritabanı hazır olana kadar 10 saniye bekleniyor..."
                Start-Sleep -Seconds 10
            } else {
                Write-Error "✗ TimescaleDB container'ı oluşturulamadı"
            }
        }
    } else {
        Write-Warning "Docker servisi çalışmıyor. Lütfen Docker Desktop'ı başlatın."
    }
} else {
    Write-Warning "Docker bulunamadı. PostgreSQL'i manuel olarak kurmanız gerekiyor."
    Write-Info "PostgreSQL kurulumu için: https://www.postgresql.org/download/windows/"
}

Write-Info ""

# ============================================================================
# 6. REDIS KURULUMU (Docker ile)
# ============================================================================

Write-Info "[6/7] Redis kurulumu (Docker ile)..."

if (Get-Command docker -ErrorAction SilentlyContinue) {
    $dockerRunning = docker ps 2>&1
    if ($LASTEXITCODE -eq 0) {
        # Redis container'ını kontrol et
        $redisContainer = docker ps -a --filter "name=redis" --format "{{.Names}}" 2>&1
        if ($redisContainer -eq "redis") {
            $containerRunning = docker ps --filter "name=redis" --format "{{.Names}}" 2>&1
            if ($containerRunning -eq "redis") {
                Write-Success "✓ Redis container'ı zaten çalışıyor"
            } else {
                Write-Info "Redis container'ı başlatılıyor..."
                docker start redis
                if ($LASTEXITCODE -eq 0) {
                    Write-Success "✓ Redis container'ı başlatıldı"
                } else {
                    Write-Error "✗ Redis container'ı başlatılamadı"
                }
            }
        } else {
            Write-Info "Redis container'ı oluşturuluyor..."
            docker run -d `
                --name redis `
                -p 6379:6379 `
                redis:7-alpine
            
            if ($LASTEXITCODE -eq 0) {
                Write-Success "✓ Redis container'ı oluşturuldu ve başlatıldı"
            } else {
                Write-Error "✗ Redis container'ı oluşturulamadı"
            }
        }
    } else {
        Write-Warning "Docker servisi çalışmıyor. Lütfen Docker Desktop'ı başlatın."
    }
} else {
    Write-Warning "Docker bulunamadı. Redis'i manuel olarak kurmanız gerekiyor."
    Write-Info "Redis kurulumu için: https://www.memurai.com/get-memurai (Memurai - Windows için)"
}

Write-Info ""

# ============================================================================
# 7. PROJE KURULUMU
# ============================================================================

Write-Info "[7/7] Proje kurulumu..."

$projectPath = $PSScriptRoot

if (Test-Path "$projectPath\DLP.RiskAnalyzer.Solution.sln") {
    Write-Info "Proje bulundu: $projectPath"
    
    # NuGet paketlerini restore et
    Write-Info "NuGet paketleri restore ediliyor..."
    Set-Location $projectPath
    dotnet restore DLP.RiskAnalyzer.Solution.sln
    
    if ($LASTEXITCODE -eq 0) {
        Write-Success "✓ NuGet paketleri başarıyla restore edildi"
    } else {
        Write-Error "✗ NuGet paketleri restore edilemedi"
    }
    
    # Projeyi build et
    Write-Info "Proje build ediliyor..."
    dotnet build DLP.RiskAnalyzer.Solution.sln --no-restore
    
    if ($LASTEXITCODE -eq 0) {
        Write-Success "✓ Proje başarıyla build edildi"
    } else {
        Write-Error "✗ Proje build edilemedi"
    }
    
    # Dashboard bağımlılıklarını yükle
    if (Test-Path "$projectPath\dashboard\package.json") {
        Write-Info "Dashboard bağımlılıkları yükleniyor..."
        Set-Location "$projectPath\dashboard"
        
        if (Get-Command npm -ErrorAction SilentlyContinue) {
            npm install
            if ($LASTEXITCODE -eq 0) {
                Write-Success "✓ Dashboard bağımlılıkları başarıyla yüklendi"
            } else {
                Write-Error "✗ Dashboard bağımlılıkları yüklenemedi"
            }
        } else {
            Write-Warning "npm bulunamadı. Node.js kurulumunu kontrol edin."
        }
    }
} else {
    Write-Warning "Proje bulunamadı. Lütfen script'i proje dizininde çalıştırın."
}

Write-Info ""

# ============================================================================
# ÖZET
# ============================================================================

Write-Info "=========================================="
Write-Info "KURULUM ÖZETİ"
Write-Info "=========================================="
Write-Info ""

# Kurulum durumunu kontrol et
$checks = @()

# .NET SDK
try {
    $dotnetVersion = dotnet --version 2>&1
    if ($LASTEXITCODE -eq 0 -and $dotnetVersion -match "^8\.0\.") {
        $checks += @{ Name = ".NET 8.0 SDK"; Status = "✓"; Version = $dotnetVersion }
    } else {
        $checks += @{ Name = ".NET 8.0 SDK"; Status = "✗"; Version = "Kurulu değil" }
    }
} catch {
    $checks += @{ Name = ".NET 8.0 SDK"; Status = "✗"; Version = "Kurulu değil" }
}

# Git
try {
    $gitVersion = git --version 2>&1
    if ($LASTEXITCODE -eq 0) {
        $checks += @{ Name = "Git"; Status = "✓"; Version = $gitVersion }
    } else {
        $checks += @{ Name = "Git"; Status = "✗"; Version = "Kurulu değil" }
    }
} catch {
    $checks += @{ Name = "Git"; Status = "✗"; Version = "Kurulu değil" }
}

# Node.js
try {
    $nodeVersion = node --version 2>&1
    if ($LASTEXITCODE -eq 0) {
        $checks += @{ Name = "Node.js"; Status = "✓"; Version = $nodeVersion }
    } else {
        $checks += @{ Name = "Node.js"; Status = "✗"; Version = "Kurulu değil" }
    }
} catch {
    $checks += @{ Name = "Node.js"; Status = "✗"; Version = "Kurulu değil" }
}

# Docker
try {
    $dockerVersion = docker --version 2>&1
    if ($LASTEXITCODE -eq 0) {
        $checks += @{ Name = "Docker"; Status = "✓"; Version = $dockerVersion }
    } else {
        $checks += @{ Name = "Docker"; Status = "✗"; Version = "Kurulu değil" }
    }
} catch {
    $checks += @{ Name = "Docker"; Status = "✗"; Version = "Kurulu değil" }
}

# PostgreSQL (Docker)
try {
    $pgContainer = docker ps --filter "name=timescaledb" --format "{{.Names}}" 2>&1
    if ($pgContainer -eq "timescaledb") {
        $checks += @{ Name = "PostgreSQL (TimescaleDB)"; Status = "✓"; Version = "Docker Container" }
    } else {
        $checks += @{ Name = "PostgreSQL (TimescaleDB)"; Status = "✗"; Version = "Çalışmıyor" }
    }
} catch {
    $checks += @{ Name = "PostgreSQL (TimescaleDB)"; Status = "✗"; Version = "Kurulu değil" }
}

# Redis (Docker)
try {
    $redisContainer = docker ps --filter "name=redis" --format "{{.Names}}" 2>&1
    if ($redisContainer -eq "redis") {
        $checks += @{ Name = "Redis"; Status = "✓"; Version = "Docker Container" }
    } else {
        $checks += @{ Name = "Redis"; Status = "✗"; Version = "Çalışmıyor" }
    }
} catch {
    $checks += @{ Name = "Redis"; Status = "✗"; Version = "Kurulu değil" }
}

# Sonuçları göster
foreach ($check in $checks) {
    if ($check.Status -eq "✓") {
        Write-Success "$($check.Status) $($check.Name) - $($check.Version)"
    } else {
        Write-Error "$($check.Status) $($check.Name) - $($check.Version)"
    }
}

Write-Info ""
Write-Info "=========================================="
Write-Info "SONRAKI ADIMLAR"
Write-Info "=========================================="
Write-Info ""
Write-Info "1. appsettings.json dosyalarını yapılandırın:"
Write-Info "   - DLP.RiskAnalyzer.Collector\appsettings.json"
Write-Info "   - DLP.RiskAnalyzer.Analyzer\appsettings.json"
Write-Info ""
Write-Info "2. Forcepoint DLP Manager bilgilerini ekleyin:"
Write-Info "   - ManagerIP"
Write-Info "   - Username"
Write-Info "   - Password"
Write-Info ""
Write-Info "3. Veritabanı migration'larını çalıştırın:"
Write-Info "   cd DLP.RiskAnalyzer.Analyzer"
Write-Info "   dotnet ef database update"
Write-Info ""
Write-Info "4. Servisleri başlatın:"
Write-Info "   - Analyzer API: cd DLP.RiskAnalyzer.Analyzer && dotnet run"
Write-Info "   - Collector Service: cd DLP.RiskAnalyzer.Collector && dotnet run"
Write-Info "   - Dashboard: cd dashboard && npm run dev"
Write-Info ""
Write-Info "Detaylı bilgi için:"
Write-Info "   - WINDOWS_KURULUM_REHBERI.md"
Write-Info "   - WINDOWS_API_BAGLANTI_REHBERI.md"
Write-Info ""
Write-Success "Kurulum tamamlandı! 🎉"

