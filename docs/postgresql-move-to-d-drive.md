# PostgreSQL Veri Dizinini D: Diskine Taşıma Rehberi

> **Sunucu:** Windows Server — PostgreSQL 16.11  
> **Mevcut konum:** `C:\Program Files\PostgreSQL\16\data` (varsayılan)  
> **Hedef konum:** `D:\PostgreSQL\data`  
> **DB Boyutu:** ~603 MB  

---

## ⚠️ ÖNEMLİ — Başlamadan Önce

- **Backup al!** Taşımadan önce mutlaka yedek alın
- İşlem sırasında **uygulama durdurulmalı** (Collector, Analyzer, Dashboard)
- Tahmini süre: **5-10 dakika** (603 MB için)

---

## Adım 1: Mevcut Data Dizinini Kontrol Et

PowerShell'de (Admin olarak):

```powershell
# Mevcut data dizinini bul
& "C:\Program Files\PostgreSQL\16\bin\psql.exe" -U postgres -c "SHOW data_directory;"
```

Çıktı şuna benzer olacak:
```
 data_directory
--------------------------------------
 C:/Program Files/PostgreSQL/16/data
```

Bu dizini not alın.

---

## Adım 2: Servisleri Durdur

```powershell
# 1. Önce uygulamaları durdur
Stop-Service -Name "DLPCollector" -ErrorAction SilentlyContinue
Stop-Service -Name "DLPAnalyzer" -ErrorAction SilentlyContinue

# 2. PostgreSQL servisini durdur
Stop-Service -Name "postgresql-x64-16"

# 3. Servisin durduğunu kontrol et
Get-Service -Name "postgresql-x64-16" | Select-Object Status, Name
```

Çıktıda `Stopped` göründüğünden emin olun.

---

## Adım 3: D: Diskinde Hedef Dizini Oluştur

```powershell
# Hedef dizin oluştur
New-Item -ItemType Directory -Path "D:\PostgreSQL" -Force
```

---

## Adım 4: Veri Dizinini Kopyala

```powershell
# Robocopy ile kopyala (izinler ve zaman damgaları korunur)
robocopy "C:\Program Files\PostgreSQL\16\data" "D:\PostgreSQL\data" /E /COPYALL /R:3 /W:5 /LOG:"D:\PostgreSQL\migration-log.txt"
```

**Parametreler:**
- `/E` — Boş alt dizinler dahil tüm alt dizinleri kopyala
- `/COPYALL` — Tüm dosya bilgilerini kopyala (izinler, sahiplik vb.)
- `/R:3` — Hata durumunda 3 kez dene
- `/W:5` — Denemeler arası 5 saniye bekle
- `/LOG:` — İşlem logunu dosyaya kaydet

Kopyalama bittikten sonra log dosyasını kontrol edin:
```powershell
Get-Content "D:\PostgreSQL\migration-log.txt" | Select-Object -Last 10
```

---

## Adım 5: İzinleri Ayarla

PostgreSQL servis hesabının D: diskindeki yeni dizine erişimi olmalı:

```powershell
# PostgreSQL servis hesabını bul
$svc = Get-WmiObject Win32_Service -Filter "Name='postgresql-x64-16'"
Write-Host "Servis Hesabi: $($svc.StartName)"

# İzinleri ayarla (genellikle "NT AUTHORITY\NetworkService" veya özel hesap)
$acl = Get-Acl "D:\PostgreSQL\data"
$serviceAccount = $svc.StartName

# Tam kontrol izni ver
$rule = New-Object System.Security.AccessControl.FileSystemAccessRule(
    $serviceAccount,
    "FullControl",
    "ContainerInherit,ObjectInherit",
    "None",
    "Allow"
)
$acl.SetAccessRule($rule)
Set-Acl -Path "D:\PostgreSQL\data" -AclObject $acl

# Alt dizinlere de uygula
Get-ChildItem "D:\PostgreSQL\data" -Recurse | ForEach-Object {
    Set-Acl -Path $_.FullName -AclObject $acl
}

Write-Host "Izinler ayarlandi."
```

---

## Adım 6: postgresql.conf Dosyasını Güncelle

```powershell
# YENİ konumdaki conf dosyasını düzenle
$confPath = "D:\PostgreSQL\data\postgresql.conf"
$content = Get-Content $confPath -Raw

# data_directory satırını güncelle (yorum satırı olabilir)
# Eğer yorum satırıysa, aç ve güncelle
if ($content -match "#data_directory") {
    $content = $content -replace "#data_directory\s*=\s*'.*'", "data_directory = 'D:/PostgreSQL/data'"
} elseif ($content -match "data_directory") {
    $content = $content -replace "data_directory\s*=\s*'.*'", "data_directory = 'D:/PostgreSQL/data'"
} else {
    # Satır yoksa ekle
    $content = "data_directory = 'D:/PostgreSQL/data'`n" + $content
}

