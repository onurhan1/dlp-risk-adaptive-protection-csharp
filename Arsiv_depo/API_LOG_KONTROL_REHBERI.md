# API Log Kontrol Rehberi

## 1. Terminal'de Canlı Log Görüntüleme

API çalışırken terminal'de log'lar otomatik olarak görüntülenir. Log seviyesi `appsettings.json` dosyasında ayarlanmıştır.

### Log Seviyeleri:
- **Trace**: En detaylı log seviyesi
- **Debug**: Debug bilgileri
- **Information**: Genel bilgiler (varsayılan)
- **Warning**: Uyarılar
- **Error**: Hatalar
- **Critical**: Kritik hatalar

## 2. Log Dosyasına Kaydetme

API'yi log dosyasına kaydetmek için:

```bash
cd "DLP_Adaptive Protection CSharp/DLP.RiskAnalyzer.Analyzer"
dotnet run 2>&1 | tee api.log
```

Bu komut hem terminal'de hem de `api.log` dosyasında log'ları gösterir.

## 3. Log Dosyasını İnceleme

### Son 50 satırı görüntüleme:
```bash
tail -50 api.log
```

### Settings ile ilgili log'ları filtreleme:
```bash
tail -100 api.log | grep -i "setting\|error\|saved\|database"
```

### Hata log'larını görüntüleme:
```bash
tail -200 api.log | grep -i "error\|exception\|failed"
```

### Canlı log takibi (tail -f):
```bash
tail -f api.log
```

## 4. Belirli Endpoint'lerin Log'larını İnceleme

### Settings endpoint log'ları:
```bash
tail -200 api.log | grep -A 10 -B 5 "Settings"
```

### Database işlem log'ları:
```bash
tail -200 api.log | grep -i "database\|entityframework\|savechanges"
```

### Sample data log'ları:
```bash
tail -200 api.log | grep -i "seed\|incident\|sample"
```

## 5. Detaylı Log Seviyesi Ayarlama

`appsettings.json` dosyasında log seviyesini artırabilirsiniz:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Information",
      "Microsoft.EntityFrameworkCore.Database.Command": "Information"
    }
  }
}
```

## 6. Settings Kaydetme İşlemini Test Etme

### 1. API'yi log dosyasına kaydet:
```bash
cd "DLP_Adaptive Protection CSharp/DLP.RiskAnalyzer.Analyzer"
dotnet run 2>&1 | tee api.log
```

### 2. Settings kaydetme isteği gönder:
```bash
curl -X POST http://localhost:8000/api/settings \
  -H "Content-Type: application/json" \
  -d '{"risk_threshold_low":55,"risk_threshold_medium":75,"risk_threshold_high":95,"email_notifications":true,"daily_report_time":"15:00","admin_email":"test@example.com"}'
```

### 3. Log'ları kontrol et:
```bash
tail -100 api.log | grep -A 10 -B 5 "Settings\|Saved\|Error"
```

## 7. Entity Framework SQL Sorgularını Görüntüleme

EF Core'un çalıştırdığı SQL sorgularını görmek için log seviyesini `Information` veya `Debug` yapın:

```json
{
  "Logging": {
    "LogLevel": {
      "Microsoft.EntityFrameworkCore.Database.Command": "Information"
    }
  }
}
```

Bu ayar ile her SQL sorgusu log'lanacaktır.

## 8. Hata Ayıklama İçin Özel Log Noktaları

SettingsController'da şu log noktaları mevcuttur:
- `Saving settings. Request keys: {Keys}`
- `Settings to save: Low={Low}, Medium={Medium}, High={High}`
- `Updating setting: {Key} = {Value}`
- `Adding new setting: {Key} = {Value}`
- `Saved setting: {Key} = {Value} (rows affected: {Rows})`
- `Error saving setting {Key} = {Value}: {Message}`

## 9. Log Dosyasını Temizleme

```bash
> api.log  # Log dosyasını temizle
```

## 10. Log Dosyası Boyutunu Kontrol Etme

```bash
ls -lh api.log  # Dosya boyutunu görüntüle
```

## Örnek Log Çıktısı

```
info: DLP.RiskAnalyzer.Analyzer.Controllers.SettingsController[0]
      Saving settings. Request keys: risk_threshold_low, risk_threshold_medium, risk_threshold_high
info: DLP.RiskAnalyzer.Analyzer.Controllers.SettingsController[0]
      Settings to save: Low=55, Medium=75, High=95, Email=test@example.com
info: DLP.RiskAnalyzer.Analyzer.Controllers.SettingsController[0]
      Updating setting: risk_threshold_low = 55
info: Microsoft.EntityFrameworkCore.Database.Command[20101]
      Executed DbCommand (5ms) [Parameters=[@p0='risk_threshold_low' (DbType = String), @p1='55' (DbType = String)], CommandType='Text', CommandTimeout='30']
      UPDATE "system_settings" SET "value" = @p1, "updated_at" = @p2
      WHERE "key" = @p0;
info: DLP.RiskAnalyzer.Analyzer.Controllers.SettingsController[0]
      Saved setting: risk_threshold_low = 55 (rows affected: 1)
```

## Sorun Giderme

### Log'lar görünmüyorsa:
1. API'nin çalıştığından emin olun
2. Log seviyesinin yeterince düşük olduğunu kontrol edin
3. `appsettings.json` dosyasının doğru yapılandırıldığını kontrol edin

### Çok fazla log varsa:
1. Log seviyesini `Warning` veya `Error` yapın
2. Belirli namespace'ler için log seviyesini artırın

