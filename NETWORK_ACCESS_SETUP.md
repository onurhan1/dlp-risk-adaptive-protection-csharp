# Network Access Setup - API'yi Ağ Üzerinden Erişilebilir Yapma

## 🎯 Amaç

API'yi sadece localhost'tan değil, ağ üzerindeki diğer cihazlardan da erişilebilir hale getirmek.

## 📋 Yapılan Değişiklikler

### 1. Program.cs - Next.js Benzeri Binding

API artık Next.js gibi çalışıyor: `0.0.0.0` IP adresinde dinliyor, bu sayede hem `localhost:5001` hem de `[network-ip]:5001` ile erişilebilir.

**Önceki Kod:**
```csharp
app.Urls.Add($"http://localhost:{port}");
```

**Yeni Kod:**
```csharp
// ASPNETCORE_URLS environment variable'ını kontrol et
// Eğer set edilmişse onu kullan, yoksa varsayılan olarak 0.0.0.0:5001 kullan
// 0.0.0.0 hem localhost hem de network IP'si ile erişime izin verir (Next.js gibi)
var urlsEnv = Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
string defaultUrl = "http://0.0.0.0:5001";

if (!string.IsNullOrEmpty(urlsEnv))
{
    var urls = urlsEnv.Split(';', StringSplitOptions.RemoveEmptyEntries);
    foreach (var url in urls)
    {
        app.Urls.Add(url.Trim());
    }
}
else
{
    app.Urls.Add(defaultUrl);
}
```

### 2. launchSettings.json - Network Profile

`launchSettings.json` dosyasına network erişimi için yeni bir profile eklendi:

- **http profile**: `http://0.0.0.0:5001` - Network erişimi için
- **localhost profile**: `http://localhost:5001` - Sadece local erişim için

## 🚀 Kullanım

### Visual Studio'da Çalıştırma

1. **Solution Explorer**'da `DLP.RiskAnalyzer.Analyzer` projesine sağ tıklayın
2. **Properties** → **Debug** → **General**
3. **Launch profiles** dropdown'ından **http** seçin (0.0.0.0 ile başlar)
4. F5 ile çalıştırın

### Komut Satırından Çalıştırma

```powershell
# Network erişimi ile (0.0.0.0) - Next.js gibi hem localhost hem network IP ile çalışır
$env:ASPNETCORE_URLS="http://0.0.0.0:5001"
dotnet run --project DLP.RiskAnalyzer.Analyzer

# Veya tek satırda
dotnet run --project DLP.RiskAnalyzer.Analyzer --urls "http://0.0.0.0:5001"

# Veya environment variable olmadan (varsayılan 0.0.0.0:5001 kullanılır)
dotnet run --project DLP.RiskAnalyzer.Analyzer
```

### Sadece Localhost'tan Erişim (Güvenlik)

Eğer sadece localhost'tan erişim istiyorsanız:

```powershell
$env:ASPNETCORE_URLS="http://localhost:5001"
dotnet run --project DLP.RiskAnalyzer.Analyzer
```

## 🌐 Network Erişimi (Next.js Benzeri)

API `0.0.0.0:5001` adresinde başladıktan sonra, **Next.js gibi** hem localhost hem de network IP'si ile erişilebilir:

1. **Bilgisayarın IP adresini bulun:**
   ```powershell
   ipconfig
   # IPv4 Address: 192.168.1.100 (örnek)
   ```

2. **Erişim yöntemleri (her ikisi de çalışır):**
   - **Localhost:** `http://localhost:5001` ✅
   - **Network IP:** `http://192.168.1.100:5001` ✅
   - Swagger UI: `http://localhost:5001/swagger` veya `http://192.168.1.100:5001/swagger`
   - Health Check: `http://localhost:5001/health` veya `http://192.168.1.100:5001/health`

3. **Dashboard yapılandırması:**
   - **Next.js Dashboard:** `.env.local` dosyasında (her iki URL de çalışır):
     ```
     # Localhost kullanabilirsiniz
     NEXT_PUBLIC_API_URL=http://localhost:5001
     
     # Veya network IP kullanabilirsiniz
     NEXT_PUBLIC_API_URL=http://192.168.1.100:5001
     ```
   - **WPF Dashboard:** `appsettings.json` dosyasında (her iki URL de çalışır):
     ```json
     {
       "ApiBaseUrl": "http://localhost:5001"
     }
     ```
     veya
     ```json
     {
       "ApiBaseUrl": "http://192.168.1.100:5001"
     }
     ```

