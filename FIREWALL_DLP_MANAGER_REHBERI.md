# Firewall Kuralı - DLP Manager Bağlantısı

## 🎯 Soru: Firewall Kuralı Nasıl Olmalı?

**Cevap:** OUTBOUND (Giden) kuralı tanımlamalısınız. Kaynak port önemli değil, **hedef port 8443** olmalı.

---

## 📊 İstek Akışı

```
┌─────────────────────┐         OUTBOUND          ┌─────────────────────┐
│  Analyzer API       │ ────────────────────────> │  DLP Manager        │
│  (Kendi Bilgisayar) │    Port: Dinamik          │  (192.168.1.100)    │
│  localhost:5001     │    → 8443 (Hedef)         │  Port: 8443         │
└─────────────────────┘                            └─────────────────────┘
```

### Önemli Noktalar:

1. **Port 5001:** Analyzer API'nin **dinlediği** port (Swagger için)
   - Bu port, Analyzer API'ye gelen istekler için kullanılır
   - Firewall kuralında **kaynak port** olarak kullanılmaz

2. **Port 8443:** DLP Manager'ın **dinlediği** port
   - Bu port, Analyzer API'nin DLP Manager'a bağlanmak için kullandığı **hedef port**
   - Firewall kuralında **hedef port** olarak kullanılır

3. **Dinamik Port:** Analyzer API, DLP Manager'a bağlanırken Windows tarafından otomatik atanan bir port kullanır (genelde 50000-65535 arası)
   - Bu port önemli değil, firewall kuralında belirtmenize gerek yok

---

## 🔥 Windows Firewall Kuralı

### Senaryo:
- **Kendi Bilgisayarınız:** Analyzer API çalışıyor (localhost:5001)
- **DLP Manager:** 192.168.1.100:8443

### OUTBOUND (Giden) Kuralı

**PowerShell ile (Yönetici olarak):**

```powershell
# OUTBOUND kuralı: Kendi bilgisayarından → DLP Manager'ın 8443 portuna
New-NetFirewallRule `
    -DisplayName "DLP Manager Outbound (8443)" `
    -Direction Outbound `
    -RemoteAddress 192.168.1.100 `
    -RemotePort 8443 `
    -Protocol TCP `
    -Action Allow
```

**Veya tüm IP'lere izin vermek için (DLP Manager IP'si değişebilirse):**

```powershell
# OUTBOUND kuralı: Kendi bilgisayarından → Herhangi bir IP'nin 8443 portuna
New-NetFirewallRule `
    -DisplayName "DLP Manager Outbound (8443)" `
    -Direction Outbound `
    -RemotePort 8443 `
    -Protocol TCP `
    -Action Allow
```

### Manuel Firewall Yapılandırması

1. **Windows Defender Firewall**'ı açın
2. **Advanced settings** → **Outbound Rules** → **New Rule**
3. **Port** seçin → **Next**
4. **TCP** seçin → **Specific remote ports**: `8443` → **Next**
5. **Allow the connection** → **Next**
6. Tüm profilleri seçin (Domain, Private, Public) → **Next**
7. **Name**: "DLP Manager Outbound (8443)" → **Finish**

### İsteğe Bağlı: Remote Address Belirtme

Eğer sadece belirli bir DLP Manager IP'sine izin vermek istiyorsanız:

1. Kural oluşturulduktan sonra → **Properties**
2. **Scope** sekmesi → **Remote IP address**
3. **These IP addresses** → **Add** → DLP Manager IP'sini girin (örn: 192.168.1.100)
4. **OK**

---

## ❌ YANLIŞ: Port 5001'den 8443'e

**YANLIŞ Anlama:**
> "Kendi bilgisayarımın 5001 portundan managerın 8443 portuna erişim tanımlatmalıyım"

**Neden Yanlış:**
- Port 5001, Analyzer API'nin **dinlediği** port (gelen istekler için)
- Firewall kuralında **kaynak port** olarak kullanılmaz
- Windows, DLP Manager'a bağlanırken **dinamik bir port** kullanır (50000-65535 arası)

**Doğru Yaklaşım:**
- **Kaynak port:** Any (herhangi bir port, dinamik)
- **Hedef port:** 8443 (DLP Manager'ın portu)
- **Yön:** Outbound (Giden)

---

## ✅ DOĞRU: Outbound Kuralı

**DOĞRU Anlama:**
> "Kendi bilgisayarımdan DLP Manager'ın 8443 portuna OUTBOUND erişim tanımlatmalıyım"

**Firewall Kuralı:**
- **Yön:** Outbound (Giden)
- **Kaynak Port:** Any (herhangi bir port)
- **Hedef Port:** 8443
- **Hedef IP:** DLP Manager IP (192.168.1.100) veya Any
- **Protokol:** TCP
- **Aksiyon:** Allow

---

## 🧪 Test

### 1. Firewall Kuralını Test Etme

**PowerShell ile (Yönetici olarak):**

```powershell
# DLP Manager'a bağlantı testi
Test-NetConnection -ComputerName 192.168.1.100 -Port 8443
```

**Başarılı Çıktı:**
```
ComputerName     : 192.168.1.100
RemoteAddress    : 192.168.1.100
RemotePort       : 8443
InterfaceAlias   : Ethernet
SourceAddress    : 192.168.1.50
TcpTestSucceeded : True
```

**Başarısız Çıktı (Firewall engelliyorsa):**
```
TcpTestSucceeded : False
```

### 2. Swagger'dan Test

1. Swagger'ı açın: `http://localhost:5001/swagger`
2. `GET /api/dlptest/connection` endpoint'ini test edin
3. Başarılı olursa firewall kuralı doğru çalışıyor demektir

### 3. Authentication Test

1. Swagger'da `GET /api/dlptest/auth` endpoint'ini test edin
2. Başarılı olursa hem firewall hem de authentication çalışıyor demektir

---

## 📋 Özet

| Özellik | Değer |
|---------|-------|
| **Kural Yönü** | Outbound (Giden) |
| **Kaynak Port** | Any (Dinamik, önemli değil) |
| **Hedef Port** | 8443 (DLP Manager'ın portu) |
| **Hedef IP** | DLP Manager IP (192.168.1.100) veya Any |
| **Protokol** | TCP |
| **Aksiyon** | Allow |

**ÖNEMLİ:** Port 5001, firewall kuralında kullanılmaz. Bu port sadece Analyzer API'nin dinlediği port (Swagger için).

---

## 🔍 Sorun Giderme

### Problem: 503 Service Unavailable

**Olası Nedenler:**
1. ❌ OUTBOUND firewall kuralı yok veya yanlış yapılandırılmış
2. ❌ DLP Manager çalışmıyor
3. ❌ DLP Manager IP adresi yanlış
4. ❌ Network bağlantısı yok

**Çözüm:**
1. OUTBOUND firewall kuralını ekleyin (yukarıdaki adımları izleyin)
2. DLP Manager'ın çalıştığını kontrol edin
3. `appsettings.json`'daki IP adresini kontrol edin
4. `Test-NetConnection` ile bağlantıyı test edin

### Problem: Connection Timeout

**Olası Nedenler:**
1. ❌ Firewall kuralı yanlış yön (Inbound yerine Outbound olmalı)
2. ❌ Hedef port yanlış (8443 olmalı)
3. ❌ DLP Manager erişilemiyor

**Çözüm:**
1. Firewall kuralının **Outbound** olduğundan emin olun
2. Hedef portun **8443** olduğundan emin olun
3. DLP Manager'a network erişimini kontrol edin

---

**Son Güncelleme:** 2024-01-16

