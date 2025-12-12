# Create node_modules.zip for offline installation
# Run this script on a machine WITH internet connection

param(
    [string]$DashboardPath = ".\dashboard",
    [string]$OutputZip = ".\node_modules.zip"
)

$ErrorActionPreference = "Stop"

Write-Host "=== Creating node_modules.zip for Offline Installation ===" -ForegroundColor Cyan
Write-Host ""

# Check if dashboard directory exists
if (-not (Test-Path $DashboardPath)) {
    Write-Host "ERROR: Dashboard directory not found: $DashboardPath" -ForegroundColor Red
    exit 1
}

# Check if package.json exists
$packageJsonPath = Join-Path $DashboardPath "package.json"
if (-not (Test-Path $packageJsonPath)) {
    Write-Host "ERROR: package.json not found in dashboard directory" -ForegroundColor Red
    exit 1
}

# Check if node_modules exists
$nodeModulesPath = Join-Path $DashboardPath "node_modules"
if (-not (Test-Path $nodeModulesPath)) {
    Write-Host "node_modules not found. Installing dependencies..." -ForegroundColor Yellow
    Set-Location $DashboardPath
    npm ci
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: npm ci failed" -ForegroundColor Red
        exit 1
    }
    Set-Location ..
}

# Check if react-plotly.js exists
$reactPlotlyPath = Join-Path $nodeModulesPath "react-plotly.js"
if (-not (Test-Path $reactPlotlyPath)) {
    Write-Host "ERROR: react-plotly.js not found. Please run 'npm ci' first." -ForegroundColor Red
    exit 1
}

Write-Host "Found node_modules directory" -ForegroundColor Green
Write-Host ""

# Remove existing zip if exists
if (Test-Path $OutputZip) {
    Write-Host "Removing existing $OutputZip..." -ForegroundColor Yellow
    Remove-Item -Path $OutputZip -Force
}

# Create zip
Write-Host "Creating $OutputZip..." -ForegroundColor Cyan
Write-Host "This may take a few minutes..." -ForegroundColor Yellow

$zipPath = Resolve-Path $OutputZip
$nodeModulesFullPath = Resolve-Path $nodeModulesPath

Compress-Archive -Path $nodeModulesFullPath -DestinationPath $zipPath -CompressionLevel Optimal

if (Test-Path $OutputZip) {
    $zipSize = (Get-Item $OutputZip).Length / 1MB
    Write-Host ""
    Write-Host "✓ node_modules.zip created successfully!" -ForegroundColor Green
    Write-Host "  Size: $([math]::Round($zipSize, 2)) MB" -ForegroundColor Cyan
    Write-Host "  Location: $zipPath" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Yellow
    Write-Host "  1. Copy node_modules.zip to the offline server" -ForegroundColor White
    Write-Host "  2. Run install-dashboard-offline.ps1 on the server" -ForegroundColor White
} else {
    Write-Host "ERROR: Failed to create node_modules.zip" -ForegroundColor Red
    exit 1
}

