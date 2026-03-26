# ASP.NET Core — AI Agent Kod Kalite Kılavuzu

> Bu belge, ASP.NET Core projeleri geliştiren AI ajanları ve geliştiriciler için hazırlanmış
> bağlayıcı bir standarttır. Kod yazmaya başlamadan önce tamamını oku.
> Buradaki kurallar gerekçesizce ihlal edilemez.

---

## ⚡ ALTIN KURALLAR — Her Şeyden Önce Oku

Bu 4 kural diğer tüm kuralların temelidir. İhlali kabul edilmez.

**1. Yazmadan önce oku.**
Yeni bir şey yazmadan önce projede aynı türden en az 3 dosyayı oku. Proje zaten bir pattern
benimsemişse o pattern'i devam ettir. Kendi kafandan yeni yol açma.

**2. İstenmeyeni yazma.**
Kullanıcı "kaydet butonu ekle" dediyse kaydet butonunu ekle. Formu yeniden tasarlama,
validasyon ekstra ekle, "ileride lazım olur" diye yeni bir servis katmanı icat etme.

**3. Credential koda girmez, girmez, girmez.**
Şifre, API anahtarı, connection string — bunlar ne kaynak kodda ne migration'da ne test
dosyasında bulunur. İstisna yoktur.

**4. Mevcut kodu kırmadan teslim et.**
Her değişiklikten sonra build çalışır, testler geçer. Kırmışsan teslim etmeden önce düzelt.
*Not: "Kırma" = bug sokma demektir, kötü kodu koruma demek değil. Refactoring görevinde
mevcut kötü kodu düzeltmek serbesttir — adım adım, her adımda testler geçmeli.*

---

## 🚨 AGENT TUZAK LİSTESİ — En Sık Yapılan 10 Hata

Detaylar aşağıdaki bölümlerde. Bu liste zihnine ilk kazınan kısaltmadır.