## 🔒 Windows Firewall Yapılandırması

Windows Firewall, 5001 portunu engelliyor olabilir. Aşağıdaki adımları izleyin:

### PowerShell ile Firewall Kuralı Ekleme (Yönetici)

```powershell
# PowerShell'i Yönetici olarak açın
New-NetFirewallRule -DisplayName "DLP Analyzer API" -Direction Inbound -LocalPort 5001 -Protocol TCP -Action Allow
```

### Manuel Firewall Yapılandırması

1. **Windows Defender Firewall**'ı açın
2. **Advanced settings** → **Inbound Rules** → **New Rule**
3. **Port** seçin → **Next**
4. **TCP** seçin → **Specific local ports**: `5001` → **Next**
5. **Allow the connection** → **Next**
6. Tüm profilleri seçin (Domain, Private, Public) → **Next**
7. **Name**: "DLP Analyzer API" → **Finish**

## ✅ Test

### 1. API'nin Çalıştığını Kontrol Edin

**Aynı bilgisayardan:**
```powershell
Invoke-WebRequest -Uri "http://localhost:5001/health" -UseBasicParsing
```

**Ağ üzerindeki başka bir cihazdan:**
```powershell
# Bilgisayarın IP adresini kullanın
Invoke-WebRequest -Uri "http://192.168.1.100:5001/health" -UseBasicParsing
```

### 2. Swagger UI'yi Test Edin

Tarayıcıda açın:
- Local: `http://localhost:5001/swagger`
- Network: `http://192.168.1.100:5001/swagger`

### 3. Dashboard'dan Test Edin

- Next.js Dashboard: `http://192.168.1.100:3002` (veya diğer cihazın IP'si)
- Login sayfasında giriş yapmayı deneyin

## ⚠️ Güvenlik Notları

1. **Production Ortamı:**
   - `0.0.0.0` binding kullanmayın
   - Belirli IP adreslerine bağlayın veya reverse proxy kullanın
   - HTTPS kullanın
   - Authentication ve Authorization'ı etkinleştirin

2. **Development Ortamı:**
   - `0.0.0.0` kullanabilirsiniz ama sadece güvenli ağlarda
   - Firewall kurallarını dikkatli yapılandırın

3. **CORS Ayarları:**
   - Şu anda `AllowAnyOrigin` aktif (development için uygun)
   - Production'da spesifik origin'ler belirtin

## 🔧 Sorun Giderme

### Problem: Diğer cihazlardan erişilemiyor

**Çözüm:**
1. Windows Firewall'ı kontrol edin (yukarıdaki adımları izleyin)
2. API'nin `0.0.0.0:5001` adresinde dinlediğini kontrol edin (console loglarına bakın)
3. Bilgisayarın IP adresinin doğru olduğunu kontrol edin (`ipconfig`)
4. Router'ın port forwarding yapılandırmasını kontrol edin (gerekirse)

### Problem: API başlamıyor

**Çözüm:**
1. Port 5001'in başka bir uygulama tarafından kullanılmadığını kontrol edin:
   ```powershell
   netstat -ano | findstr :5001
   ```
2. Yönetici yetkisiyle çalıştırın (gerekirse)
3. Farklı bir port deneyin (örn: 5002)

### Problem: CORS hatası

**Çözüm:**
- `Program.cs`'de CORS ayarlarını kontrol edin
- Dashboard'un doğru API URL'ini kullandığından emin olun

## 📝 Özet

- ✅ API artık `0.0.0.0:5001` adresinde dinliyor
- ✅ Network üzerindeki diğer cihazlardan erişilebilir
- ✅ Windows Firewall kuralı eklenmeli
- ✅ Dashboard'ların API URL'ini güncellemeyi unutmayın

---

**Not:** Bu yapılandırma development ortamı için uygundur. Production ortamında ek güvenlik önlemleri alınmalıdır.

