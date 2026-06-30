$excel = New-Object -ComObject Excel.Application
$excel.Visible = $false
$wb = $excel.Workbooks.Open("c:\Users\abdul\Desktop\dlp-risk-adaptive-protection-csharp-main\Politika_Envanteri\maskeli_politika_envanteri.xlsx")
$ws = $wb.Worksheets.Item(1)
$rows = $ws.UsedRange.Rows.Count

# Type column distribution
$typeMap = @{}
for($r = 2; $r -le $rows; $r++) {
    $typeVal = $ws.Cells.Item($r, 1).Text
    if($typeVal -ne '') {
        if(-not $typeMap.ContainsKey($typeVal)) {
            $typeMap[$typeVal] = 0
        }
        $typeMap[$typeVal]++
    }
}

Write-Host "=== TYPE COLUMN DISTRIBUTION ==="
foreach($key in $typeMap.Keys | Sort-Object) {
    Write-Host "  $key : $($typeMap[$key]) rows"
}

# For each type, show which columns have data
Write-Host ""
Write-Host "=== COLUMNS WITH DATA PER TYPE ==="
foreach($typeName in $typeMap.Keys | Sort-Object) {
    Write-Host ""
    Write-Host "--- TYPE: $typeName ---"
    # Find first 3 rows of this type
    $sampleRows = @()
    for($r = 2; $r -le $rows; $r++) {
        if($ws.Cells.Item($r, 1).Text -eq $typeName) {
            $sampleRows += $r
            if($sampleRows.Count -ge 3) { break }
        }
    }
    
    # Show which columns have data for first sample row
    $firstRow = $sampleRows[0]
    Write-Host "  Sample Row $firstRow :"
    for($c = 1; $c -le 53; $c++) {
        $val = $ws.Cells.Item($firstRow, $c).Text
        if($val -ne '') {
            $header = $ws.Cells.Item(1, $c).Text
            Write-Host "    Col $c ($header): $val"
        }
    }
    
    # Show second sample if exists
    if($sampleRows.Count -ge 2) {
        $secondRow = $sampleRows[1]
        Write-Host "  Sample Row $secondRow :"
        for($c = 1; $c -le 53; $c++) {
            $val = $ws.Cells.Item($secondRow, $c).Text
            if($val -ne '') {
                $header = $ws.Cells.Item(1, $c).Text
                Write-Host "    Col $c ($header): $val"
            }
        }
    }
}

$wb.Close($false)
$excel.Quit()
[System.Runtime.Interopservices.Marshal]::ReleaseComObject($excel) | Out-Null
