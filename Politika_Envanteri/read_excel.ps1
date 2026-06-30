$excel = New-Object -ComObject Excel.Application
$excel.Visible = $false
$wb = $excel.Workbooks.Open("c:\Users\abdul\Desktop\dlp-risk-adaptive-protection-csharp-main\Politika_Envanteri\maskeli_politika_envanteri.xlsx")

foreach($ws in $wb.Worksheets) {
    Write-Host "=== SHEET: $($ws.Name) ==="
    $usedRange = $ws.UsedRange
    $rows = $usedRange.Rows.Count
    $cols = $usedRange.Columns.Count
    Write-Host "Rows: $rows Cols: $cols"
    
    Write-Host "--- HEADERS (Row 1) ---"
    for($c = 1; $c -le $cols; $c++) {
        $val = $ws.Cells.Item(1, $c).Text
        if($val -ne '') {
            Write-Host "  Col $c : $val"
        }
    }
    
    Write-Host "--- SAMPLE DATA (Rows 2-4) ---"
    for($r = 2; $r -le [Math]::Min(4, $rows); $r++) {
        Write-Host "  Row $r :"
        for($c = 1; $c -le [Math]::Min($cols, 30); $c++) {
            $val = $ws.Cells.Item($r, $c).Text
            if($val -ne '') {
                $header = $ws.Cells.Item(1, $c).Text
                Write-Host "    $header : $val"
            }
        }
    }
}

$wb.Close($false)
$excel.Quit()
[System.Runtime.Interopservices.Marshal]::ReleaseComObject($excel) | Out-Null
