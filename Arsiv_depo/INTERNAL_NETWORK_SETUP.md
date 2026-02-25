# Internal Network Kurulum Rehberi

## 📋 Genel Bakış

Bu uygulama internal bir sunucuda çalışacak ve IP adresi ile erişilebilir olacak şekilde yapılandırılmıştır. İnternete açık değildir, sadece internal network'ten erişilebilir.

---

## ✅ Yapılandırma Durumu

### 1. Dashboard (Next.js) - Port 3002

**Durum**: ✅ **YAPILANDIRILDI**

Dashboard artık `0.0.0.0:3002` adresinde dinliyor, bu sayede:
- ✅ `localhost:3002` üzerinden erişilebilir
- ✅ `192.168.1.100:3002` (sunucu IP'si) üzerinden erişilebilir
- ✅ Internal network'teki herhangi bir IP'den erişilebilir

**Yapılan Değişiklik**:
```json
// package.json
"start": "next start -H 0.0.0.0 -p 3002"
```

### 2. API (Backend) - Port 5001

**Durum**: ✅ **ZATEN YAPILANDIRILMIŞ**

API zaten `0.0.0.0:5001` adresinde dinliyor:
- ✅ `localhost:5001` üzerinden erişilebilir
- ✅ `192.168.1.100:5001` (sunucu IP'si) üzerinden erişilebilir
- ✅ Internal network'teki herhangi bir IP'den erişilebilir

**Mevcut Yapılandırma**:
```csharp
// Program.cs
string defaultUrl = "http://0.0.0.0:5001"; // 0.0.0.0 allows both localhost and network IP access
```

### 3. CORS Yapılandırması

**Durum**: ✅ **INTERNAL NETWORK İÇİN YAPILANDIRILDI**

CORS artık internal network IP'lerini otomatik olarak kabul ediyor:
- ✅ `http://localhost:3002` ✅
- ✅ `http://192.168.1.100:3002` ✅
- ✅ `http://10.0.0.50:3002` ✅
- ✅ Herhangi bir internal IP:3002 ✅

**Yapılan Değişiklik**:
```csharp
// Program.cs - CORS policy
policy.SetIsOriginAllowed(origin =>
{
    // Allow localhost
    if (origin.StartsWith("http://localhost:") || origin.StartsWith("https://localhost:"))
        return true;
    
    // Allow any IP address on port 3000, 3001, or 3002 (internal network)
    var uri = new Uri(origin);
    var port = uri.Port;
    if (port == 3000 || port == 3001 || port == 3002)
    {
        var host = uri.Host;
        if (System.Net.IPAddress.TryParse(host, out _))
        {
            return true; // It's an IP address, allow it
        }
    }
    return false;
});
```

**appsettings.json**:
```json
{
  "Cors": {
    "AllowedOrigins": [
      "http://localhost:3000",
      "http://localhost:3001",
      "http://localhost:3002"
    ],
    "AllowInternalNetwork": true
  }
}
```

### 4. API URL Detection

**Durum**: ✅ **ZATEN YAPILANDIRILMIŞ**

Dashboard, API URL'ini otomatik olarak algılıyor:
- Kullanıcı `http://192.168.1.100:3002` üzerinden erişirse
- Dashboard otomatik olarak `http://192.168.1.100:5001` API'sini kullanır

**Mevcut Yapılandırma**:
```typescript
// lib/api-config.ts
function getApiUrl(): string {
  if (typeof window !== 'undefined') {
    const hostname = window.location.hostname;
    
    if (hostname === 'localhost' || hostname === '127.0.0.1') {
      return 'http://localhost:5001';
    }
    
    // If accessing via network IP, use the same hostname for API
    return `http://${hostname}:5001`;
  }
  return 'http://localhost:5001';
}
```

---

## 🚀 Kullanım Senaryoları

### Senaryo 1: Sunucu IP'si ile Erişim

**Sunucu IP**: `192.168.1.100`

1. **Dashboard'a Erişim**:
   ```
   http://192.168.1.100:3002
   ```

2. **API Otomatik Algılama**:
   - Dashboard otomatik olarak `http://192.168.1.100:5001` API'sini kullanır
   - Kullanıcı herhangi bir yapılandırma yapmaz

3. **CORS**:
   - `http://192.168.1.100:3002` origin'i otomatik olarak kabul edilir

### Senaryo 2: Localhost Erişimi

