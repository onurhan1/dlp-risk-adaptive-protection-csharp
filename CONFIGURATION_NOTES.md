# Yapılandırma Notları

## ⚠️ Önemli: Yapılandırma Dosyalarını Düzenlemeniz Gerekiyor!

Bu projede **gerçek IP adresleri ve kimlik bilgileri** placeholder olarak bırakılmıştır. 
Sistemi çalıştırmadan önce aşağıdaki dosyaları düzenlemeniz **zorunludur**.

---

## 📝 Düzenlenmesi Gereken Dosyalar

### 1. Collector Service Yapılandırması

**Dosya**: `DLP.RiskAnalyzer.Collector/appsettings.json`

```json
{
  "DLP": {
    "ManagerIP": "YOUR_DLP_MANAGER_IP",      // ← Forcepoint DLP Manager IP adresini yazın
    "ManagerPort": 8443,                     // Port genellikle 8443 (HTTPS)
    "Username": "YOUR_DLP_USERNAME",         // ← Forcepoint DLP API kullanıcı adını yazın
    "Password": "YOUR_DLP_PASSWORD"          // ← Forcepoint DLP API şifresini yazın
  },
  "Redis": {
    "Host": "localhost",                     // Redis host (genellikle localhost)
    "Port": 6379                             // Redis port (varsayılan 6379)
  }
}
```

**Örnek:**
```json
{
  "DLP": {
    "ManagerIP": "10.0.0.100",
    "ManagerPort": 8443,
    "Username": "dlp_api_user",
    "Password": "SecurePassword123!"
  }
}
```

### 2. Analyzer API Yapılandırması

**Dosya**: `DLP.RiskAnalyzer.Analyzer/appsettings.json`

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=dlp_analytics;Username=postgres;Password=postgres"
    // ↑ PostgreSQL şifresini kendi şifrenizle değiştirin (Docker kullanıyorsanız genellikle 'postgres')
  },
  "Redis": {
    "Host": "localhost",
    "Port": 6379
  },
  "DLP": {
    "ManagerIP": "YOUR_DLP_MANAGER_IP",      // ← Forcepoint DLP Manager IP adresini yazın
    "ManagerPort": 8443,
    "Username": "YOUR_DLP_USERNAME",         // ← Forcepoint DLP API kullanıcı adını yazın
    "Password": "YOUR_DLP_PASSWORD"          // ← Forcepoint DLP API şifresini yazın
  }
}
```

---

## 🔍 Yapılandırma Değerlerini Nasıl Bulabilirsiniz?

### Forcepoint DLP Manager IP Adresi

1. Forcepoint DLP Manager yönetim konsoluna giriş yapın
2. System Settings / Network Settings bölümünden IP adresini bulun
3. VEYA hostname kullanabilirsiniz: `dlp.company.com`

### Forcepoint DLP API Kullanıcı Bilgileri

1. Forcepoint DLP Manager'da API kullanıcısı oluşturun
2. API erişimi için gerekli izinleri verin
3. Kullanıcı adı ve şifresini not edin

### PostgreSQL Şifresi

- **Docker kullanıyorsanız**: Genellikle `postgres` (container kurulumunda belirlediğiniz şifre)
- **Homebrew/Manuel kurulum**: PostgreSQL kurulumu sırasında belirlediğiniz şifre
- **Bağlantı testi**: `psql -U postgres -h localhost` ile şifreyi kontrol edebilirsiniz

---

## 🔒 Güvenlik Notları

1. **⚠️ `appsettings.json` dosyalarını `.gitignore`'a ekleyin!**
   - Hassas bilgileri (şifreler, IP adresleri) Git'e commit etmeyin
   - `.env` dosyası kullanmayı düşünebilirsiniz (production için)

2. **Production Ortamı için:**
   - Şifreleri environment variables kullanın
   - Azure Key Vault veya benzeri güvenli depolama çözümleri kullanın
   - HTTPS kullanın
   - SSL certificate validation'ı production'da etkinleştirin

3. **Test Ortamı için:**
   - `appsettings.json` dosyalarını güvenli tutun
   - Gerçek production şifrelerini kullanmayın

---

## ✅ Yapılandırma Kontrol Checklist

Kurulumdan önce kontrol edin:

- [ ] `DLP.RiskAnalyzer.Collector/appsettings.json` - DLP Manager IP, Username, Password dolduruldu
- [ ] `DLP.RiskAnalyzer.Analyzer/appsettings.json` - DLP Manager IP, Username, Password dolduruldu
- [ ] `DLP.RiskAnalyzer.Analyzer/appsettings.json` - PostgreSQL şifresi dolduruldu
- [ ] Forcepoint DLP Manager erişilebilir (ping testi yapabilirsiniz)
- [ ] PostgreSQL çalışıyor ve bağlanılabiliyor
- [ ] Redis çalışıyor ve bağlanılabiliyor

---

## 🧪 Yapılandırma Testi

Yapılandırmayı test etmek için:

```bash
# Collector Service başlattığınızda, loglar şunları göstermeli:
# - "Access token obtained" (DLP API bağlantısı başarılı)
# - "Fetched X incidents from DLP API" (Veri çekme başarılı)

# Analyzer API başlattığınızda:
# - http://localhost:8000/health endpoint'i çalışmalı
# - Swagger UI açılabilmeli: http://localhost:8000/swagger
```

---

## 📞 Yardım

Yapılandırma ile ilgili sorun yaşarsanız:

1. Log dosyalarını kontrol edin
2. DLP Manager IP adresinin erişilebilir olduğunu doğrulayın (ping, telnet)
3. API kullanıcı bilgilerinin doğru olduğunu kontrol edin
4. PostgreSQL ve Redis bağlantılarını test edin

---

**Yapılandırma tamamlandıktan sonra servisleri başlatabilirsiniz! 🚀**

