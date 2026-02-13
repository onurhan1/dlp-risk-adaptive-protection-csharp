<#
.SYNOPSIS
    Imports merceks.csv data into the PostgreSQL database.

.DESCRIPTION
    This script reads the merceks.csv file and imports all incident records
    into the mercek_incidents table using direct PostgreSQL COPY command.

.PARAMETER CsvPath
    Path to the merceks.csv file (default: database/merceks.csv)

.PARAMETER ConnectionString
    PostgreSQL connection string (reads from appsettings.json if not provided)

.EXAMPLE
    .\import-mercek-data.ps1
    .\import-mercek-data.ps1 -CsvPath "C:\data\merceks.csv"
#>

param(
    [string]$CsvPath = "database/merceks.csv",
    [string]$ConnectionString = $null
)

# Colors for output
$ErrorColor = "Red"
$SuccessColor = "Green"
$InfoColor = "Cyan"
$WarningColor = "Yellow"

Write-Host "`n=== Mercek CSV Import ===" -ForegroundColor $InfoColor

# Check if CSV file exists
if (-not (Test-Path $CsvPath)) {
    Write-Host "ERROR: CSV file not found at: $CsvPath" -ForegroundColor $ErrorColor
    exit 1
}

$csvFullPath = (Resolve-Path $CsvPath).Path
Write-Host "CSV File: $csvFullPath" -ForegroundColor $InfoColor

# Get connection string from appsettings.json if not provided
if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
    $appsettingsPath = "DLP.RiskAnalyzer.Analyzer/appsettings.json"
    
    if (Test-Path $appsettingsPath) {
        $appsettings = Get-Content $appsettingsPath | ConvertFrom-Json
        $ConnectionString = $appsettings.ConnectionStrings.DefaultConnection
        Write-Host "Connection string loaded from appsettings.json" -ForegroundColor $InfoColor
    }
    else {
        Write-Host "ERROR: appsettings.json not found and no connection string provided" -ForegroundColor $ErrorColor
        exit 1
    }
}

# Parse connection string
$connParams = @{}
$ConnectionString -split ';' | ForEach-Object {
    if ($_ -match '(.+?)=(.+)') {
        $connParams[$matches[1].Trim()] = $matches[2].Trim()
    }
}

$dbHost = $connParams['Host']
$dbPort = $connParams['Port']
$dbName = $connParams['Database']
$dbUser = $connParams['Username']
$dbPassword = $connParams['Password']

Write-Host "Database: $dbName @ $dbHost`:$dbPort" -ForegroundColor $InfoColor

# Set PGPASSWORD environment variable
$env:PGPASSWORD = $dbPassword

# Create temp SQL file for import
$tempSqlFile = [System.IO.Path]::GetTempFileName() + ".sql"

$importSql = @"
-- Drop and recreate mercek_incidents table
DROP TABLE IF EXISTS mercek_incidents;

CREATE TABLE mercek_incidents (
    incident_id INTEGER PRIMARY KEY,
    status_id VARCHAR(50),
    flow_status_id VARCHAR(50),
    assignment_group_id INTEGER,
    summary_description VARCHAR(500),
    incident_description TEXT,
    impact_id VARCHAR(50),
    priority_id VARCHAR(50),
    category_id INTEGER,
    assigned_user_code VARCHAR(100),
    open_date TIMESTAMP,
    close_date TIMESTAMP,
    start_date TIMESTAMP,
    solution_description TEXT,
    request_type_id VARCHAR(50),
    call_type_id VARCHAR(50),
    solution_method VARCHAR(200),
    user_name VARCHAR(200),
    definition_category_id INTEGER,
    definition_category_path VARCHAR(500)
);

-- Create indexes
CREATE INDEX idx_mercek_open_date ON mercek_incidents(open_date);
CREATE INDEX idx_mercek_close_date ON mercek_incidents(close_date);
CREATE INDEX idx_mercek_user_name ON mercek_incidents(user_name);
CREATE INDEX idx_mercek_assigned_user ON mercek_incidents(assigned_user_code);
CREATE INDEX idx_mercek_status ON mercek_incidents(status_id);

-- Import CSV data
COPY mercek_incidents (
    incident_id, status_id, flow_status_id, assignment_group_id,
    summary_description, incident_description, impact_id, priority_id,
    category_id, assigned_user_code, open_date, close_date, start_date,
    solution_description, request_type_id, call_type_id, solution_method,
    user_name, definition_category_id, definition_category_path
)
FROM '$($csvFullPath -replace '\\','/')'
WITH (FORMAT csv, HEADER true, DELIMITER ',', NULL '', QUOTE '"');
"@

$importSql | Set-Content -Path $tempSqlFile -Encoding UTF8

Write-Host "`nExecuting import..." -ForegroundColor $InfoColor

try {
    # Execute SQL with psql
    $output = & psql -h $dbHost -p $dbPort -U $dbUser -d $dbName -f $tempSqlFile 2>&1
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "`n✓ Import completed successfully!" -ForegroundColor $SuccessColor
        
        # Get record count
        $countQuery = "SELECT COUNT(*) FROM mercek_incidents;"
        $count = & psql -h $dbHost -p $dbPort -U $dbUser -d $dbName -t -c $countQuery
        
        Write-Host "Total records imported: $($count.Trim())" -ForegroundColor $SuccessColor
    }
    else {
        Write-Host "`n✗ Import failed!" -ForegroundColor $ErrorColor
        Write-Host $output -ForegroundColor $ErrorColor
        exit 1
    }
}
catch {
    Write-Host "`n✗ Import failed: $_" -ForegroundColor $ErrorColor
    exit 1
}
finally {
    # Clean up
    if (Test-Path $tempSqlFile) {
        Remove-Item $tempSqlFile -Force
    }
    
    # Clear password from environment
    $env:PGPASSWORD = $null
}

Write-Host "`n=== Import Complete ===" -ForegroundColor $SuccessColor
"@