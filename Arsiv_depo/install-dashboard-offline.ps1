# Offline Dashboard Installation Script for Windows Server 2025
# Bu script, node_modules.zip dosyasını kullanarak offline kurulum yapar

param(
    [string]$DashboardPath = ".\dashboard",
    [string]$NodeModulesZip = ".\node_modules.zip"
)

$ErrorActionPreference = "Stop"

Write-Host "=== Offline Dashboard Installation ===" -ForegroundColor Cyan
Write-Host ""

# Check if dashboard directory exists
if (-not (Test-Path $DashboardPath)) {
    Write-Host "ERROR: Dashboard directory not found: $DashboardPath" -ForegroundColor Red
    exit 1
}

Write-Host "Dashboard path: $DashboardPath" -ForegroundColor Green
Write-Host ""

# Check if node_modules.zip exists
if (-not (Test-Path $NodeModulesZip)) {
    Write-Host "WARNING: node_modules.zip not found: $NodeModulesZip" -ForegroundColor Yellow
    Write-Host "Please provide node_modules.zip file or run npm install on a machine with internet." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "To create node_modules.zip on a machine with internet:" -ForegroundColor Cyan
    Write-Host "  cd dashboard" -ForegroundColor White
    Write-Host "  npm ci" -ForegroundColor White
    Write-Host "  cd .." -ForegroundColor White
    Write-Host "  Compress-Archive -Path dashboard\node_modules -DestinationPath node_modules.zip" -ForegroundColor White
    Write-Host ""
    
    $continue = Read-Host "Do you want to continue with npm install? (requires internet) [y/N]"
    if ($continue -ne "y" -and $continue -ne "Y") {
        exit 1
    }
    
    # Install with npm (requires internet)
    Write-Host "Installing dependencies with npm (requires internet)..." -ForegroundColor Yellow
    Set-Location $DashboardPath
    npm install
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: npm install failed" -ForegroundColor Red
        exit 1
    }
    Set-Location ..
} else {
    # Extract node_modules.zip
    Write-Host "Extracting node_modules.zip..." -ForegroundColor Cyan
    $nodeModulesPath = Join-Path $DashboardPath "node_modules"
    
    # Remove existing node_modules if exists
    if (Test-Path $nodeModulesPath) {
        Write-Host "Removing existing node_modules..." -ForegroundColor Yellow
        Remove-Item -Path $nodeModulesPath -Recurse -Force
    }
    
    # Extract zip
    Expand-Archive -Path $NodeModulesZip -DestinationPath $DashboardPath -Force
    Write-Host "✓ node_modules extracted" -ForegroundColor Green
    Write-Host ""
}

# Check if node_modules exists now
$nodeModulesPath = Join-Path $DashboardPath "node_modules"
if (-not (Test-Path $nodeModulesPath)) {
    Write-Host "ERROR: node_modules not found after extraction" -ForegroundColor Red
    exit 1
}

# Check if react-plotly.js exists
$reactPlotlyPath = Join-Path $nodeModulesPath "react-plotly.js"
if (-not (Test-Path $reactPlotlyPath)) {
    Write-Host "ERROR: react-plotly.js not found in node_modules" -ForegroundColor Red
    exit 1
}
Write-Host "✓ react-plotly.js found" -ForegroundColor Green
Write-Host ""

# Build dashboard
Write-Host "Building dashboard..." -ForegroundColor Cyan
Set-Location $DashboardPath

# Check if .next exists
if (Test-Path ".next") {
    $rebuild = Read-Host ".next directory exists. Rebuild? [y/N]"
    if ($rebuild -eq "y" -or $rebuild -eq "Y") {
        Remove-Item -Path ".next" -Recurse -Force
    } else {
        Write-Host "Skipping build (using existing .next)" -ForegroundColor Yellow
        Set-Location ..
        Write-Host ""
        Write-Host "=== Installation Complete ===" -ForegroundColor Green
        Write-Host "To start dashboard, run:" -ForegroundColor Cyan
        Write-Host "  cd dashboard" -ForegroundColor White
        Write-Host "  npm start" -ForegroundColor White
        exit 0
    }
}

npm run build
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Build failed" -ForegroundColor Red
    exit 1
}

Set-Location ..

Write-Host ""
Write-Host "=== Installation Complete ===" -ForegroundColor Green
Write-Host ""
Write-Host "Dashboard is ready to start!" -ForegroundColor Green
Write-Host ""
Write-Host "To start dashboard, run:" -ForegroundColor Cyan
Write-Host "  cd dashboard" -ForegroundColor White
Write-Host "  npm start" -ForegroundColor White
Write-Host ""
Write-Host "Or use standalone build:" -ForegroundColor Cyan
Write-Host "  cd dashboard\.next\standalone" -ForegroundColor White
Write-Host "  node server.js" -ForegroundColor White
Write-Host ""

