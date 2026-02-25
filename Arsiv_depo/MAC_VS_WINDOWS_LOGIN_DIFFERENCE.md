# Mac OS vs Windows Server 2025 Login Farkı

## 🤔 Neden Mac OS'da Sorun Yok?

### 1. **API Restart Sıklığı Farkı**

**Mac OS (Development Ortamı)**:
- API genellikle sürekli çalışıyor (`dotnet run` ile başlatılıyor)
- API restart'ları nadir (sadece kod değişikliği sonrası)
- Her restart'ta yeni hash oluşsa bile, restart'lar çok nadir
- Development'ta genellikle tek bir session'da çalışıyor

**Windows Server 2025 (Production)**:
- API NSSM ile Windows Service olarak çalışıyor
- Sistem restart'ları sonrası API otomatik başlıyor → **Yeni hash!**
- Servis restart'ları (update, maintenance) → **Yeni hash!**
- Her restart'ta yeni salt/hash oluşturuluyor
- İlk başlatmada oluşturulan hash ile sonraki başlatmalardaki hash farklı!

### 2. **Password Hash Oluşturma Mekanizması**

```csharp
// UsersController.cs - Her başlatmada çalışıyor
private static (string Hash, string Salt) CreatePasswordHash(string password)
{
    var saltBytes = RandomNumberGenerator.GetBytes(16); // ⚠️ HER SEFERİNDE FARKLI!
    var hashBytes = Rfc2898DeriveBytes.Pbkdf2(password, saltBytes, 100000, HashAlgorithmName.SHA256, 32);
    return (Convert.ToBase64String(hashBytes), Convert.ToBase64String(saltBytes));
}
```

**Sorun**: `RandomNumberGenerator.GetBytes(16)` her çağrıldığında **farklı bir salt** üretiyor!

**Mac'te Neden Çalışıyor?**
- Mac'te API restart'ları çok nadir
- İlk başlatmada hash oluşturuluyor
- API restart olmadan login yapılıyor → Aynı hash kullanılıyor ✅

**Windows Server'da Neden Çalışmıyor?**
- Windows Server'da API restart'ları sık (sistem restart, servis restart)
- İlk başlatmada hash oluşturuluyor (Hash1)
- Sistem restart → API yeniden başlıyor → Yeni hash oluşturuluyor (Hash2)
- Hash1 ≠ Hash2 → Login başarısız ❌

### 3. **In-Memory User Storage**

```csharp
private static readonly List<UserModel> _users = new(); // ⚠️ In-memory!
private static bool _initialized = false;
```

**Sorun**: Kullanıcılar **memory'de** tutuluyor, **database'de değil**!

- API restart → Memory temizleniyor → Kullanıcılar kayboluyor
- Yeni başlatmada yeni hash ile kullanıcı oluşturuluyor
- Eski hash ile yeni hash eşleşmiyor!

**Mac'te Neden Çalışıyor?**
- Mac'te API restart'ları nadir
- Memory'deki hash ile login yapılıyor → Çalışıyor ✅

**Windows Server'da Neden Çalışmıyor?**
- Windows Server'da API restart'ları sık
- Restart sonrası yeni hash oluşturuluyor
- Eski hash ile yeni hash eşleşmiyor → Login başarısız ❌

### 4. **Encoding/Line Ending Farklılıkları**

**Mac OS**:
- Unix line endings: `\n`
- UTF-8 encoding (default)
- Normalize işlemi daha az gerekli

**Windows Server 2025**:
- Windows line endings: `\r\n`
- UTF-8 encoding ama farklı locale ayarları olabilir
- Normalize işlemi daha kritik

**Son Güncellemelerle Çözüldü**: 
- Windows line ending normalizasyonu eklendi
- UTF-8 encoding garantisi eklendi
- Control character temizleme eklendi

## 📊 Senaryo Karşılaştırması

### Senaryo 1: İlk Başlatma ve Login

**Mac OS**:
1. API başlatılıyor → Hash1 oluşturuluyor
2. Login yapılıyor → Hash1 ile doğrulanıyor ✅
3. API çalışmaya devam ediyor (restart yok)

**Windows Server**:
1. API başlatılıyor → Hash1 oluşturuluyor
2. Login yapılıyor → Hash1 ile doğrulanıyor ✅
3. Sistem restart → API yeniden başlıyor → Hash2 oluşturuluyor
4. Login yapılıyor → Hash1 ile Hash2 eşleşmiyor ❌

### Senaryo 2: API Restart Sonrası

**Mac OS**:
- API restart nadir
- Restart sonrası hemen login yapılıyor → Yeni hash ile çalışıyor ✅
- Veya restart olmadan çalışmaya devam ediyor

**Windows Server**:
- API restart sık (sistem restart, servis restart)
- Restart sonrası login yapılıyor → Yeni hash ile çalışıyor ✅
- **AMA**: Eğer restart öncesi hash ile restart sonrası hash farklıysa → ❌

## 🔧 Çözüm Önerileri

### 1. **Sabit Salt Kullan (Geçici Çözüm)**

```csharp
// Geçici çözüm: Sabit salt
private static readonly byte[] FIXED_SALT = Convert.FromBase64String("c2FsdF9mb3JfdGVzdGluZw==");

private static (string Hash, string Salt) CreatePasswordHash(string password)
{
    var hashBytes = Rfc2898DeriveBytes.Pbkdf2(password, FIXED_SALT, 100000, HashAlgorithmName.SHA256, 32);
    return (Convert.ToBase64String(hashBytes), Convert.ToBase64String(FIXED_SALT));
}
```

**Avantaj**: Her başlatmada aynı hash oluşturulur
**Dezavantaj**: Güvenlik riski (salt sabit)

### 2. **Database'e Kaydet (Kalıcı Çözüm - ÖNERİLEN)**

Kullanıcıları database'e kaydet:
- Password hash database'de saklanır
- API restart'ta hash değişmez
- Her platformda aynı şekilde çalışır

### 3. **Configuration'dan Oku (Alternatif)**

`appsettings.json`'dan hash'i oku:
- İlk başlatmada hash oluştur ve `appsettings.json`'a kaydet
- Sonraki başlatmalarda `appsettings.json`'dan oku
- Hash değişmez

## 🎯 Sonuç

**Mac OS'da Sorun Yok Çünkü**:
1. API restart'ları nadir
2. Development ortamında sürekli çalışıyor
3. Restart sonrası hemen login yapılıyor (yeni hash ile)

**Windows Server'da Sorun Var Çünkü**:
1. API restart'ları sık (sistem restart, servis restart)
2. Production ortamında servis olarak çalışıyor
3. Restart öncesi hash ile restart sonrası hash farklı

**En İyi Çözüm**: Password hash'i database'e kaydetmek. Bu şekilde her platformda aynı şekilde çalışır ve restart'tan etkilenmez.

