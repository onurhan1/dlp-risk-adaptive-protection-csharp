# Network Access Düzeltmesi - Kritik

## 🔴 Sorun

Local ortamda farklı cihazdan giriş yapmaya çalışıldığında "Cannot connect to API" hatası alınıyor. Localhost:5001 çalışıyor ama diğer cihazlardan erişilemiyor.

## ✅ Yapılan Düzeltmeler

### 1. API URL Binding - Zorunlu 0.0.0.0

**Dosya**: `DLP.RiskAnalyzer.Analyzer/Program.cs`

**Değişiklik**:
- API'nin her zaman `0.0.0.0:5001` üzerinde dinlemesi garanti edildi
- Environment variable'da localhost varsa otomatik olarak `0.0.0.0`'a çevriliyor
- Eğer hiç `0.0.0.0` URL'i yoksa, zorunlu olarak ekleniyor

**Kod**:
```csharp
// CRITICAL: Ensure we're listening on 0.0.0.0, not just localhost
if (!app.Urls.Any(url => url.Contains("0.0.0.0")))
{
    app.Urls.Clear();
    app.Urls.Add(defaultUrl);
    Console.WriteLine("WARNING: Forced API to listen on 0.0.0.0:5001 for network access");
}
```

### 2. launchSettings.json - localhost Profili Düzeltildi

**Dosya**: `DLP.RiskAnalyzer.Analyzer/Properties/launchSettings.json`

**Değişiklik**:
- `localhost` profili artık `http://0.0.0.0:5001` kullanıyor
- Tüm profiller network erişimine açık

**Önceki**:
```json
"applicationUrl": "http://localhost:5001"
```

**Sonra**:
```json
"applicationUrl": "http://0.0.0.0:5001"
```

### 3. Dashboard - Network IP Desteği

**Dosya**: `dashboard/package.json`

**Durum**: ✅ Zaten yapılandırılmış
- Dashboard `-H 0.0.0.0` ile başlatılıyor
- Network IP'lerden erişilebilir

### 4. CORS - Internal Network Desteği

**Dosya**: `DLP.RiskAnalyzer.Analyzer/Program.cs` ve `appsettings.json`

**Durum**: ✅ Zaten yapılandırılmış
- `AllowInternalNetwork: true` aktif
- Herhangi bir internal IP:3002 origin'i kabul ediliyor

---

## 🚀 Kullanım

### API'yi Başlatma

**Önemli**: API'yi başlatırken hangi profili kullanırsanız kullanın, artık her zaman `0.0.0.0:5001` üzerinde dinleyecek.

```bash
cd DLP.RiskAnalyzer.Analyzer
dotnet run
```

Veya belirli bir profil ile:
```bash
dotnet run --launch-profile http
dotnet run --launch-profile https
dotnet run --launch-profile localhost  # Artık 0.0.0.0 kullanıyor
```

### Kontrol

API başlatıldığında console'da şunu görmelisiniz:
```
API is listening on:
  - http://0.0.0.0:5001
    Swagger UI: http://0.0.0.0:5001/swagger
    Health Check: http://0.0.0.0:5001/health
```

### Test

1. **Sunucu IP'sini öğrenin**:
   ```bash
   # Windows
   ipconfig
   
   # Linux/Mac
   ifconfig
   # veya
   hostname -I
   ```

2. **Başka bir cihazdan test edin**:
   - Sunucu IP'si: `192.168.1.100` ise
   - Tarayıcıdan: `http://192.168.1.100:5001/health` adresine gidin
   - `{"status":"healthy",...}` yanıtı almalısınız

3. **Dashboard'dan test edin**:
   - Başka bir cihazdan: `http://192.168.1.100:3002` adresine gidin
   - Login sayfası görünmeli
   - Giriş yapın ve API çağrılarının çalıştığını doğrulayın

---

## 🔧 Sorun Giderme

### Sorun: Hala "Cannot connect to API" hatası alıyorum

**Çözüm 1**: API'yi yeniden başlatın
```bash
# API'yi durdurun (Ctrl+C)
# Sonra tekrar başlatın
dotnet run
```

**Çözüm 2**: Firewall kontrolü
```bash
# Windows Firewall'da 5001 portunu açın
netsh advfirewall firewall add rule name="DLP API" dir=in action=allow protocol=TCP localport=5001

# Linux (iptables)
sudo iptables -A INPUT -p tcp --dport 5001 -j ACCEPT
```

**Çözüm 3**: API'nin gerçekten 0.0.0.0'da dinlediğini kontrol edin
```bash
# Windows
netstat -an | findstr :5001

# Linux/Mac
netstat -an | grep :5001
# veya
ss -tlnp | grep :5001
```

Çıktıda şunu görmelisiniz:
```
TCP    0.0.0.0:5001           0.0.0.0:0              LISTENING
```

Eğer `127.0.0.1:5001` görüyorsanız, API hala sadece localhost'ta dinliyor demektir.

**Çözüm 4**: Environment variable kontrolü
```bash
# ASPNETCORE_URLS environment variable'ını kontrol edin
echo $ASPNETCORE_URLS  # Linux/Mac
echo %ASPNETCORE_URLS% # Windows

# Eğer localhost içeriyorsa, temizleyin veya 0.0.0.0 yapın
```

---

## 📝 Özet

✅ **API**: Artık her zaman `0.0.0.0:5001` üzerinde dinliyor  
✅ **Dashboard**: `0.0.0.0:3002` üzerinde dinliyor  
✅ **CORS**: Internal network IP'lerini kabul ediyor  
✅ **API URL Detection**: Otomatik olarak doğru API URL'ini kullanıyor  

**Sonuç**: Artık farklı cihazlardan network IP ile erişilebilir olmalı. ✅

---

## ⚠️ Önemli Notlar

1. **API'yi yeniden başlatın**: Değişikliklerin etkili olması için API'yi durdurup yeniden başlatmanız gerekiyor.

2. **Firewall**: Windows Firewall veya Linux firewall'da 5001 portunun açık olduğundan emin olun.

3. **Network**: Sunucu ve client cihazların aynı network'te olduğundan emin olun.

4. **IP Adresi**: Sunucunun IP adresini doğru öğrendiğinizden emin olun (private IP, public IP değil).

---

**Son Güncelleme**: 2025-01-XX  
**Kritiklik**: 🔴 YÜKSEK - Network erişimi için zorunlu

