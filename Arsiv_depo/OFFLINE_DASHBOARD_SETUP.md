# Offline Dashboard Kurulum Rehberi

## Sorun
Dashboard offline sunucuda çalıştırıldığında `react-plotly.js` modülü bulunamıyor hatası alınıyor.

## Sebep
Sunucuda `node_modules` klasörü yok. `package-lock.json` git'te var ama paketler yüklenmemiş.

## Çözüm: Offline Paket Kurulumu

### Yöntem 1: İnternet Bağlantısı Olan Bir Makinede Hazırlama (Önerilen)

1. **İnternet bağlantısı olan bir makinede** (development makinesi) paketleri hazırlayın:

```bash
cd dashboard
npm ci  # package-lock.json'a göre tam kurulum
```

2. **`node_modules` klasörünü sıkıştırın:**

```bash
# Windows'ta
cd ..
tar -czf node_modules.tar.gz dashboard/node_modules

# VEYA PowerShell ile
Compress-Archive -Path dashboard\node_modules -DestinationPath node_modules.zip
```

3. **Sıkıştırılmış dosyayı sunucuya aktarın** (USB, network share, vb.)

4. **Sunucuda açın:**

```powershell
# PowerShell'de
Expand-Archive -Path node_modules.zip -DestinationPath dashboard\
```

5. **Sunucuda build edin:**

```powershell
cd dashboard
npm run build
npm start
```

### Yöntem 2: npm Cache Kullanma (İnternet Bağlantısı Gerekir)

Eğer sunucuda geçici olarak internet bağlantısı açabiliyorsanız:

```powershell
cd dashboard
npm install
npm run build
npm start
```

### Yöntem 3: Dockerfile ile Build (Önerilen - Production)

Dockerfile zaten hazır. İnternet bağlantısı olan bir makinede build edip image'ı sunucuya aktarın:

```bash
# Build makinesinde
cd dashboard
docker build -t dlp-dashboard:latest .

# Image'ı export edin
docker save dlp-dashboard:latest -o dlp-dashboard.tar

# Sunucuya aktarın ve import edin
docker load -i dlp-dashboard.tar
docker run -p 3002:3002 dlp-dashboard:latest
```

### Yöntem 4: Standalone Build (En İyi - Offline)

Next.js standalone build kullanarak tüm bağımlılıkları bundle'layın:

```powershell
cd dashboard
npm install  # İnternet bağlantısı olan makinede
npm run build  # Standalone build oluşturur
```

Build sonrası `.next/standalone` klasörü oluşur. Bu klasörü sunucuya aktarın:

```powershell
# Sunucuda
cd dashboard
# .next/standalone klasörünü kopyalayın
node .next/standalone/server.js
```

## Windows Server 2025 İçin Adım Adım

### 1. Development Makinesinde Hazırlık

```powershell
cd "C:\DLP_RiskAnalyzer\dashboard"
npm ci
npm run build
```

### 2. node_modules'i Sunucuya Aktarma

```powershell
# Development makinesinde
Compress-Archive -Path "C:\DLP_RiskAnalyzer\dashboard\node_modules" -DestinationPath "C:\node_modules.zip"

# USB veya network share ile sunucuya kopyalayın
```

### 3. Sunucuda Kurulum

```powershell
# Sunucuda
cd "C:\DLP_RiskAnalyzer\dashboard"

# node_modules'i açın
Expand-Archive -Path "C:\node_modules.zip" -DestinationPath ".\"

# Build edin (eğer .next yoksa)
npm run build

# Başlatın
npm start
```

## Kontrol

Kurulumun başarılı olduğunu kontrol edin:

```powershell
cd dashboard
Test-Path node_modules\react-plotly.js  # True dönmeli
Test-Path .next  # True dönmeli
```

## Notlar

- `package-lock.json` zaten git'te commit edilmiş, bu yüzden paket versiyonları sabit
- `npm ci` komutu `package-lock.json`'a göre tam kurulum yapar (önerilen)
- `npm install` yerine `npm ci` kullanın (daha güvenilir)
- Standalone build kullanırsanız, `node_modules`'e ihtiyaç kalmaz (sadece `.next/standalone` yeterli)

## Hızlı Çözüm (Acil Durum)

Eğer hemen çalıştırmak istiyorsanız ve geçici internet bağlantısı açabiliyorsanız:

```powershell
cd dashboard
npm install
npm run build
npm start
```

Sonra internet bağlantısını kapatabilirsiniz. Dashboard çalışmaya devam edecektir.

