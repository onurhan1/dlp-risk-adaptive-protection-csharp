# DLP Risk Analyzer - Quick Start Script
# PowerShell script to quickly set up and start all services

param(
    [switch]$SkipBuild = $false,
    [switch]$SkipMigration = $false
)

Write-Host "=== DLP Risk Analyzer C# - Quick Start ===" -ForegroundColor Green
Write-Host ""

# Check .NET SDK
Write-Host "[1/6] Checking .NET SDK..." -ForegroundColor Yellow
$dotnetVersion = dotnet --version 2>$null
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: .NET SDK not found. Please install .NET 8.0 SDK." -ForegroundColor Red
    exit 1
}
Write-Host "  .NET SDK Version: $dotnetVersion" -ForegroundColor Green

# Check if we're in the right directory
if (-not (Test-Path "DLP.RiskAnalyzer.Solution.sln")) {
    Write-Host "ERROR: Solution file not found. Please run this script from the project root directory." -ForegroundColor Red
    exit 1
}

# Restore packages
Write-Host ""
Write-Host "[2/6] Restoring NuGet packages..." -ForegroundColor Yellow
dotnet restore
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Package restore failed." -ForegroundColor Red
    exit 1
}
Write-Host "  Packages restored successfully." -ForegroundColor Green

# Build solution
if (-not $SkipBuild) {
    Write-Host ""
    Write-Host "[3/6] Building solution..." -ForegroundColor Yellow
    dotnet build -c Release
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: Build failed." -ForegroundColor Red
        exit 1
    }
    Write-Host "  Build succeeded." -ForegroundColor Green
}

# Check PostgreSQL connection
Write-Host ""
Write-Host "[4/6] Checking PostgreSQL connection..." -ForegroundColor Yellow
try {
    $pgTest = Test-NetConnection -ComputerName localhost -Port 5432 -WarningAction SilentlyContinue
    if ($pgTest.TcpTestSucceeded) {
        Write-Host "  PostgreSQL is reachable on port 5432." -ForegroundColor Green
    } else {
        Write-Host "  WARNING: Cannot connect to PostgreSQL on port 5432." -ForegroundColor Yellow
        Write-Host "  Please ensure PostgreSQL is running." -ForegroundColor Yellow
    }
} catch {
    Write-Host "  WARNING: Could not check PostgreSQL connection." -ForegroundColor Yellow
}

# Check Redis connection
Write-Host ""
Write-Host "[5/6] Checking Redis connection..." -ForegroundColor Yellow
try {
    $redisTest = Test-NetConnection -ComputerName localhost -Port 6379 -WarningAction SilentlyContinue
    if ($redisTest.TcpTestSucceeded) {
        Write-Host "  Redis is reachable on port 6379." -ForegroundColor Green
    } else {
        Write-Host "  WARNING: Cannot connect to Redis on port 6379." -ForegroundColor Yellow
        Write-Host "  Please ensure Redis is running (Docker/Memurai/WSL2)." -ForegroundColor Yellow
    }
} catch {
    Write-Host "  WARNING: Could not check Redis connection." -ForegroundColor Yellow
}

# Database migration
if (-not $SkipMigration) {
    Write-Host ""
    Write-Host "[6/6] Running database migrations..." -ForegroundColor Yellow
    
    # Check if dotnet-ef is installed
    $efInstalled = dotnet tool list -g | Select-String "dotnet-ef"
    if (-not $efInstalled) {
        Write-Host "  Installing Entity Framework Core Tools..." -ForegroundColor Cyan
        dotnet tool install --global dotnet-ef --version 8.0.0
    }
    
    Push-Location "DLP.RiskAnalyzer.Analyzer"
    dotnet ef database update --no-build
    Pop-Location
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  Database migrations completed." -ForegroundColor Green
    } else {
        Write-Host "  WARNING: Database migration failed. You may need to run it manually." -ForegroundColor Yellow
        Write-Host "  Command: cd DLP.RiskAnalyzer.Analyzer && dotnet ef database update" -ForegroundColor Yellow
    }
}

# Start services
Write-Host ""
Write-Host "=== Starting Services ===" -ForegroundColor Green
Write-Host ""

# Get current directory
$projectRoot = $PWD.Path

# Start Analyzer API
Write-Host "[1/3] Starting Analyzer API (http://localhost:8000)..." -ForegroundColor Cyan
Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$projectRoot\DLP.RiskAnalyzer.Analyzer'; Write-Host 'Analyzer API starting...' -ForegroundColor Green; dotnet run" -WindowStyle Normal

Start-Sleep -Seconds 5

# Start Collector
Write-Host "[2/3] Starting Collector Service..." -ForegroundColor Cyan
Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$projectRoot\DLP.RiskAnalyzer.Collector'; Write-Host 'Collector Service starting...' -ForegroundColor Green; dotnet run" -WindowStyle Normal

Start-Sleep -Seconds 3

# Start Dashboard
Write-Host "[3/3] Starting WPF Dashboard..." -ForegroundColor Cyan
Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$projectRoot\DLP.RiskAnalyzer.Dashboard'; Write-Host 'Dashboard starting...' -ForegroundColor Green; dotnet run" -WindowStyle Normal

Write-Host ""
Write-Host "=== All Services Started! ===" -ForegroundColor Green
Write-Host ""
Write-Host "📍 URLs:" -ForegroundColor Yellow
Write-Host "  • Analyzer API: http://localhost:8000" -ForegroundColor Cyan
Write-Host "  • Swagger UI:   http://localhost:8000/swagger" -ForegroundColor Cyan
Write-Host "  • Dashboard:    WPF window should open automatically" -ForegroundColor Cyan
Write-Host ""
Write-Host "💡 Tips:" -ForegroundColor Yellow
Write-Host "  • Check API health: Invoke-WebRequest http://localhost:8000/health" -ForegroundColor Gray
Write-Host "  • View API docs: Open http://localhost:8000/swagger in browser" -ForegroundColor Gray
Write-Host "  • Stop services: Close the PowerShell windows" -ForegroundColor Gray
Write-Host ""
Write-Host "Press any key to exit..." -ForegroundColor Gray
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")