Set-Content $confPath $content -Encoding UTF8
Write-Host "postgresql.conf guncellendi."
```

---

## Adım 7: Windows Servis Kayıt Defterini Güncelle

PostgreSQL servisi, hangi config dosyasını kullanacağını bilmeli:

```powershell
# Mevcut servis yapılandırmasını gör
$svc = Get-WmiObject Win32_Service -Filter "Name='postgresql-x64-16'"
Write-Host "Mevcut ImagePath: $($svc.PathName)"

# Servis başlatma komutundaki -D parametresini güncelle
# Örnek eski: "...pg_ctl.exe" runservice -N ... -D "C:\Program Files\PostgreSQL\16\data"
# Yeni: -D "D:\PostgreSQL\data"

# Registry'den güncelle
$regPath = "HKLM:\SYSTEM\CurrentControlSet\Services\postgresql-x64-16"
$currentImagePath = (Get-ItemProperty $regPath).ImagePath

# Eski data yolunu yenisiyle değiştir
$newImagePath = $currentImagePath -replace [regex]::Escape('"C:\Program Files\PostgreSQL\16\data"'), '"D:\PostgreSQL\data"'
$newImagePath = $newImagePath -replace [regex]::Escape("C:\\Program Files\\PostgreSQL\\16\\data"), "D:\\PostgreSQL\\data"

Set-ItemProperty -Path $regPath -Name "ImagePath" -Value $newImagePath

Write-Host "Eski: $currentImagePath"
Write-Host "Yeni: $newImagePath"
```

---

## Adım 8: PostgreSQL'i Yeni Konumdan Başlat

```powershell
# Servisi başlat
Start-Service -Name "postgresql-x64-16"

# Durumu kontrol et
Get-Service -Name "postgresql-x64-16" | Select-Object Status, Name
```

Eğer servis **başlamazsa**, hata loglarını kontrol edin:
```powershell
# Windows Event Log kontrol
Get-EventLog -LogName Application -Source "postgresql*" -Newest 5 | Format-List

# PostgreSQL log dosyası
Get-Content "D:\PostgreSQL\data\log\*.log" | Select-Object -Last 30
```

---

## Adım 9: Doğrulama

```powershell
# 1. Yeni data dizinini doğrula
& "C:\Program Files\PostgreSQL\16\bin\psql.exe" -U postgres -c "SHOW data_directory;"
# Çıktı: D:/PostgreSQL/data olmalı

# 2. Veritabanı boyutunu doğrula
& "C:\Program Files\PostgreSQL\16\bin\psql.exe" -U postgres -c "SELECT pg_size_pretty(pg_database_size('dlp_risk_db'));"

# 3. Tablo sayılarını doğrula
& "C:\Program Files\PostgreSQL\16\bin\psql.exe" -U postgres -d dlp_risk_db -c "SELECT count(*) FROM incidents;"
# Çıktı: 179164 olmalı

# 4. Bağlantı testi
& "C:\Program Files\PostgreSQL\16\bin\psql.exe" -U postgres -d dlp_risk_db -c "SELECT version();"
```

---

## Adım 10: Uygulamaları Yeniden Başlat

```powershell
# Servisleri başlat
Start-Service -Name "DLPCollector" -ErrorAction SilentlyContinue
Start-Service -Name "DLPAnalyzer" -ErrorAction SilentlyContinue

# Connection string değişikliği GEREKMİYOR
# PostgreSQL aynı port ve aynı IP'den çalışmaya devam eder
```

> **NOT:** Connection string'de bir değişiklik yapmanıza **gerek yok**. PostgreSQL hala aynı port (5432) ve adres üzerinden çalışır, sadece verilerin fiziksel konumu değişir.

---

## Adım 11: Eski Dizini Temizle (Opsiyonel)

Her şey çalıştığından emin olduktan sonra (1-2 gün bekleyin):

```powershell
# Eski data dizinini yedek olarak yeniden adlandır
Rename-Item "C:\Program Files\PostgreSQL\16\data" "C:\Program Files\PostgreSQL\16\data_OLD_BACKUP"

# 1-2 hafta sorunsuz çalıştıktan sonra silebilirsiniz:
# Remove-Item "C:\Program Files\PostgreSQL\16\data_OLD_BACKUP" -Recurse -Force
```

---

## 🔧 Sorun Giderme

### Servis başlamıyor
```powershell
# Log kontrol
Get-Content "D:\PostgreSQL\data\log\*.log" | Select-Object -Last 50

# İzin kontrolü
icacls "D:\PostgreSQL\data"

# Manuel başlatmayı dene (hata mesajı daha detaylı olur)
& "C:\Program Files\PostgreSQL\16\bin\pg_ctl.exe" start -D "D:\PostgreSQL\data" -l "D:\PostgreSQL\startup.log"
```

### "Permission denied" hatası
```powershell
# Tüm dosyaları PostgreSQL servis hesabına ver
$svc = Get-WmiObject Win32_Service -Filter "Name='postgresql-x64-16'"
icacls "D:\PostgreSQL\data" /grant "$($svc.StartName):(OI)(CI)F" /T
```

### Disk alanı kontrolü
```powershell
Get-PSDrive D | Select-Object Used, Free, @{N='Free_GB';E={[math]::Round($_.Free/1GB,2)}}
```
