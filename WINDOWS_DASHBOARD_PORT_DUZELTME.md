# Windows Dashboard Port Düzeltme Rehberi - Adım Adım

## 🔧 Sorun
Dashboard uygulaması API'yi 8000 portunda arıyor ama API 5001 portunda çalışıyor.

## 📋 Adım Adım Çözüm

### Adım 1: Build Output Dizinine Git

1. **Windows Explorer'ı açın**
2. Proje dizinine gidin (örnek: `C:\Projects\dlp-risk-adaptive-protection-csharp`)
3. Şu dizine gidin:
   ```
   DLP.RiskAnalyzer.Dashboard\bin\Debug\net8.0-windows
   ```

### Adım 2: appsettings.json Dosyasını Kontrol Et

1. `bin\Debug\net8.0-windows` dizininde `appsettings.json` dosyasının olup olmadığını kontrol edin
2. **Eğer dosya YOKSA:**
   - Kaynak dizindeki `appsettings.json` dosyasını kopyalayın
   - Kaynak: `DLP.RiskAnalyzer.Dashboard\appsettings.json`
   - Hedef: `DLP.RiskAnalyzer.Dashboard\bin\Debug\net8.0-windows\appsettings.json`

### Adım 3: appsettings.json Dosyasını Düzenle

1. `bin\Debug\net8.0-windows\appsettings.json` dosyasını **Notepad** veya **Visual Studio Code** ile açın
2. İçeriğini şu şekilde düzenleyin:

```json
{
  "ApiBaseUrl": "http://localhost:5001"
}
```

3. Dosyayı **kaydedin** (Ctrl+S)

### Adım 4: Visual Studio'da Projeyi Temizle ve Yeniden Build Et

1. **Visual Studio'yu açın**
2. **Solution Explorer**'da `DLP.RiskAnalyzer.Dashboard` projesine sağ tıklayın
3. **Clean** seçeneğini tıklayın
4. Tekrar sağ tıklayın ve **Rebuild** seçeneğini tıklayın
5. Build'in başarılı olduğunu kontrol edin

### Adım 5: appsettings.json Dosyasının Kopyalandığını Doğrula

1. Build tamamlandıktan sonra tekrar şu dizine gidin:
   ```
   DLP.RiskAnalyzer.Dashboard\bin\Debug\net8.0-windows
   ```
2. `appsettings.json` dosyasının burada olduğundan emin olun
3. Dosyayı açıp içeriğinin doğru olduğunu kontrol edin:
   ```json
   {
     "ApiBaseUrl": "http://localhost:5001"
   }
   ```

### Adım 6: Dashboard'u Çalıştır ve Test Et

1. Visual Studio'da **F5** tuşuna basarak Dashboard'u çalıştırın
2. **Output** penceresini açın (View → Output veya Ctrl+Alt+O)
3. **Show output from:** dropdown'ından **Debug** seçin
4. Dashboard açıldığında Output penceresinde şu logları görmelisiniz:
   ```
   [LoginWindow] API Base URL: http://localhost:5001
   [LoginWindow] Config file path: C:\...\bin\Debug\net8.0-windows\appsettings.json
   [LoginWindow] Config file exists: True
   ```
5. Login ekranında `admin` / `admin123` ile giriş yapmayı deneyin

---

## 🔍 Alternatif Çözüm: Environment Variable Kullan

Eğer yukarıdaki adımlar işe yaramazsa, environment variable kullanabilirsiniz:

### Visual Studio'da Environment Variable Ekle

1. **Solution Explorer**'da `DLP.RiskAnalyzer.Dashboard` projesine sağ tıklayın
2. **Properties** seçeneğini tıklayın
3. **Debug** sekmesine gidin
4. **Environment variables** bölümüne şunu ekleyin:
   - **Name:** `ApiBaseUrl`
   - **Value:** `http://localhost:5001`
5. **Save** butonuna tıklayın
6. Dashboard'u yeniden çalıştırın

---

## 🛠️ Komut Satırından Düzeltme (PowerShell)

Eğer komut satırından düzeltmek isterseniz:

```powershell
# 1. Proje dizinine git
cd "C:\Projects\dlp-risk-adaptive-protection-csharp\DLP.RiskAnalyzer.Dashboard"

# 2. appsettings.json dosyasını düzenle
$configPath = "appsettings.json"
$config = @{
    ApiBaseUrl = "http://localhost:5001"
} | ConvertTo-Json
Set-Content -Path $configPath -Value $config

# 3. Build output dizinine kopyala
$outputPath = "bin\Debug\net8.0-windows\appsettings.json"
Copy-Item -Path $configPath -Destination $outputPath -Force

# 4. Projeyi temizle ve build et
dotnet clean
dotnet build

# 5. appsettings.json'ın kopyalandığını kontrol et
if (Test-Path $outputPath) {
    Write-Host "✓ appsettings.json başarıyla kopyalandı" -ForegroundColor Green
    Get-Content $outputPath
} else {
    Write-Host "✗ appsettings.json kopyalanamadı!" -ForegroundColor Red
}
```

---

## ✅ Doğrulama Checklist

- [ ] `bin\Debug\net8.0-windows\appsettings.json` dosyası var
- [ ] Dosya içeriği `"ApiBaseUrl": "http://localhost:5001"` şeklinde
- [ ] Visual Studio Output penceresinde `[LoginWindow] API Base URL: http://localhost:5001` görünüyor
- [ ] Dashboard açıldığında login ekranı geliyor
- [ ] `admin` / `admin123` ile giriş yapılabiliyor

---

## 🚨 Hala Sorun Varsa

### 1. API'nin Çalıştığını Kontrol Et

PowerShell'de:
```powershell
# API'nin 5001 portunda çalıştığını kontrol et
Invoke-WebRequest -Uri "http://localhost:5001/health" -UseBasicParsing
```

Eğer hata alırsanız, API çalışmıyor demektir. API'yi başlatın.

### 2. Firewall Kontrolü

Windows Firewall'un 5001 portunu engellemediğinden emin olun.

### 3. Debug Loglarını Kontrol Et

Visual Studio'da:
1. **Debug → Windows → Output** açın
2. Dashboard'u çalıştırın
3. Output penceresindeki logları kontrol edin
4. Hangi URL'in kullanıldığını göreceksiniz

### 4. Manuel appsettings.json Kopyalama

Eğer otomatik kopyalama çalışmıyorsa:
1. `DLP.RiskAnalyzer.Dashboard\appsettings.json` dosyasını açın
2. İçeriğini kopyalayın
3. `DLP.RiskAnalyzer.Dashboard\bin\Debug\net8.0-windows\appsettings.json` dosyasını oluşturun
4. İçeriği yapıştırın ve kaydedin

---

## 📝 Notlar

- Build sonrası `appsettings.json` dosyası otomatik olarak kopyalanmalı
- Eğer kopyalanmıyorsa, `.csproj` dosyasındaki `CopyToOutputDirectory` ayarını kontrol edin
- Dashboard'u her çalıştırdığınızda output dizinindeki `appsettings.json` dosyasını kontrol edin
- Debug logları hangi URL'in kullanıldığını gösterir

---

**Sorun devam ederse, Output penceresindeki logları paylaşın!**

