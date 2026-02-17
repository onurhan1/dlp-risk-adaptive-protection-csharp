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
-- Drop and recreate merceks table
DROP TABLE IF EXISTS merceks;

CREATE TABLE merceks (
    incidentid INTEGER PRIMARY KEY,
    statusid VARCHAR(50),
    flowstatusid VARCHAR(50),
    assignmentgroupid INTEGER,
    summarydescription VARCHAR(500),
    incidentdescription TEXT,
    impactid VARCHAR(50),
    priorityid VARCHAR(50),
    categoryid INTEGER,
    assignedusercode VARCHAR(100),
    opendate TIMESTAMP,
    closedate TIMESTAMP,
    startdate TIMESTAMP,
    solutiondescription TEXT,
    requesttypeid VARCHAR(50),
    calltypeid VARCHAR(50),
    solutionmethod VARCHAR(200),
    username VARCHAR(200),
    systemdate TIMESTAMP,
    definitioncategoryid INTEGER,
    definitioncategorypath VARCHAR(500)
);

-- Create indexes
CREATE INDEX idx_mercek_opendate ON merceks(opendate);
CREATE INDEX idx_mercek_closedate ON merceks(closedate);
CREATE INDEX idx_mercek_systemdate ON merceks(systemdate);
CREATE INDEX idx_mercek_username ON merceks(username);
CREATE INDEX idx_mercek_assignedusercode ON merceks(assignedusercode);
CREATE INDEX idx_mercek_statusid ON merceks(statusid);

-- Import CSV data
COPY merceks (
    incidentid, statusid, flowstatusid, assignmentgroupid,
    summarydescription, incidentdescription, impactid, priorityid,
    categoryid, assignedusercode, opendate, closedate, startdate,
    solutiondescription, requesttypeid, calltypeid, solutionmethod,
    username, systemdate, definitioncategoryid, definitioncategorypath
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
        $countQuery = "SELECT COUNT(*) FROM merceks;"
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