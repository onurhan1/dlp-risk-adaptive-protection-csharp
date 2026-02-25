# zip-new-packages.ps1
# Sadece yeni eklenen npm paketlerini ve bagimliklarini zipeler
# Cikti: Desktop'a "new-node-packages.zip" olarak kaydeder

$dashboardDir = "$PSScriptRoot\dashboard"
$nodeModules = "$dashboardDir\node_modules"
$outputZip = "$env:USERPROFILE\Desktop\new-node-packages.zip"
$tempDir = "$env:TEMP\new-node-packages"

# Yeni eklenen paketler
$newPackages = @("exceljs", "jspdf", "jspdf-autotable")

Write-Host "=== Yeni NPM Paketleri Zipper ===" -ForegroundColor Cyan
Write-Host ""

# Temizlik
if (Test-Path $tempDir) { Remove-Item $tempDir -Recurse -Force }
if (Test-Path $outputZip) { Remove-Item $outputZip -Force }
New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
New-Item -ItemType Directory -Path "$tempDir\node_modules" -Force | Out-Null

# Her paketin bagimlilik agacini bul
$allDeps = @{}

foreach ($pkg in $newPackages) {
    Write-Host "Paket analiz ediliyor: $pkg" -ForegroundColor Yellow
    
    try {
        $json = & npm ls $pkg --all --json 2>$null | ConvertFrom-Json
        
        # Recursive olarak tum bagimliliklari topla
        function Get-AllDeps($node) {
            if ($node.dependencies) {
                foreach ($dep in $node.dependencies.PSObject.Properties) {
                    $depName = $dep.Name
                    if (-not $allDeps.ContainsKey($depName)) {
                        $allDeps[$depName] = $true
                        Get-AllDeps $dep.Value
                    }
                }
            }
        }
        Get-AllDeps $json
    } catch {
        Write-Host "  npm ls basarisiz, dogrudan klasor taranacak" -ForegroundColor DarkYellow
    }
    
    # Paketi kendisini de ekle
    $allDeps[$pkg] = $true
}

# Eger npm ls calismazsa, package-lock.json'dan bul
if ($allDeps.Count -le $newPackages.Count) {
    Write-Host ""
    Write-Host "package-lock.json'dan bagimliliklar okunuyor..." -ForegroundColor Yellow
    
    $lockFile = Get-Content "$dashboardDir\package-lock.json" -Raw | ConvertFrom-Json
    
    function Get-LockDeps($pkgName, $visited) {
        if ($visited.ContainsKey($pkgName)) { return }
        $visited[$pkgName] = $true
        
        $lockPkg = $lockFile.packages."node_modules/$pkgName"
        if (-not $lockPkg) { return }
        
        if ($lockPkg.dependencies) {
            foreach ($dep in $lockPkg.dependencies.PSObject.Properties) {
                Get-LockDeps $dep.Name $visited
            }
        }
        if ($lockPkg.optionalDependencies) {
            foreach ($dep in $lockPkg.optionalDependencies.PSObject.Properties) {
                if (Test-Path "$nodeModules\$($dep.Name)") {
                    Get-LockDeps $dep.Name $visited
                }
            }
        }
    }
    
    foreach ($pkg in $newPackages) {
        Get-LockDeps $pkg $allDeps
    }
}

Write-Host ""
Write-Host "Toplam $($allDeps.Count) paket bulundu" -ForegroundColor Green
Write-Host ""

# Paketleri temp klasore kopyala
$copiedCount = 0
$totalSize = 0

foreach ($dep in $allDeps.Keys | Sort-Object) {
    $srcPath = "$nodeModules\$dep"
    $destPath = "$tempDir\node_modules\$dep"
    
    if (Test-Path $srcPath) {
        # Scoped paketler icin ust klasoru olustur (@types/xxx gibi)
        $parentDir = Split-Path $destPath -Parent
        if (-not (Test-Path $parentDir)) {
            New-Item -ItemType Directory -Path $parentDir -Force | Out-Null
        }
        
        Copy-Item $srcPath $destPath -Recurse -Force
        $size = (Get-ChildItem $srcPath -Recurse -File | Measure-Object -Property Length -Sum).Sum
        $sizeMB = [math]::Round($size / 1MB, 2)
        Write-Host "  + $dep ($sizeMB MB)" -ForegroundColor DarkGray
        $copiedCount++
        $totalSize += $size
    } else {
        Write-Host "  ! $dep bulunamadi (opsiyonel olabilir)" -ForegroundColor DarkYellow
    }
}

Write-Host ""
Write-Host "Kopyalanan: $copiedCount paket, Toplam: $([math]::Round($totalSize / 1MB, 2)) MB" -ForegroundColor Cyan

# Zip olustur
Write-Host ""
Write-Host "ZIP olusturuluyor: $outputZip" -ForegroundColor Yellow

Compress-Archive -Path "$tempDir\node_modules" -DestinationPath $outputZip -Force

$zipSize = [math]::Round((Get-Item $outputZip).Length / 1MB, 2)

# Temizlik
Remove-Item $tempDir -Recurse -Force

Write-Host ""
Write-Host "=== TAMAMLANDI ===" -ForegroundColor Green
Write-Host "ZIP dosyasi: $outputZip" -ForegroundColor Green
Write-Host "ZIP boyutu: $zipSize MB" -ForegroundColor Green
Write-Host ""
Write-Host "Sunucuda kullanim:" -ForegroundColor Cyan
Write-Host "  1. ZIP'i sunucuya kopyala" -ForegroundColor White
Write-Host "  2. dashboard/ klasorune gir" -ForegroundColor White
Write-Host "  3. Expand-Archive -Path new-node-packages.zip -DestinationPath . -Force" -ForegroundColor White
Write-Host ""
