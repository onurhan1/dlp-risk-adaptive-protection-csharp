# API Servisini Yeniden Başlatma Rehberi

## Sorun
AI Settings sayfasında "404 - AI Settings endpoint not found" hatası alınıyor.

## Çözüm Adımları

### 1. API Servisini Durdurun
Eğer API servisi çalışıyorsa, önce durdurun:
- Terminal'de `Ctrl+C` ile durdurun
- Veya Windows Service ise: `net stop DLP.RiskAnalyzer.Analyzer`

### 2. Projeyi Yeniden Derleyin
```bash
cd "DLP_Adaptive Protection CSharp"
dotnet build DLP.RiskAnalyzer.Analyzer/DLP.RiskAnalyzer.Analyzer.csproj
```

### 3. API Servisini Başlatın

#### Development Ortamı:
```bash
cd DLP.RiskAnalyzer.Analyzer
dotnet run
```

#### Production (Windows Service):
```bash
# NSSM ile yönetiliyorsa:
net start DLP.RiskAnalyzer.Analyzer

# Veya doğrudan:
cd DLP.RiskAnalyzer.Analyzer
dotnet DLP.RiskAnalyzer.Analyzer.dll
```

### 4. API'nin Çalıştığını Doğrulayın

Tarayıcıda şu URL'leri test edin:
- `http://localhost:5001/swagger` - Swagger UI açılmalı
- `http://localhost:5001/api/settings/ai` - JSON response dönmeli
- `http://localhost:5001/health` - Health check endpoint

### 5. Dashboard'u Yenileyin
- Tarayıcıda `Ctrl+F5` ile hard refresh yapın
- Veya dashboard'u yeniden başlatın:
```bash
cd dashboard
npm run dev
```

## Kontrol Listesi

- [ ] API servisi çalışıyor mu? (`http://localhost:5001/health`)
- [ ] Yeni controller'lar derlendi mi? (Build log'unda hata var mı?)
- [ ] API port'u doğru mu? (Varsayılan: 5001)
- [ ] Dashboard API URL'i doğru mu? (`http://localhost:5001` veya network IP)
- [ ] CORS ayarları doğru mu? (Program.cs'de `AllowAnyOrigin` var mı?)

## Hata Devam Ederse

1. **Swagger'da kontrol edin:**
   - `http://localhost:5001/swagger` adresine gidin
   - `GET /api/settings/ai` endpoint'ini görüyor musunuz?

2. **API log'larını kontrol edin:**
   - Terminal'de API log'larına bakın
   - 404 hatası görüyor musunuz?

3. **Network tab'ını kontrol edin:**
   - Browser DevTools > Network
   - Request URL'i doğru mu?
   - Response status code nedir?

