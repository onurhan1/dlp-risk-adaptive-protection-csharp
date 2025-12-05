# Dashboard node_modules Sorunu Çözüm Rehberi

## Sorun
Dashboard çalıştırıldığında şu hatalar alınıyor:
- `Module not found: Can't resolve 'axios'`
- `Module not found: Can't resolve 'date-fns'`
- `Module not found: Can't resolve 'plotly.js'`
- `Module not found: Can't resolve 'react-plotly.js'`
- Tailwind CSS PostCSS plugin hatası

## Sebep
`node_modules` klasörü eksik veya yanlış konumda. Ayrıca path'te klasör adı iki kez tekrarlanmış olabilir.

## Çözüm

### 1. Doğru Dizin Yapısını Kontrol Edin

Path'iniz şu şekilde olmamalı:
```
C:\...\dlp-risk-adaptive-protection-csharp-main\dlp-risk-adaptive-protection-csharp-main\dashboard\
```

Doğru path şu şekilde olmalı:
```
C:\DLP_RiskAnalyzer\dashboard\
```

Eğer path'te klasör adı iki kez tekrarlanmışsa:
1. İç klasörü dışarı çıkarın
2. Veya doğru klasöre gidin

### 2. node_modules Klasörünü Kontrol Edin

```powershell
cd dashboard
Test-Path node_modules
```

Eğer `False` dönerse, `node_modules` yok demektir.

### 3. Paketleri Yükleyin

#### Yöntem A: İnternet Bağlantısı Varsa

```powershell
cd dashboard
npm install
```

#### Yöntem B: Offline (node_modules.zip Kullanarak)

```powershell
# node_modules.zip'i dashboard klasörüne kopyalayın
cd dashboard
Expand-Archive -Path ..\node_modules.zip -DestinationPath . -Force
```

### 4. node_modules'in Doğru Yerde Olduğunu Doğrulayın

```powershell
cd dashboard
Test-Path node_modules\axios
Test-Path node_modules\date-fns
Test-Path node_modules\react-plotly.js
Test-Path node_modules\plotly.js
```

Hepsi `True` dönmeli.

### 5. Next.js Cache'i Temizleyin

```powershell
cd dashboard
Remove-Item -Path .next -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -Path node_modules\.cache -Recurse -Force -ErrorAction SilentlyContinue
```

### 6. Yeniden Build Edin

```powershell
cd dashboard
npm run build
```

### 7. Dashboard'u Başlatın

```powershell
npm start
```

## Hızlı Çözüm Script'i

Aşağıdaki PowerShell script'ini çalıştırın:

```powershell
# Dashboard dizinine gidin
$dashboardPath = "C:\DLP_RiskAnalyzer\dashboard"  # Kendi path'inizi yazın
cd $dashboardPath

# node_modules kontrolü
if (-not (Test-Path "node_modules")) {
    Write-Host "node_modules bulunamadı. Yükleniyor..." -ForegroundColor Yellow
    npm install
}

# Cache temizleme
Write-Host "Cache temizleniyor..." -ForegroundColor Cyan
Remove-Item -Path ".next" -Recurse -Force -ErrorAction SilentlyContinue

# Build
Write-Host "Build ediliyor..." -ForegroundColor Cyan
npm run build

# Başlat
Write-Host "Dashboard başlatılıyor..." -ForegroundColor Green
npm start
```

## Path Sorunu Düzeltme

Eğer path'te klasör adı iki kez tekrarlanmışsa:

```powershell
# Yanlış path
C:\Users\...\dlp-risk-adaptive-protection-csharp-main\dlp-risk-adaptive-protection-csharp-main\dashboard\

# Doğru path (iç klasörü dışarı çıkarın)
C:\Users\...\dlp-risk-adaptive-protection-csharp-main\dashboard\
```

Veya projeyi doğru bir yere taşıyın:

```powershell
# Tüm projeyi doğru yere kopyalayın
Copy-Item -Path "C:\Users\...\dlp-risk-adaptive-protection-csharp-main\dlp-risk-adaptive-protection-csharp-main\*" -Destination "C:\DLP_RiskAnalyzer\" -Recurse -Force
```

## Kontrol Listesi

- [ ] Path'te klasör adı iki kez tekrarlanmamış
- [ ] `dashboard` klasöründe `package.json` var
- [ ] `dashboard` klasöründe `node_modules` var
- [ ] `node_modules\axios` var
- [ ] `node_modules\date-fns` var
- [ ] `node_modules\react-plotly.js` var
- [ ] `node_modules\plotly.js` var
- [ ] `.next` klasörü temizlendi
- [ ] `npm run build` başarılı
- [ ] `npm start` çalışıyor

## Notlar

- `node_modules` klasörü git'te olmamalı (`.gitignore`'da)
- `package-lock.json` git'te olmalı
- Build öncesi cache temizlemek önemli
- Path'te özel karakterler veya boşluklar sorun çıkarabilir

