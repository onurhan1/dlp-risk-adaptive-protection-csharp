# ExcelJS, jsPDF ve jspdf-autotable paketlerini ve TÜM bağımlılıklarını
# tek bir zip dosyasına paketler.
# Uzak sunucuya GitHub üzerinden göndermek için kullanılır.

param(
    [string]$OutputZip = ".\dashboard\libraries-bundle.zip"
)

$ErrorActionPreference = "Stop"

Write-Host "=== Kutuphane Bundle Olusturucu ===" -ForegroundColor Cyan
Write-Host "exceljs, jspdf, jspdf-autotable ve bagimliliklari paketleniyor..." -ForegroundColor Yellow
Write-Host ""

# Gecici klasor olustur
$tempDir = Join-Path $env:TEMP "lib-bundle-$(Get-Random)"
$tempNodeModules = Join-Path $tempDir "node_modules"

try {
    # Temp klasor olustur
    New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
    Write-Host "[1/4] Gecici klasor olusturuldu: $tempDir" -ForegroundColor Green

    # Minimal package.json olustur (sadece 3 paket)
    $packageJson = @{
        name = "lib-bundle"
        version = "1.0.0"
        private = $true
        dependencies = @{
            "exceljs" = "^4.4.0"
            "jspdf" = "^4.1.0"
            "jspdf-autotable" = "^5.0.7"
        }
    } | ConvertTo-Json -Depth 3

    $packageJsonPath = Join-Path $tempDir "package.json"
    Set-Content -Path $packageJsonPath -Value $packageJson -Encoding UTF8
    Write-Host "[2/4] package.json olusturuldu (sadece 3 paket)" -ForegroundColor Green

    # npm install ile sadece bu 3 paketin tum bagimliliklerini indir
    Write-Host "[3/4] npm install calistiriliyor (bu biraz surabilir)..." -ForegroundColor Yellow
    Push-Location $tempDir
    $npmOutput = cmd /c "npm install --omit=dev 2>&1"
    $npmExitCode = $LASTEXITCODE
    $npmOutput | ForEach-Object { Write-Host "  $_" -ForegroundColor Gray }
    if ($npmExitCode -ne 0 -and -not (Test-Path $tempNodeModules)) {
        Write-Host "HATA: npm install basarisiz oldu!" -ForegroundColor Red
        Pop-Location
        exit 1
    }
    Pop-Location
    Write-Host "  npm install tamamlandi!" -ForegroundColor Green

    # Mevcut zip varsa sil
    if (Test-Path $OutputZip) {
        Remove-Item -Path $OutputZip -Force
        Write-Host "  Eski zip silindi." -ForegroundColor Yellow
    }

    # node_modules klasorunu ziple
    Write-Host "[4/4] node_modules zipleniyor..." -ForegroundColor Yellow
    Compress-Archive -Path $tempNodeModules -DestinationPath $OutputZip -CompressionLevel Optimal

    if (Test-Path $OutputZip) {
        $zipSize = [math]::Round((Get-Item $OutputZip).Length / 1MB, 2)
        Write-Host ""
        Write-Host "BASARILI! libraries-bundle.zip olusturuldu!" -ForegroundColor Green
        Write-Host "  Boyut: $zipSize MB" -ForegroundColor Cyan
        Write-Host "  Konum: $(Resolve-Path $OutputZip)" -ForegroundColor Cyan
        
        # Icindeki paketleri listele
        $packages = Get-ChildItem -Path $tempNodeModules -Directory | Where-Object { $_.Name -notlike ".*" }
        Write-Host "  Toplam paket sayisi: $($packages.Count)" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "Icerik:" -ForegroundColor Yellow
        foreach ($pkg in $packages) {
            Write-Host "    - $($pkg.Name)" -ForegroundColor White
        }
        
        Write-Host ""
        Write-Host "--- Sonraki Adimlar ---" -ForegroundColor Yellow
        Write-Host "1. git add dashboard/libraries-bundle.zip" -ForegroundColor White
        Write-Host "2. git commit -m 'Add exceljs, jspdf, jspdf-autotable bundle'" -ForegroundColor White
        Write-Host "3. git push origin main" -ForegroundColor White
        Write-Host "4. Uzak sunucuda: git pull" -ForegroundColor White
        Write-Host "5. Uzak sunucuda: Expand-Archive -Path dashboard/libraries-bundle.zip -DestinationPath dashboard/ -Force" -ForegroundColor White
        Write-Host "   (Bu, node_modules klasorune paketleri ekler)" -ForegroundColor White
    } else {
        Write-Host "HATA: Zip olusturulamadi!" -ForegroundColor Red
        exit 1
    }
}
finally {
    # Gecici klasoru temizle
    if (Test-Path $tempDir) {
        Remove-Item -Path $tempDir -Recurse -Force -ErrorAction SilentlyContinue
    }
    Write-Host ""
    Write-Host "Gecici dosyalar temizlendi." -ForegroundColor Gray
}