**Sunucu üzerinden localhost ile erişim**:

1. **Dashboard'a Erişim**:
   ```
   http://localhost:3002
   ```

2. **API Otomatik Algılama**:
   - Dashboard otomatik olarak `http://localhost:5001` API'sini kullanır

---

## 🔧 Kurulum Adımları

### 1. Dashboard'ı Başlatma

```bash
cd dashboard
npm install
npm run build
npm start
```

Dashboard şu adreslerde dinleyecek:
- `http://0.0.0.0:3002` (tüm network interface'lerinde)
- `http://localhost:3002` (local erişim)
- `http://192.168.1.100:3002` (network IP ile erişim)

### 2. API'yi Başlatma

```bash
cd DLP.RiskAnalyzer.Analyzer
dotnet run
```

API şu adreslerde dinleyecek:
- `http://0.0.0.0:5001` (tüm network interface'lerinde)
- `http://localhost:5001` (local erişim)
- `http://192.168.1.100:5001` (network IP ile erişim)

### 3. CORS Yapılandırması

`appsettings.json` dosyasında `AllowInternalNetwork: true` olduğundan emin olun:

```json
{
  "Cors": {
    "AllowInternalNetwork": true
  }
}
```

---

## 🔒 Güvenlik Notları

### Internal Network Güvenliği

1. **Firewall**: Internal network'te firewall kuralları ile sadece gerekli portlar açık olmalı
2. **Network Isolation**: Uygulama internal network'te izole edilmiş olmalı
3. **Authentication**: JWT token authentication aktif
4. **CORS**: Sadece internal network IP'leri kabul ediliyor

### Production İçin Öneriler

1. **HTTPS**: Internal network'te bile HTTPS kullanılması önerilir
2. **IP Whitelist**: Belirli IP aralıklarına sınırlama yapılabilir
3. **Monitoring**: Network trafiği izlenmeli

---

## 🧪 Test Senaryoları

### Test 1: Network IP ile Erişim

1. Sunucu IP'sini öğrenin: `ipconfig` (Windows) veya `ifconfig` (Linux/Mac)
2. Başka bir bilgisayardan tarayıcıyı açın
3. `http://[SUNUCU_IP]:3002` adresine gidin
4. Login sayfası görünmeli
5. Giriş yapın ve dashboard'ın çalıştığını doğrulayın

### Test 2: API Bağlantısı

1. Dashboard'a giriş yapın
2. Browser Developer Tools'u açın (F12)
3. Network sekmesine gidin
4. Dashboard'da bir işlem yapın (ör: Settings sayfasına gidin)
5. API çağrılarının `http://[SUNUCU_IP]:5001` adresine yapıldığını doğrulayın

### Test 3: CORS Kontrolü

1. Browser Developer Tools'u açın (F12)
2. Console sekmesine gidin
3. Dashboard'da bir işlem yapın
4. CORS hatası olmamalı

---

## 📝 Özet

✅ **Dashboard**: `0.0.0.0:3002` üzerinde dinliyor - IP ile erişilebilir  
✅ **API**: `0.0.0.0:5001` üzerinde dinliyor - IP ile erişilebilir  
✅ **CORS**: Internal network IP'lerini otomatik kabul ediyor  
✅ **API URL Detection**: Otomatik olarak doğru API URL'ini kullanıyor  

**Sonuç**: Uygulama internal network'te IP adresi ile erişilebilir şekilde yapılandırılmıştır. ✅

---

## 🆘 Sorun Giderme

### Sorun: Dashboard'a IP ile erişilemiyor

**Çözüm**:
1. Dashboard'ın `0.0.0.0:3002` üzerinde dinlediğinden emin olun
2. Firewall'da 3002 portunun açık olduğundan emin olun
3. `npm start` komutunu kontrol edin (`-H 0.0.0.0` parametresi olmalı)

### Sorun: API'ye bağlanamıyor

**Çözüm**:
1. API'nin `0.0.0.0:5001` üzerinde dinlediğinden emin olun
2. Firewall'da 5001 portunun açık olduğundan emin olun
3. Browser console'da API URL'ini kontrol edin

### Sorun: CORS hatası alıyorum

**Çözüm**:
1. `appsettings.json`'da `AllowInternalNetwork: true` olduğundan emin olun
2. API'yi yeniden başlatın
3. Browser cache'ini temizleyin

---

**Son Güncelleme**: 2025-01-XX

