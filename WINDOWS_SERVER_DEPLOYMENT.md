# Windows Server Deployment Rehberi

## Zip Dosyası Hazırlama

### Mac'te Zip Oluşturma

```bash
./create-project-zip.sh
```

Bu script:
- ✅ Mac kalıntılarını temizler (.DS_Store, ._*, .AppleDouble)
- ✅ Build klasörlerini hariç tutar (bin/, obj/, node_modules/, .next/)
- ✅ Log dosyalarını hariç tutar
- ✅ IDE dosyalarını hariç tutar
- ✅ Sadece kaynak kodları ve gerekli dosyaları içerir

### Zip İçeriği

Zip dosyası şunları içerir:
- ✅ Tüm C# projeleri (Analyzer, Collector, Dashboard, Shared)
- ✅ Dashboard kaynak kodları
- ✅ Tüm konfigürasyon dosyaları
- ✅ Kurulum script'leri
- ✅ Dokümantasyon

Zip dosyası şunları içermez:
- ❌ `node_modules/` (ayrı zip gerekli - `create-node-modules-zip.ps1` kullanın)
- ❌ `.next/` build klasörü
- ❌ `bin/`, `obj/` build klasörleri
- ❌ Mac kalıntıları (.DS_Store, ._*, vb.)
- ❌ Log dosyaları

## Windows Server'da Kurulum

### 1. Zip Dosyasını Aktarın

Zip dosyasını Windows Server'a kopyalayın (USB, network share, vb.)

### 2. Zip'i Açın

```powershell
# Örnek: C:\DLP_RiskAnalyzer klasörüne açın
Expand-Archive -Path "DLP_RiskAnalyzer_*.zip" -DestinationPath "C:\DLP_RiskAnalyzer" -Force
```

### 3. Dashboard node_modules Kurulumu

Dashboard için `node_modules` gerekiyor. İki seçenek:

#### Seçenek A: Offline Kurulum (Önerilen)

1. **İnternet bağlantısı olan bir makinede** `node_modules.zip` oluşturun:
   ```powershell
   .\create-node-modules-zip.ps1
   ```

2. `node_modules.zip` dosyasını Windows Server'a kopyalayın

3. **Windows Server'da** kurulum yapın:
   ```powershell
   cd C:\DLP_RiskAnalyzer
   .\install-dashboard-offline.ps1
   ```

#### Seçenek B: Online Kurulum

Eğer sunucuda geçici internet bağlantısı varsa:

```powershell
cd C:\DLP_RiskAnalyzer\dashboard
npm install
npm run build
```

### 4. .NET SDK Kurulumu

```powershell
# .NET 8 SDK kurulu olmalı
dotnet --version

# Eğer kurulu değilse, install-dotnet.sh script'ini Windows'a uyarlayın
# veya https://dotnet.microsoft.com/download adresinden indirin
```

### 5. Projeleri Build Edin

```powershell
cd C:\DLP_RiskAnalyzer

# Analyzer API
cd DLP.RiskAnalyzer.Analyzer
dotnet build
dotnet publish -c Release -o ./publish

# Collector Service
cd ..\DLP.RiskAnalyzer.Collector
dotnet build
dotnet publish -c Release -o ./publish
```

### 6. Servisleri Başlatın

Detaylı kurulum için `WINDOWS_SERVER_2025_KURULUM_REHBERI.md` dosyasına bakın.

## Kontrol Listesi

- [ ] Zip dosyası Windows Server'a kopyalandı
- [ ] Zip dosyası açıldı
- [ ] `node_modules.zip` hazırlandı ve kopyalandı (veya `npm install` çalıştırıldı)
- [ ] Dashboard `node_modules` kuruldu
- [ ] Dashboard build edildi (`npm run build`)
- [ ] .NET SDK kurulu
- [ ] C# projeleri build edildi
- [ ] Servisler yapılandırıldı
- [ ] Servisler başlatıldı

## Sorun Giderme

### "Module not found" Hataları

Dashboard'da `axios`, `date-fns`, `plotly.js` bulunamıyorsa:

```powershell
cd C:\DLP_RiskAnalyzer\dashboard
Test-Path node_modules

# Eğer False dönerse:
npm install
```

Detaylı bilgi için `DASHBOARD_NODE_MODULES_FIX.md` dosyasına bakın.

### Mac Kalıntıları

Eğer zip'te Mac kalıntıları varsa:

```powershell
# Windows Server'da temizleyin
Get-ChildItem -Path . -Recurse -Force | Where-Object { $_.Name -like "._*" -or $_.Name -eq ".DS_Store" } | Remove-Item -Force
```

## Notlar

- Zip dosyası yaklaşık 200-300 MB olabilir (node_modules hariç)
- `node_modules.zip` ayrı olarak hazırlanmalı (yaklaşık 500-800 MB)
- Windows Server'da path'te özel karakterler veya boşluklar sorun çıkarabilir
- Tüm servisler aynı sunucuda çalışacak şekilde yapılandırılmıştır