1. **Controller'a iş mantığı yazma** — servis katmanına taşı (§1)
2. **`Console.WriteLine` / `Debug.WriteLine` kullanma** — loglama servisi kullan (§13)
3. **Hardcoded UI metni yazma** — MessageService / localization'dan al (§20)
4. **Hardcoded sayı veya string sabiti yazma** — `const`, enum veya konfigürasyon (§6)
5. **Credential koda yazma** — şifre, token, key hiçbir dosyada olmaz (§9)
6. **`async` metodu `.Result` / `.Wait()` ile çağırma** — deadlock (§17)
7. **Her tabloya soft delete uygulama** — log/event tablolarında fiziksel sil (§18)
8. **Interface olmadan servis yazma** — `IXxxService` zorunlu (§4)
9. **Test yazmadan teslim etme** — her yeni public metod için en az 2 test (§27)
10. **İstenmeyeni yazma** — sadece isteneni yaz, fazlası geri döndürülür (Altın Kural #2)

---

## BÖLÜM 1 — MİMARİ & TASARIM

### 1. Katmanlı Mimari Zorunluluğu

Her proje en az üç katmana sahip olmalıdır. Katmanlar birbirinin içine geçemez.

```
Controller   →  İstek al, yanıt ver. İş mantığı YOK.
Service      →  İş mantığı burada. DB'ye doğrudan erişim YOK.
Repository   →  Sadece veri erişimi. İş mantığı YOK.
```

**Yanlış — Fat Controller:**
```csharp
public async Task<IActionResult> Save(UserDto dto)
{
    // Doğrudan DB erişimi — YASAK
    var existing = await _db.Users.FirstOrDefaultAsync(x => x.Email == dto.Email);
    if (existing != null) return Json(new { success = false, message = "Email zaten kayıtlı." });
    var user = new User { Email = dto.Email, CreatedAt = DateTime.Now };
    _db.Users.Add(user);
    await _db.SaveChangesAsync();
    // E-posta gönderme — YASAK, bu iş mantığı
    await _smtpClient.SendAsync(...);
    return Json(new { success = true });
}
```

**Doğru — İnce Controller:**
```csharp
public async Task<IActionResult> Save(UserDto dto)
{
    var result = await _userService.CreateAsync(dto);
    return Json(new { success = result.Success, message = result.Message });
}
```

---

### 2. Şişman Dosya Yasağı

Bir dosya tek bir sorumluluğu yerine getirir. Aşağıdaki limitler aşıldığında bölünmesi gerekir:

| Dosya Türü | Maksimum Satır | Bölme Yöntemi |
|---|---|---|
| Controller | 300 satır | İlgili action'ları yeni controller'a taşı |
| Service | 400 satır | Sorumlulukları ayrı servislere böl |
| Repository | 250 satır | Spesifik sorgu metodlarını partial class'a taşı |
| View (.cshtml) | 200 satır | Partial view'lar kullan |
| JavaScript | 300 satır | Modüllere böl |

Uzun dosya "daha az dosya" anlamına gelmez. Anlaşılmaz kod anlamına gelir.

---

### 3. SOLID Uyumu

**Single Responsibility:** Bir sınıfın değişmesi için tek bir neden olmalı.
```csharp
// Yanlış: Hem kullanıcı yönetimi hem e-posta gönderimi aynı sınıfta
public class UserManager { SaveUser(); SendWelcomeEmail(); GenerateReport(); }

// Doğru: Her sorumluluk ayrı sınıfta
public class UserService { SaveUser(); }
public class EmailService { SendWelcomeEmail(); }
public class ReportService { GenerateReport(); }
```

**Open/Closed:** Yeni davranış eklemek için mevcut kodu değiştirme, genişlet.
```csharp
// Strateji pattern'i veya yeni implementasyon — mevcut sınıfa dokunmadan
public interface INotificationChannel { Task SendAsync(string message); }
public class EmailNotification : INotificationChannel { ... }
public class SmsNotification : INotificationChannel { ... }
```

**Dependency Inversion:** Somut sınıfa değil, interface'e bağlan.
```csharp
// Yanlış:
private readonly UserRepository _repo; // somut sınıf

// Doğru:
private readonly IUserRepository _repo; // interface
```

---

### 4. Interface Zorunluluğu

Her servis ve repository bir interface'e sahip olmalıdır. DI kaydı interface üzerinden yapılır.

```csharp
// Her zaman çift olarak yaz:
public interface IOrderService { Task<OrderResult> CreateAsync(OrderDto dto); }
public class OrderService : IOrderService { ... }

// DI kaydı:
services.AddScoped<IOrderService, OrderService>();
```

**Neden?**
- Testlerde mock'lanabilir
- İmplementasyon değişince bağımlılar etkilenmez
- Derleyici, interface'i implement etmeyi zorunlu kılar

---

### 5. DI ve Startup Organizasyonu

`Program.cs` yalnızca servis gruplarını çağırır. Detay içermez.

**Yanlış — Şişman Program.cs:**
```csharp
// 200 satırlık Program.cs içinde her şey
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IUserService, UserService>();
// ... 50 satır daha
```

**Doğru — Extension Method Grupları:**
```csharp
// Program.cs — temiz ve okunabilir
builder.Services.AddRepositories();
builder.Services.AddDomainServices();
builder.Services.AddInfrastructureServices(builder.Environment);

// ServiceCollectionExtensions.cs — detaylar burada
public static IServiceCollection AddRepositories(this IServiceCollection services)
{
    services.AddScoped<IUserRepository, UserRepository>();
    services.AddScoped<IOrderRepository, OrderRepository>();
    return services;
}
```

**Lifetime Kuralı:**
| Lifetime | Kullanım |
|---|---|
| `Singleton` | Durum tutmayan, thread-safe altyapı (LogService, Cache) |
| `Scoped` | HTTP isteği başına bir instance (Service, Repository, DbContext) |
| `Transient` | Çok hafif, durumsuz yardımcı sınıflar |

---

## BÖLÜM 2 — KONFİGÜRASYON & DEĞER YÖNETİMİ

### 6. Magic String ve Number Yasağı

Kodun içinde anlamsız sabit değer bulunmaz. Her sabitin bir ismi olur.

```csharp
// Yanlış:
if (user.Role == "Admin") { }
Thread.Sleep(30000);
if (amount > 50000) { }

// Doğru:
if (user.Role == Roles.Admin) { }
Thread.Sleep(TimeSpan.FromSeconds(30).Milliseconds);
if (amount > _settingsProvider.GetDecimal("Limits:MaxAmount")) { }
```

```csharp
// Sabitler ayrı sınıfta tanımlanır:
public static class Roles
{
    public const string Admin = "Admin";
    public const string Operator = "Operator";
    public const string Viewer = "Viewer";
}
```

---

### 7. Konfigürasyon Hiyerarşisi

Her değerin nerede yaşayacağına karar vermek için şu soruyu sor:
*"Bu değer değişirse uygulamayı yeniden başlatmak kabul edilebilir mi?"*

```
┌─────────────────────────────────────────────────────────┐
│  KOD İÇİNDE          Hiçbir zaman değişmeyen sabitler  │
│  (const, enum)        Örnek: HTTP status kodları        │
├─────────────────────────────────────────────────────────┤
│  appsettings.json     Değişince restart gerekir         │
│                       Örnek: DB provider, dosya yolları │
├─────────────────────────────────────────────────────────┤
│  Environment Variable Ortam bazlı, gizli olabilir       │
│                       Örnek: Connection string, API key │
├─────────────────────────────────────────────────────────┤
│  Veritabanı           Değişince restart GEREKMEZ        │
│  (AppSettings tablo)  Örnek: Limitler, zaman aşımları, │
│                       e-posta ayarları, iş kuralları    │
└─────────────────────────────────────────────────────────┘
```

---

### 8. DB-Driven Konfigürasyon

Çalışma zamanında değişebilecek iş kuralları veritabanında tutulur.

```csharp
// AppSettings tablosu — Key/Value/Category yapısı
public class AppSetting
{
    public string Key { get; set; }       // "SMTP_Host", "Limit_MaxAmount"
    public string? Value { get; set; }
    public string Category { get; set; }  // "SMTP", "Limits"
    public string InputType { get; set; } // "text", "number", "toggle"
}

// Kullanım — restart gerektirmez, DB güncellemesi yeterli:
var smtpHost = await _appSettingsRepository.GetValueAsync("SMTP_Host");
var maxAmount = _settingsProvider.GetDecimal("Limit_MaxAmount");
```

Seed veri örüntüsü: Uygulama ilk çalıştığında varsayılan değerleri yoksa ekle.
```csharp
if (!dbContext.AppSettings.Any(x => x.Key == "SMTP_Host"))
    dbContext.AppSettings.Add(new AppSetting { Key = "SMTP_Host", Value = "", Category = "SMTP" });
```

---

## BÖLÜM 3 — GÜVENLİK

### 9. Credential Yönetimi

```
GİT'E GİRMEZ:
  ✗ Şifre (düz metin veya hash)
  ✗ Connection string (kullanıcı adı/şifre içeriyorsa)
  ✗ API anahtarı, token, secret
  ✗ SMTP şifresi
  ✗ Sertifika

GİT'E GİREBİLİR:
  ✓ Connection string şablonu: "Server=YOUR_SERVER;Database=..."
  ✓ Yapısal ayarlar: port numarası, provider adı
  ✓ Varsayılan (boş) seed değerleri
```

`.gitignore` zorunluları:
```
appsettings.Production.json
appsettings.Development.json
*.pfx
*.p12
secrets.json
```

Gerçek değerler `appsettings.Production.json` veya environment variable'da yaşar,
git'e asla girmez.

---

### 10. Şifre Hash Standardı

```csharp
// YASAK — MD5, SHA1, SHA256 ile şifre saklama:
var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(password)));
// "240be518..." → rainbow table'da saniyeler içinde kırılır

// ZORUNLU — PBKDF2 (ASP.NET Identity PasswordHasher):
var hasher = new PasswordHasher<User>();
user.PasswordHash = hasher.HashPassword(user, plainPassword);

// Doğrulama:
var result = hasher.VerifyHashedPassword(user, user.PasswordHash, inputPassword);
if (result == PasswordVerificationResult.Success) { /* giriş başarılı */ }
```

**Migration'da hash bulunmaz.** Admin kullanıcı seed'i uygulama startup'ında runtime'da üretilir:
```csharp
if (!dbContext.Users.Any())
{
    var hasher = new PasswordHasher<User>();
    var admin = new User { Username = "admin", Role = "Admin" };
    admin.PasswordHash = hasher.HashPassword(admin, "admin123");
    dbContext.Users.Add(admin);
    dbContext.SaveChanges();
}
```

---

### 11. CSRF ve Security Headers

**CSRF:** Global `AutoValidateAntiforgeryTokenAttribute` tüm POST/PUT/DELETE endpoint'lerini korur.
Ayrıca her form'a `@Html.AntiForgeryToken()` eklenir.

**Security Headers** — middleware'de tüm response'lara eklenir:
```csharp
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    await next();
});
```

Her controller'da tekrar eklenmez. Bir kez, merkezi olarak.

---

### 12. Kimlik Doğrulama ve Yetkilendirme

- Global `AuthorizeFilter` tüm action'ları varsayılan olarak korur
- `[AllowAnonymous]` sadece giriş sayfasına eklenir, başka yere değil
- Rol bazlı kontrol dinamik filtre veya policy üzerinden yapılır, action içinde `if (user.Role == ...)` ile değil
- Cookie ayarları: `HttpOnly = true`, `Secure = true` (production), `SameSite = Lax`

```csharp
// Yanlış — action içinde rol kontrolü:
public IActionResult Delete(int id)
{
    if (User.Identity.Name != "admin") return Unauthorized(); // YASAK
    ...
}

// Doğru — filtre veya policy:
[Authorize(Roles = "Admin")]
public IActionResult Delete(int id) { ... }
```

---

## BÖLÜM 4 — LOGLAMA & HATA YÖNETİMİ

### 13. Loglama Standardı

```csharp
// YASAK:
Console.WriteLine("Kullanıcı kaydedildi");
Debug.WriteLine("Hata: " + ex.Message);
System.Diagnostics.Trace.WriteLine(...);

// ZORUNLU — LogService (DI ile inject et):
_logService.LogInfo("Kullanıcı oluşturuldu", "Category");
_logService.LogWarning("Geçersiz giriş denemesi", "Auth");
_logService.LogError("Kullanıcı kaydedilemedi", ex, "Users");
```

**Loglama Mimarisi** (DB tabanlı + dosya güvenlik ağı):
- `LogService` → Singleton, `Channel<AppLog>` buffer ile async DB yazma
- Her `Log()` çağrısı: (1) Channel'a yazar (non-blocking), (2) dosyaya yazar (güvenlik ağı)
- Arka plan worker Channel'dan okuyarak `DSG.AppLogs` tablosuna insert eder
- `GetLogs()` artık dosya parse etmez — doğrudan DB'den SQL sorgusu ile okur
- `AppLogs` tablosu: `Id (bigint PK)`, `Timestamp`, `Level (10)`, `Category (100)`, `Message (text)`

**Log Temizleme — Hangfire job:**
- `log-cleanup` job'ı her gece 03:00'da (Turkey Standard Time) çalışır
- Saklama süresi `LOG_RetentionDays` AppSetting'den okunur (varsayılan: 30 gün)
- `JobSettings` tablosundan UI üzerinden cron ifadesi ve aktiflik yönetilebilir

**Serilog** (Program.cs) — framework logları için paralelde dosyaya yazar:
- EF Core, Hangfire, ASP.NET Core framework logları Serilog ile `Logs/{year}/log_{date}.txt`'e düşer
- Uygulama logları `LogService` üzerinden hem DB'ye hem aynı dosyaya yazılır

**Log Seviyesi Disiplini:**
| Seviye | Ne zaman |
|---|---|
| `Debug` | Geliştirme sırasında detay; production'da kapalı |
| `Information` | Önemli iş olayları: kullanıcı girişi, kayıt oluşturma |
| `Warning` | Beklenmedik ama kurtarılabilir durum: retry, fallback |
| `Error` | İşlem başarısız, müdahale gerekebilir |

---

### 14. Global Hata Yönetimi

Tüm işlenmeyen exception'lar middleware'de yakalanır. Her controller'da try-catch olmaz.

```csharp
// Middleware — tek noktada:
public class GlobalExceptionMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try { await next(context); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "İşlenmeyen hata. Path: {Path}", context.Request.Path);
            context.Response.StatusCode = 500;
            await context.Response.WriteAsJsonAsync(new
            {
                success = false,
                message = "Beklenmeyen bir hata oluştu."
            });
        }
    }
}
```

Controller'lardaki try-catch sadece iş mantığı hataları için kullanılır,
altyapı hatalarını yakalamak için değil.

---

### 15. Catch Bloğu Standardı

```csharp
// YASAK — boş catch:
catch (Exception) { }

// YASAK — exception yutma:
catch (Exception ex) { return false; } // log yok

// YASAK — sadece rethrow:
catch (Exception ex) { throw; } // try-catch'in anlamı yok

// YASAK — hardcoded mesaj:
catch (Exception ex)
{
    _logger.LogError(ex, "Hata");
    return Json(new { success = false, message = "Bir hata oluştu." }); // hardcoded
}

// DOĞRU:
catch (Exception ex)
{
    _logger.LogError(ex, "Kullanıcı güncellenirken hata. Id: {Id}", id);
    return Json(new { success = false, message = _messageService.GetError("UpdateFailed") });
}
```

Her catch bloğu şunları yapar:
1. Bağlamsal bilgiyle log yazar (hangi kayıt, hangi işlem)
2. Localization'dan hata mesajı döner
3. Gerekirse exception'ı yeniden fırlatır (middleware yakalar)

---

## BÖLÜM 5 — VERİ ERİŞİM KATMANI

### 16. Repository Pattern Zorunluluğu

Controller veya Service, `DbContext`'e doğrudan erişemez.

```csharp
// YASAK — Service içinde doğrudan DbContext:
public class OrderService
{
    private readonly AppDbContext _db; // YASAK
    public async Task<List<Order>> GetActiveOrders()
        => await _db.Orders.Where(x => x.IsActive).ToListAsync();
}

// DOĞRU — Repository aracılığıyla:
public class OrderService
{
    private readonly IOrderRepository _orderRepo;
    public async Task<List<Order>> GetActiveOrders()
        => await _orderRepo.GetActiveAsync();
}
```

**Generic Base + Spesifik Repository:**
```csharp
// Temel operasyonlar generic'te:
public interface IRepository<T>
{
    Task<T?> GetByIdAsync(int id);
    Task<List<T>> GetAllAsync();
    Task AddAsync(T entity);
    Task UpdateAsync(T entity);
    Task DeleteAsync(int id);
}

// Entity'ye özel sorgular spesifik interface'de:
public interface IOrderRepository : IRepository<Order>
{
    Task<List<Order>> GetActiveByCustomerAsync(int customerId);
    Task<Order?> GetWithItemsAsync(int orderId);
}
```

---

### 17. Async/Await Disiplini

```csharp
// YASAK — senkron DB çağrısı:
var users = _db.Users.ToList();        // thread'i bloklar
var user = _db.Users.Find(id);         // bloklar

// YASAK — async'i senkrona döndürme:
var result = _service.GetAsync().Result;    // deadlock riski
var result = _service.GetAsync().GetAwaiter().GetResult(); // aynı risk

// YASAK — async void (hata yakalanamaz):
public async void ProcessOrder() { ... }

// DOĞRU:
var users = await _db.Users.ToListAsync();
var user = await _db.Users.FindAsync(id);
public async Task ProcessOrderAsync() { ... }
```

Kural basit: DB'ye dokunan her metod `Async` suffix'i taşır ve `await` kullanır.

---

### 18. Soft Delete — Ne Zaman, Ne Zaman Değil

Soft delete her tabloya uygulanmaz. Yanlış uygulanırsa tablo şişer, sorgular yavaşlar,
index verimsizleşir. Önce karar ver, sonra uygula.

**Soft delete KULLAN:**
```
✓ Master data (User, Product, Customer) — referans bütünlüğü bozulmaz
✓ Geri alma ihtiyacı olan işlemler — kullanıcı sildiğini geri alabilmeli
✓ Yasal/kurumsal denetim zorunluluğu — "kim ne zaman sildi" izlenebilmeli
✓ Diğer tablolardan referans alan kayıtlar — FK ilişkisi var
```

**Soft delete KULLANMA — fiziksel sil:**
```
✗ Log / event tabloları — zaten append-only, milyonlarca satır
✗ Geçici / session verileri — zaten kısa ömürlü
✗ Yüksek hacimli işlem kayıtları — tablo şişer, index bozulur
✗ Hangfire, audit trail gibi framework tabloları
```

**Soft delete uygunsa — doğru uygulama:**

Status değerleri: `1 = Aktif`, `2 = Pasif/Askıda`, `3 = Silinmiş`

```csharp
public class User
{
    public int Id { get; set; }
    public short Status { get; set; } = 1;
}

// Silme:
user.Status = 3;
await _db.SaveChangesAsync();
```

**Filtre unutulmasın diye `HasQueryFilter` — DbContext'te bir kez tanımla:**
```csharp
// DbContext.OnModelCreating:
modelBuilder.Entity<User>().HasQueryFilter(x => x.Status != 3);

// Artık her sorguda otomatik — geliştirici filtreyi unutamaz:
var users = await _db.Users.ToListAsync(); // otomatik Status != 3

// Gerektiğinde bypass:
var allUsers = await _db.Users.IgnoreQueryFilters().ToListAsync();
```

**Unique constraint sadece aktif kayıtlara:**
```csharp
b.HasIndex(x => x.Email)
    .IsUnique()
    .HasFilter("\"Status\" != 3"); // silinmiş kayıtlar unique kısıtına dahil değil
```

**Büyük tablolar için alternatif — Archive pattern:**
```
Silme yerine: kaydı {Tablo}Archive tablosuna taşı, orijinal tablodan sil.
Avantaj: Ana tablo şişmez. Arşiv tablosu nadiren sorgulanır.
```

---

### 19. Veritabanı Migration Standardı

```
✓ Migration başına tek sorumluluk — bir migration, bir değişiklik
✓ Migration isimleri açıklayıcı: "AddUserIsLdapColumn", "CreateOrdersTable"
✓ Seed veri idempotent: "yoksa ekle" mantığı
✗ Migration içinde credential bulunmaz (hash dahil)
✗ Büyük veri dönüşümleri migration içinde yapılmaz — ayrı script
✗ Migration elle düzenlenmez — EF Core generate eder
```

İdempotent schema patch örüntüsü (migration dışında kolon eklendiyse):
```sql
-- PostgreSQL:
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                   WHERE table_name = 'Users' AND column_name = 'IsLdapUser') THEN
        ALTER TABLE "Users" ADD COLUMN "IsLdapUser" boolean NOT NULL DEFAULT false;
    END IF;
END $$;
```

---

## BÖLÜM 6 — FRONTEND

### 20. UI Metin Yönetimi

Kullanıcıya gösterilen hiçbir metin kaynak koda gömülmez.

```csharp
// YASAK — hardcoded metin:
return Json(new { message = "Kayıt başarıyla silindi." });
ViewBag.Error = "Bu alan zorunludur.";

// DOĞRU — localization servisi:
return Json(new { message = _messageService.Get("Common", "DeleteSuccess") });
ViewBag.Error = _messageService.Get("Validation", "FieldRequired");
```

```html
<!-- YASAK — cshtml'de hardcoded metin: -->
<button>Kaydet</button>
<span>Yükleniyor...</span>

<!-- DOĞRU — mesaj servisi veya PageContext: -->
<button>@_messageService.Get("Common", "Save")</button>
<span id="loadingText" style="display:none"></span>
<script>
    // Sunucudan JS'e veri geçişi — tek kabul edilen inline script türü:
    window.PageMessages = {
        loading: '@_messageService.Get("Common", "Loading")',
        deleteConfirm: '@_messageService.Get("Common", "DeleteConfirm")'
    };
</script>
```

---

### 21. Inline CSS ve JS Yasağı

```html
<!-- YASAK — inline style: -->
<div style="margin-top: 20px; color: red; font-weight: bold;">

<!-- YASAK — inline script (iş mantığı): -->
<script>
    $('#saveBtn').click(function() {
        $.ajax({ url: '/save', ... });
    });
</script>

<!-- DOĞRU — harici dosyalar: -->
@section Styles {
    <link rel="stylesheet" href="~/css/pages/Order/Index.css" />
}
@section Scripts {
    <script src="~/js/pages/Order/Index.js"></script>
}
```

**Dosya organizasyonu:**
```
wwwroot/
├── css/
│   └── pages/
│       └── {Controller}/
│           └── Index.css     ← sayfa bazlı stil
└── js/
    └── pages/
        └── {Controller}/
            └── Index.js      ← sayfa bazlı script
```

---

### 22. AJAX-First Prensibi

Kullanıcı bir işlem yaptığında sayfa yenilenmez. Sadece değişen kısım güncellenir.

```javascript
// YASAK:
location.reload();
window.location.href = '/Order/Index';
form.submit(); // tam sayfa POST

// DOĞRU:
$.ajax({
    url: '/Order/Save',
    type: 'POST',
    data: formData,
    success: function(response) {
        if (response.success) {
            window.showSuccessToast(response.message);
            dataTable.ajax.reload(); // sadece tablo güncellenir
        } else {
            window.showErrorToast(response.message);
        }
    },
    error: function() {
        window.showErrorToast(window.GlobalMessages.ajax.genericError);
    }
});
```

**Controller dönüş formatı — her zaman aynı:**
```csharp
return Json(new { success = true, message = "...", data = ... });
return Json(new { success = false, message = "..." });
```

Sayfa yenilenebilir istisnalar: Login/Logout, menü navigasyonu, dil değişimi.

---

### 23. UI Component Standardı

Bir projede aynı türden component tek şekilde görünür. Her sayfada farklı bir bildirim,
farklı bir onay kutusu olamaz.

```javascript
// Proje genelinde TEK bildirim sistemi:
window.showSuccessToast(message);  // yeşil, sağ üst
window.showErrorToast(message);    // kırmızı, sağ üst
window.showWarningToast(message);  // sarı, sağ üst

// Proje genelinde TEK onay dialog'u:
window.showConfirmDialog(message, onConfirm, onCancel);

// Proje genelinde TEK modal açma:
window.openModal(url, title);
window.closeModal();
```

Yeni bir sayfa için başka bir kütüphane, başka bir toast, başka bir modal icat edilmez.
Projenin benimsediği component sistemi tüm sayfalarda tutarlı kullanılır.

---

### 24. Form Validasyon Standardı

Client-side ve server-side validasyon her ikisi zorunludur. Biri diğerinin yerine geçmez.

```csharp
// Server-side — her zaman:
public async Task<IActionResult> Save(OrderDto dto)
{
    if (!ModelState.IsValid)
        return Json(new { success = false, message = _messageService.Get("Validation", "InvalidForm") });
    ...
}
```

```javascript
// Client-side — gönderim öncesi:
$('#orderForm').on('submit', function(e) {
    e.preventDefault();
    if (!validateForm()) return; // client kontrolü
    // ajax gönder
});
```

Validasyon mesajları localization'dan gelir, hardcoded olmaz.

---

## BÖLÜM 7 — API TASARIMI

### 25. Response Format Tutarlılığı

Tüm endpoint'ler aynı wrapper yapısını döner. İstemci her zaman aynı yapıyı parse eder.

```csharp
// Standart başarı yanıtı:
return Json(new { success = true, message = "Kayıt oluşturuldu.", data = dto });

// Standart hata yanıtı:
return Json(new { success = false, message = "Geçersiz veri.", errors = validationErrors });

// Liste yanıtı (DataTables uyumlu):
return Json(new { draw, recordsTotal, recordsFiltered, data = list });
```

Her endpoint bu yapıdan sapmaz. `{ result: ... }`, `{ ok: true }`, `{ status: "error" }` gibi
farklı formatlar kullanılmaz.

---

### 26. HTTP Status Code Disiplini

```csharp
// YANLIŞ — her şeye 200:
return StatusCode(200, new { error = "Bulunamadı." });

// DOĞRU:
return Ok(data);                  // 200 — başarılı GET
return Created(url, data);        // 201 — başarılı POST (kaynak oluşturuldu)
return NoContent();               // 204 — başarılı DELETE
return BadRequest(errors);        // 400 — geçersiz istek
return Unauthorized();            // 401 — kimlik doğrulanmadı
return Forbid();                  // 403 — yetkisiz erişim
return NotFound();                // 404 — kaynak bulunamadı
return StatusCode(500, message);  // 500 — sunucu hatası
```

**MVC + AJAX-First pattern'de** `Json()` ile `{ success: bool }` body flag yeterlidir — client bu flag'i kontrol eder:
```csharp
return Json(new { success = false, message = "Geçersiz veri." }); // 200 döner, client success flag'ine bakar
```

**REST API endpoint'lerinde** HTTP status code semantiği kullanılır:
```csharp
return BadRequest(new { message = "..." });  // 400
return NotFound();                           // 404
```

**YASAK — `Json()` ile `Response.StatusCode` karıştırma:**
```csharp
Response.StatusCode = 400;
return Json(new { success = false }); // tutarsız — ya biri ya diğeri
```

---

## BÖLÜM 8 — TEST

### 27. Test Kapsama Zorunluluğu

Kod yazılıyorsa test de yazılır. Opsiyonel değildir.

```
Her yeni Controller  →  Tests/Controllers/{Controller}Tests.cs
Her yeni Service     →  Tests/Services/{Service}Tests.cs
Her yeni Repository  →  Tests/Repositories/{Repository}Tests.cs (karmaşık sorgular için)
```

**Minimum kapsam — her public metod için:**
- Happy path: Normal girdi, beklenen sonuç
- Hata senaryosu: Geçersiz girdi, sınır koşulu, exception durumu

```csharp
// Happy path:
[Fact]
public async Task CreateOrder_ValidInput_ReturnsSuccess() { }

// Hata senaryosu:
[Fact]
public async Task CreateOrder_DuplicateOrderNumber_ReturnsError() { }
[Fact]
public async Task CreateOrder_CustomerNotFound_ThrowsNotFoundException() { }
```

---

### 28. Test İzolasyon Standardı

Her test bağımsız çalışır. Test sırası sonucu etkilemez.

```csharp
public class OrderServiceTests : IDisposable
{
    private readonly InMemoryDbContext _db;
    private readonly OrderService _service;

    public OrderServiceTests()
    {
        // Her test için izole DB — Guid ile unique isim:
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new InMemoryDbContext(options);

        // Mock'lar:
        var loggerMock = new Mock<ILogger<OrderService>>();
        _service = new OrderService(new OrderRepository(_db), loggerMock.Object);
    }

    public void Dispose()
    {
        _db.Dispose();
        // Temp dosyalar varsa temizle
    }
}
```

**Shared state yasağı:** Static değişken, paylaşılan dosya, ortak DB — bunlar testler arası
durum sızdırır ve rastgele başarısızlıklara yol açar.

---

### 29. Test İsimlendirme Standardı

```
[Metod]_[Senaryo]_[BeklenenSonuç]
```

```csharp
// Doğru örnekler:
CreateOrder_ValidInput_ReturnsSuccessResult
CreateOrder_EmptyCustomerId_ThrowsArgumentException
GetById_NonExistentId_ReturnsNull
Login_WrongPassword_ReturnsFailureWithMessage
SyncCsv_FileNotFound_ReturnsFalseAndLogsError
GetActiveUsers_DatabaseEmpty_ReturnsEmptyList

// Yanlış örnekler:
TestCreate()            // ne test ediliyor?
OrderTest1()            // anlamsız
CreateOrderTest()       // senaryo ve sonuç yok
```

---

## BÖLÜM 9 — PERFORMANS

### 30. N+1 Sorgu Yasağı

```csharp
// YASAK — N+1: Her order için ayrı DB sorgusu:
var orders = await _db.Orders.ToListAsync();
foreach (var order in orders)
{
    var customer = await _db.Customers.FindAsync(order.CustomerId); // N sorgu!
    order.CustomerName = customer.Name;
}

// DOĞRU — tek sorguda Join:
var orders = await _db.Orders
    .Include(x => x.Customer)
    .ToListAsync();
```

```csharp
// YASAK — tüm veriyi çekip bellekte filtrele:
var allUsers = await _db.Users.ToListAsync();
var admins = allUsers.Where(x => x.Role == "Admin").ToList(); // tüm tablo belleğe

// DOĞRU — DB'de filtrele:
var admins = await _db.Users.Where(x => x.Role == "Admin").ToListAsync();
```

---

### 31. Sayfalama Zorunluluğu

Liste dönen her endpoint sayfalanır. Sınırsız liste döndürme yasaktır.

```csharp
// YASAK:
var allOrders = await _orderRepo.GetAllAsync(); // 100k kayıt?

// DOĞRU:
var pagedOrders = await _orderRepo.GetPagedAsync(pageNumber: 1, pageSize: 50);

// DataTables için server-side paging:
public async Task<IActionResult> GetData(int draw, int start, int length)
{
    var result = await _orderRepo.GetPagedAsync(start / length + 1, length);
    return Json(new { draw, recordsTotal = result.Total, data = result.Items });
}
```

---

## BÖLÜM 10 — PAKET & BAĞIMLILIK YÖNETİMİ

### 32. Package Disiplini

```
EKLEMEDEN ÖNCE SOR:
  - Bu paketi projenin geri kalanı da kullanabilecek mi?
  - Standart kütüphane (System.*, Microsoft.*) ile yapılamaz mı?
  - Paketin aktif bakımı var mı? Son commit ne zaman?
  - Lisansı projeye uygun mu?

YASAK:
  ✗ Tek bir metod için büyük paket eklemek
  ✗ Versiyon sabitlemeden eklemek (floating version)
  ✗ Birden fazla paketi aynı iş için kullanmak (2 farklı JSON kütüphanesi)
```

```xml
<!-- Versiyon her zaman sabitlenir: -->
<PackageReference Include="CsvHelper" Version="33.0.1" />

<!-- YASAK — floating version: -->
<PackageReference Include="CsvHelper" Version="*" />
```

---

### 33. YAGNI Prensibi

*"You Aren't Gonna Need It"* — İhtiyacın olmayan şeyi yazma.

```csharp
// YASAK — "ileride lazım olur" soyutlaması:
public interface IOrderProcessorFactory<T> where T : IOrderProcessor
{
    T Create(OrderProcessorOptions options);
}
// Şu an tek bir sipariş işleyicisi var. Factory'ye gerek yok.

// DOĞRU — şu anki ihtiyacı karşıla:
public class OrderProcessor
{
    public async Task ProcessAsync(Order order) { ... }
}
```

Refactor zamanı geldiğinde yapılır. Henüz gelmemişken yapılmaz.
Benzer 3 satır kod, erken soyutlamadan iyidir.

---

## BÖLÜM 11 — KOD STİLİ

### 34. Kod ve Yorum Dili Kuralı

```csharp
// Sınıf, metod, property, değişken → İngilizce
public class UserService { }
public async Task<User?> GetByUsernameAsync(string username) { }
var activeUserCount = 0;

// Yorumlar → Türkçe (veya projenin benimsediği dil — tutarlı ol)
// Kullanıcı adına göre aktif kullanıcıyı veritabanından çeker
var user = await _repo.FindByUsernameAsync(username);

// DB tablo ve kolon adları → İngilizce
// Route ve URL'ler → İngilizce: /api/users/123
```

Proje boyunca tutarlı ol. Bazı dosyalar Türkçe yorum, bazıları İngilizce olamaz.

---

### 35. Yorum Disiplini

```csharp
// GEREKSIZ YORUM — kod zaten anlatıyor:
// Kullanıcıyı id ile bul
var user = await _repo.GetByIdAsync(id);

// GEREKSIZ YORUM — magic comment:
// TODO: Düzelt
// FIXME: Çalışmıyor
// Bunlar PR'a girmez. Düzelt veya issue aç.

// DEĞERLI YORUM — "neden" açıklar:
// PBKDF2 kullanıyoruz; SHA256 rainbow table'da mevcut olduğu için kullanılmaz.
var hasher = new PasswordHasher<User>();

// DEĞERLI YORUM — karmaşık mantık:
// Üst sınır dahil, alt sınır hariç — matematiksel aralık [min, max)
return value >= min && value < max;
```

---

### 36. İsimlendirme Tutarlılığı

```csharp
// Async metod suffix'i her zaman:
Task GetUsersAsync()     ✓
Task GetUsers()          ✗

// Boolean property/değişken önek:
bool isActive            ✓
bool hasPermission       ✓
bool active              ✗

// Repository metod isimleri:
GetByIdAsync()           ✓  // tekil kayıt
GetAllAsync()            ✓  // liste
GetPagedAsync()          ✓  // sayfalı liste
FindByEmailAsync()       ✓  // özel sorgu
FetchData()              ✗  // belirsiz

// Interface ön eki her zaman I:
IUserRepository          ✓
UserRepositoryInterface  ✗
```

---

## BÖLÜM 12 — CI/CD & GİT

### 37. Pipeline Kalite Kapısı

Her push veya PR'da otomatik çalışan pipeline zorunludur.

```yaml
# .github/workflows/ci.yml — minimum:
name: CI
on:
  push:
    branches: [ main ]
  pull_request:
    branches: [ main ]
jobs:
  build-and-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'
      - run: dotnet restore
      - run: dotnet build --configuration Release --no-restore
      - run: dotnet test --configuration Release --no-build
```

**Kalite kapıları:**
- Build başarısız → merge edilemez
- Test başarısız → merge edilemez
- Pipeline'da credential bulunmaz

---

### 38. Git Hijyeni

```
✓ GİT'E GİRER:
  Kaynak kod, test dosyaları, migration'lar
  appsettings.json (şablon, gerçek değer olmadan)
  .gitignore, README.md, CLAUDE.md, REFERENCE.md
  CI/CD workflow dosyaları

✗ GİT'E GİRMEZ:
  appsettings.Production.json
  appsettings.Development.json (gerçek değer varsa)
  *.pfx, *.p12, *.key (sertifikalar)
  bin/, obj/ (derleme çıktıları)
  .vs/, .idea/ (IDE dosyaları)
  Şifre, token, API anahtarı içeren her şey
```

**Commit mesajı standardı:** (Türkçe karakter kullanma — encoding bozuluyor; İngilizce yaz)
```
feat: Add user role management
fix: Null reference in UserService.GetByIdAsync
refactor: Move OrderController logic to service layer
test: Add CsvDataCatalogSyncService tests
```

**AI aracı prefix'i:** Her commit'in başına kimin yaptığını belirt. GitHub commit listesinde görünür, arama ile filtrelenebilir.
```
[Claude]  fix: SensitivityData dropdown z-index
[Cursor]  feat: Dashboard stat cards thousand separator
[Human]   refactor: CsvDataCatalogSyncService cleanup
```
GitHub'da filtrelemek için: `repo:owner/repo [Claude]` şeklinde arama yap.

**Versiyonlama & Tagleme:**

SemVer (`MAJOR.MINOR.PATCH`) kullanılır. Her anlamlı push'ta tag atılır.

```
MAJOR  — Geriye dönük uyumsuz değişiklik
         Örnekler: DB şemasında elle müdahale gerektiren değişiklik,
                   public API endpoint silindi/imzası değişti,
                   authentication/authorization modeli değişti

MINOR  — Geriye dönük uyumlu yeni özellik
         Örnekler: yeni sayfa/endpoint eklendi, yeni Hangfire job,
                   yeni UI modali, yeni servis/entegrasyon

PATCH  — Hata düzeltme, küçük iyileştirme
         Örnekler: mapping hatası düzeltmesi, UI genişlik ayarı,
                   seed fix, timeout ayarı, log mesajı düzeltmesi
```

Tag atma adımları:
```bash
# 1. Değişiklikleri push et
git push origin main

# 2. Tag oluştur ve push et
git tag -a v1.2.3 -m "[Claude] feat: owner auto-assign + replica propagation"
git push origin v1.2.3
```

Kurallar:
```
✓ Tag mesajı commit mesajı standardını izler ([Claude]/[Human] prefix)
✓ Her MAJOR tag öncesi REFERENCE.md güncellenir
✓ Aynı commit'e birden fazla tag atılmaz
✗ main'e force push ile geçmiş tag'ler ezilmez
✗ Pre-release (v1.0.0-beta) deploy edilmez — sadece CI/CD içinde kullanılabilir
```

Mevcut versiyon takibi için `git tag --sort=-v:refname | head -5` komutu kullanılır.

---

## BÖLÜM 13 — ALTYAPI & GÖZLEMLENEBILIRLIK

### 39. Önbellekleme (Caching)

Önbellek yanlış uygulanırsa stale data, memory sızıntısı veya kullanıcılar arası veri sızıntısı oluşur.
Önbellek her zaman **servis katmanında** yönetilir, controller'da değil.

**Ne zaman kullan:**
```
✓ Sık okunan, nadiren değişen veri (lookup tabloları, konfigürasyon, AI analiz sonuçları)
✗ Kullanıcıya özgü veri — yanlış cache anahtarıyla başka kullanıcının verisi döner
✗ Sık değişen işlem verisi — cache hit oranı düşük, invalidation yükü artar
```

**IMemoryCache (tek sunucu) — doğru kullanım:**
```csharp
public class ProductService
{
    private readonly IMemoryCache _cache;
    private const string CacheKey = "products_all";

    public async Task<List<Product>> GetAllAsync()
    {
        if (_cache.TryGetValue(CacheKey, out List<Product>? cached))
            return cached!;

        var products = await _repo.GetAllAsync();
        _cache.Set(CacheKey, products, TimeSpan.FromMinutes(10)); // TTL zorunlu
        return products;
    }

    // Veri değişince invalidate et:
    public async Task UpdateAsync(Product product)
    {
        await _repo.UpdateAsync(product);
        _cache.Remove(CacheKey); // stale data önlenir
    }
}
```

**Yasak:**
```csharp
_cache.Set(key, data); // TTL yok — bellek sızıntısı, YASAK

// Controller'da cache yönetimi — YASAK:
public async Task<IActionResult> Index()
{
    if (_cache.TryGetValue("products", out var data)) return Json(data);
    ...
}
```

**IDistributedCache (Redis) — çoklu sunucu veya restart'ta veri korunması gerektiğinde:**
```csharp
services.AddStackExchangeRedisCache(options =>
    options.Configuration = builder.Configuration["Redis:ConnectionString"]);
```

---

### 40. Rate Limiting

Public endpoint'ler kısıtlanmadan bırakılırsa brute force, scraping ve maliyet kontrolsüzlüğü
sorunları ortaya çıkar. ASP.NET Core 7+ built-in rate limiting — harici paket gerekmez.

```csharp
// Program.cs:
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("api", limiter =>
    {
        limiter.PermitLimit = 100;              // 100 istek
        limiter.Window = TimeSpan.FromMinutes(1); // dakikada
        limiter.QueueLimit = 0;
    });
    options.AddFixedWindowLimiter("login", limiter =>
    {
        limiter.PermitLimit = 5;                // 5 deneme
        limiter.Window = TimeSpan.FromMinutes(5);
    });
    options.RejectionStatusCode = 429; // Too Many Requests
});
app.UseRateLimiter();
```

```csharp
// Controller veya action bazlı:
[EnableRateLimiting("login")]
public async Task<IActionResult> Login(LoginDto dto) { ... }

[EnableRateLimiting("api")]
public class ApiController : Controller { ... }
```

**Ne zaman zorunlu:**
```
✓ Giriş / şifre sıfırlama endpoint'leri — brute force'a karşı
✓ Public API endpoint'leri — scraping ve kötüye kullanım
✓ LLM/AI entegrasyonu olan endpoint'ler — maliyet kontrolü
```

---

### 41. Docker & Container Standardı

**YASAK — tek aşamalı build (SDK production'a giriyor):**
```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0
COPY . .
RUN dotnet publish -c Release -o /app
ENTRYPOINT ["dotnet", "/app/WebApp.dll"]
# SDK image'ı ~900MB — production'da gereksiz
```

**DOĞRU — multi-stage build:**
```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY *.sln .
COPY WebDSManagement.Web/*.csproj WebDSManagement.Web/
RUN dotnet restore
COPY . .
RUN dotnet publish WebDSManagement.Web -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
HEALTHCHECK --interval=30s --timeout=10s --start-period=30s \
    CMD curl -f http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "WebDSManagement.Web.dll"]
# Runtime image ~220MB — SDK içermez
```

**Health check endpoint zorunlu:**
```csharp
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>();
app.MapHealthChecks("/health");
```

**Credential container'a iki yoldan girer — ikisi de yasak:**
```
✗ Dockerfile içinde ENV DB_PASS=supersecret
✗ docker-compose.yml içinde hardcoded değer
✓ .env dosyası → git'e girmez (.gitignore'da)
✓ Secrets manager (Docker Secrets, Vault, Azure Key Vault)
```

```yaml
# docker-compose.yml — değerler .env dosyasından:
services:
  app:
    image: webdsmanagement:latest
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ConnectionStrings__DefaultConnection=${DB_CONNECTION}
    ports:
      - "8080:8080"
```

---

### 42. Gözlemlenebilirlik (Observability)

Loglama tek başına yetmez. Latency, error rate ve dependency health izlenmezse sorunlar
production'da geç fark edilir. Üç katman birbirini tamamlar:

```
Log    → Serilog (§13) — "ne oldu?"
Trace  → OpenTelemetry — "nerede, ne kadar sürdü, hangi servis çağrıldı?"
Metric → OpenTelemetry — "error rate, p95 latency, aktif request sayısı"
```

**OpenTelemetry minimum kurulum:**
```csharp
// Paketler: OpenTelemetry.Extensions.Hosting
//           OpenTelemetry.Instrumentation.AspNetCore
//           OpenTelemetry.Instrumentation.EntityFrameworkCore
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddConsoleExporter())   // geliştirme; production'da Jaeger/Tempo/Grafana
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddConsoleExporter());
```

**Proje büyüklüğüne göre minimum seviye:**
```
Küçük proje:  Serilog + Health Check endpoint (§41) — yeterli
Orta proje:   + OpenTelemetry traces — hata analizi kolaylaşır
Büyük proje:  + Metrics exporter (Prometheus/Grafana) — kapasite planlaması
```

Application Insights, Datadog, Grafana Cloud — hepsi OpenTelemetry uyumludur.
Vendor bağımlılığı önlemek için kodda OpenTelemetry API kullan, exporter'ı konfigürasyondan değiştir.

---

## KAPANIŞ — Agent İçin Kontrol Listesi

Kodu teslim etmeden önce şu soruları yanıtla:

- [ ] Build çalışıyor mu? (`dotnet build` — 0 error)
- [ ] Tüm testler geçiyor mu? (`dotnet test` — 0 failure)
- [ ] Hardcoded metin var mı? (string literal arayın)
- [ ] Hardcoded değer/sabit var mı? (magic number/string)
- [ ] Console.WriteLine veya Debug.WriteLine var mı?
- [ ] Credential, şifre, hash, token var mı?
- [ ] Controller'da iş mantığı var mı?
- [ ] Async metod senkron çağrılıyor mu? (.Result, .Wait())
- [ ] DB'ye doğrudan erişim (DbContext) controller'da var mı?
- [ ] Yeni dosyaya karşılık test dosyası oluşturuldu mu?
- [ ] Mevcut pattern'dan sapma var mı? (Neden? Gerekçesi var mı?)
- [ ] İstenmeyenden fazla bir şey yazıldı mı?
- [ ] Cache kullanan metod TTL tanımlamış mı? (`Set(key, value, TimeSpan...)`)
- [ ] Giriş / public endpoint'lerde rate limiting var mı?

Tüm yanıtlar temizse teslim edilebilir.
