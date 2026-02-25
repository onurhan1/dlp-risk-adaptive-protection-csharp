# Service Status Check Script
# Checks the status of all required services for DLP Risk Analyzer

Write-Host "=== DLP Risk Analyzer - Service Status Check ===" -ForegroundColor Green
Write-Host ""

# PostgreSQL Check
Write-Host "[PostgreSQL]" -ForegroundColor Yellow
$pgServices = Get-Service -Name postgresql* -ErrorAction SilentlyContinue
if ($pgServices) {
    foreach ($service in $pgServices) {
        $statusColor = if ($service.Status -eq 'Running') { 'Green' } else { 'Red' }
        Write-Host "  Service: $($service.DisplayName)" -ForegroundColor $statusColor
        Write-Host "  Status:  $($service.Status)" -ForegroundColor $statusColor
    }
} else {
    Write-Host "  Service: Not found (check if using Docker container)" -ForegroundColor Yellow
    
    # Test connection to port 5432
    $pgPortTest = Test-NetConnection -ComputerName localhost -Port 5432 -WarningAction SilentlyContinue
    if ($pgPortTest.TcpTestSucceeded) {
        Write-Host "  Port 5432: Open (PostgreSQL may be running in Docker)" -ForegroundColor Green
    } else {
        Write-Host "  Port 5432: Closed (PostgreSQL not accessible)" -ForegroundColor Red
    }
}

Write-Host ""

# Redis Check
Write-Host "[Redis]" -ForegroundColor Yellow
$redisServices = Get-Service -Name Memurai* -ErrorAction SilentlyContinue
if ($redisServices) {
    foreach ($service in $redisServices) {
        $statusColor = if ($service.Status -eq 'Running') { 'Green' } else { 'Red' }
        Write-Host "  Service: $($service.DisplayName)" -ForegroundColor $statusColor
        Write-Host "  Status:  $($service.Status)" -ForegroundColor $statusColor
    }
} else {
    Write-Host "  Service: Not found (check Docker/WSL2)" -ForegroundColor Yellow
    
    # Test connection to port 6379
    $redisPortTest = Test-NetConnection -ComputerName localhost -Port 6379 -WarningAction SilentlyContinue
    if ($redisPortTest.TcpTestSucceeded) {
        Write-Host "  Port 6379: Open (Redis may be running in Docker/WSL2)" -ForegroundColor Green
    } else {
        Write-Host "  Port 6379: Closed (Redis not accessible)" -ForegroundColor Red
    }
}

# Docker Containers Check
Write-Host ""
Write-Host "[Docker Containers]" -ForegroundColor Yellow
$dockerExists = Get-Command docker -ErrorAction SilentlyContinue
if ($dockerExists) {
    $timescaleContainer = docker ps -a --filter "name=timescaledb" --format "{{.Names}}" 2>$null
    $redisContainer = docker ps -a --filter "name=redis" --format "{{.Names}}" 2>$null
    
    if ($timescaleContainer) {
        $timescaleStatus = docker ps --filter "name=timescaledb" --format "{{.Status}}" 2>$null
        Write-Host "  TimescaleDB Container: $timescaleContainer - $timescaleStatus" -ForegroundColor $(if($timescaleStatus -match "Up"){'Green'}else{'Red'})
    } else {
        Write-Host "  TimescaleDB Container: Not found" -ForegroundColor Yellow
    }
    
    if ($redisContainer) {
        $redisStatus = docker ps --filter "name=redis" --format "{{.Status}}" 2>$null
        Write-Host "  Redis Container: $redisContainer - $redisStatus" -ForegroundColor $(if($redisStatus -match "Up"){'Green'}else{'Red'})
    } else {
        Write-Host "  Redis Container: Not found" -ForegroundColor Yellow
    }
} else {
    Write-Host "  Docker: Not installed or not in PATH" -ForegroundColor Yellow
}

# Analyzer API Check
Write-Host ""
Write-Host "[Analyzer API]" -ForegroundColor Yellow
try {
    $response = Invoke-WebRequest -Uri "http://localhost:8000/health" -Method GET -TimeoutSec 2 -ErrorAction Stop
    if ($response.StatusCode -eq 200) {
        Write-Host "  Status: Running (Healthy)" -ForegroundColor Green
        $healthData = $response.Content | ConvertFrom-Json
        Write-Host "  Timestamp: $($healthData.timestamp)" -ForegroundColor Gray
    }
} catch {
    Write-Host "  Status: Not responding" -ForegroundColor Red
    Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor Gray
}

# Port Status
Write-Host ""
Write-Host "[Port Status]" -ForegroundColor Yellow
$ports = @(8000, 5432, 6379)
foreach ($port in $ports) {
    $portTest = Test-NetConnection -ComputerName localhost -Port $port -WarningAction SilentlyContinue
    if ($portTest.TcpTestSucceeded) {
        Write-Host "  Port $port : Open" -ForegroundColor Green
    } else {
        Write-Host "  Port $port : Closed" -ForegroundColor Red
    }
}

# .NET Runtime Check
Write-Host ""
Write-Host "[.NET Runtime]" -ForegroundColor Yellow
$dotnetVersion = dotnet --version 2>$null
if ($LASTEXITCODE -eq 0) {
    Write-Host "  .NET SDK Version: $dotnetVersion" -ForegroundColor Green
    
    $desktopRuntime = dotnet --list-runtimes | Select-String "Microsoft.WindowsDesktop.App"
    if ($desktopRuntime) {
        Write-Host "  Desktop Runtime: Installed" -ForegroundColor Green
    } else {
        Write-Host "  Desktop Runtime: Not found (required for WPF Dashboard)" -ForegroundColor Yellow
    }
} else {
    Write-Host "  .NET SDK: Not found" -ForegroundColor Red
}

Write-Host ""
Write-Host "=== Check Complete ===" -ForegroundColor Green

