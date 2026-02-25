# Windows Server 2025 Login Sorun Giderme Rehberi

## 🔍 Olası Sorun Nedenleri

### 1. **Password Hash Her Başlatmada Değişiyor (KRİTİK)**

**Sorun**: `UsersController` her API başlatıldığında yeni bir salt/hash oluşturuyor. Bu demek ki:
- İlk başlatmada `admin123` için bir hash oluşturuluyor
- API yeniden başlatıldığında, **yeni bir salt/hash oluşturuluyor**
- Eski hash ile yeni hash eşleşmiyor!

**Neden Mac OS'da Sorun Yok?**
- Mac'te API sürekli çalışıyor (yeniden başlatılmıyor)
- Mac'te development ortamında API restart'ları nadir
- Windows Server'da NSSM ile servis olarak çalışıyor, her restart'ta yeni hash
- Windows Server'da sistem restart'ları veya servis restart'ları daha sık

**Çözüm**: Password hash'i database'e kaydetmek veya sabit bir salt kullanmak gerekiyor.

**Geçici Çözüm**: API'yi yeniden başlatın ve hemen login yapmayı deneyin.

### 2. **API URL Yanlış Yapılandırılmış**

**Kontrol**:
1. Dashboard'u açın: `http://[SERVER_IP]:3002`
2. Browser Console'u açın (F12)
3. Şunu çalıştırın:
```javascript
console.log('API URL:', window.location.origin.replace(':3002', ':5001'));
```

**Beklenen**: `http://[SERVER_IP]:5001`

**Sorun**: Eğer `localhost` veya yanlış IP görüyorsanız, `dashboard/lib/api-config.ts` dosyasını kontrol edin.

### 3. **Encoding/Line Ending Sorunları**

Windows Server'da farklı encoding kullanılıyor olabilir. Son güncellemelerle bu sorun çözülmüş olmalı, ama kontrol edin:

**Backend Log Kontrolü**:
```powershell
# API log dosyasını kontrol edin
Get-Content "DLP.RiskAnalyzer.Analyzer\api.log" -Tail 50 | Select-String "Login"
```

**Beklenen Log Mesajları**:
```
Login attempt - Username: 'admin' (Length: 5), Password Length: 8
User found - Username: admin, HasPasswordHash: True, HasPasswordSalt: True
Password validation for user admin: SUCCESS
```

### 4. **CORS Sorunu**

**Kontrol**: Browser Console'da (F12) Network tab'ı açın ve login request'ini kontrol edin:
- Status: `401` → Authentication sorunu
- Status: `CORS error` → CORS sorunu
- Status: `404` → API URL yanlış

### 5. **API Çalışmıyor**

**Kontrol**:
```powershell
# Port 5001'i kontrol edin
netstat -ano | findstr :5001

# API health check
Invoke-WebRequest -Uri "http://localhost:5001/health" -UseBasicParsing
```

**Beklenen**: `{"status":"healthy",...}`

## 🔧 Adım Adım Sorun Giderme

### Adım 1: API Log'larını Kontrol Edin

```powershell
# API log dosyasını açın
notepad "DLP.RiskAnalyzer.Analyzer\api.log"

# Veya PowerShell'de son 50 satırı gösterin
Get-Content "DLP.RiskAnalyzer.Analyzer\api.log" -Tail 50
```

**Arayın**:
- `Login attempt` mesajları
- `Password validation` sonuçları
- `User not found` hataları
- `Password hash verification test` sonuçları

### Adım 2: Browser Console'da Test Edin

Dashboard'u açın (`http://[SERVER_IP]:3002`) ve Browser Console'da (F12) şunu çalıştırın:

```javascript
// Test 1: API URL kontrolü
fetch('http://localhost:5001/health')
  .then(r => r.json())
  .then(console.log)
  .catch(console.error);

// Test 2: Login testi
fetch('http://localhost:5001/api/auth/login', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ username: 'admin', password: 'admin123' })
})
.then(r => {
  console.log('Status:', r.status);
  return r.json();
})
.then(data => {
  console.log('Response:', data);
  if (data.token) {
    console.log('✅ LOGIN SUCCESS!');
  } else {
    console.log('❌ LOGIN FAILED:', data);
  }
})
.catch(err => console.error('Error:', err));
```

### Adım 3: API'yi Yeniden Başlatın

**Neden**: Password hash her başlatmada değişiyor olabilir.

```powershell
# NSSM ile çalışıyorsa
nssm restart DLP-Analyzer-API

# Veya manuel olarak
# Servisi durdurun ve tekrar başlatın
```

**Önemli**: API yeniden başlatıldıktan sonra **hemen** login yapmayı deneyin.

### Adım 4: Password Hash Sabitleme (Geçici Çözüm)

Eğer sorun devam ediyorsa, `UsersController.cs` dosyasında sabit bir salt kullanabilirsiniz:

```csharp
// Geçici çözüm: Sabit salt kullan
private static readonly byte[] FIXED_SALT = Convert.FromBase64String("c2FsdF9mb3JfdGVzdGluZw==");

private static (string Hash, string Salt) CreatePasswordHash(string password)
{
    // Sabit salt kullan (sadece test için!)
    var hashBytes = Rfc2898DeriveBytes.Pbkdf2(password, FIXED_SALT, 100000, HashAlgorithmName.SHA256, 32);
    return (Convert.ToBase64String(hashBytes), Convert.ToBase64String(FIXED_SALT));
}
```

**⚠️ UYARI**: Bu sadece test için! Production'da database'e kaydedin.

### Adım 5: Network Tab'da Request/Response Kontrolü

1. Dashboard'u açın (`http://[SERVER_IP]:3002`)
2. Browser DevTools'u açın (F12)
3. Network tab'ına gidin
4. Login yapmayı deneyin
5. `/api/auth/login` request'ini bulun
6. Kontrol edin:
   - **Request Headers**: `Content-Type: application/json; charset=utf-8`
   - **Request Payload**: `{"username":"admin","password":"admin123"}`
   - **Response Status**: `200` (başarılı) veya `401` (başarısız)
   - **Response Body**: Token varsa başarılı, `{"detail":"Invalid username or password"}` varsa başarısız

## 📋 Kontrol Listesi

- [ ] API çalışıyor mu? (`http://localhost:5001/health`)
- [ ] Dashboard çalışıyor mu? (`http://[SERVER_IP]:3002`)
- [ ] API log'larında login attempt görünüyor mu?
- [ ] Password validation sonucu ne?
- [ ] Browser Console'da hata var mı?
- [ ] Network tab'da request/response doğru mu?
- [ ] API yeniden başlatıldı mı?

## 🚨 En Yaygın Sorun: Password Hash Değişimi

**Sorun**: Her API başlatıldığında yeni salt/hash oluşturuluyor.

**Çözüm**: 
1. **Kısa vadeli**: API'yi yeniden başlatın ve hemen login yapın
2. **Uzun vadeli**: Password hash'i database'e kaydedin (Users tablosu oluşturun)

## 📞 Destek

Eğer sorun devam ediyorsa, şu bilgileri toplayın:

1. API log dosyası (`api.log`)
2. Browser Console çıktısı
3. Network tab screenshot'u
4. API health check sonucu
5. Windows Server sürümü ve .NET sürümü

