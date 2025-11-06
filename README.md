# DLP Risk Adaptive Protection - C# Application

Forcepoint DLP Risk Adaptive Protection için Windows Native C# uygulaması.

## 📋 Proje Yapısı

```
DLP_Adaptive Protection CSharp/
├── DLP.RiskAnalyzer.Analyzer/        # ASP.NET Core Web API
│   └── Controllers/
│       ├── AuthController.cs         # Authentication endpoints
│       └── ClassificationController.cs
└── DLP.RiskAnalyzer.Dashboard/       # WPF Desktop Application
    ├── App.xaml                      # Application entry point
    ├── App.xaml.cs
    ├── LoginWindow.xaml              # Login screen
    ├── LoginWindow.xaml.cs
    ├── MainWindow.xaml               # Main dashboard
    └── MainWindow.xaml.cs
```

## 🚀 Özellikler

### ✅ Login Sistemi
- Modern Material Design login ekranı
- JWT token tabanlı authentication
- "Remember Me" özelliği
- Güvenli credential storage

### ✅ Dashboard
- Real-time risk monitoring
- Interactive data grids
- User investigation timeline
- Alert details view

## 🔐 Authentication

Varsayılan kullanıcı bilgileri:
- **Username**: `admin`
- **Password**: `admin123`

Bu bilgiler `appsettings.json` dosyasında yapılandırılabilir:

```json
{
  "Authentication": {
    "Username": "admin",
    "Password": "admin123"
  }
}
```

## 🛠️ Kurulum

### Gereksinimler
- .NET 8.0 SDK
- Visual Studio 2022 veya Rider
- MaterialDesignThemes NuGet paketi

### Adımlar

1. **Projeyi klonlayın**:
```bash
git clone https://github.com/onurhan1/dlp-risk-adaptive-protection-csharp.git
cd "DLP_Adaptive Protection CSharp"
```

2. **Dependencies'i yükleyin**:
```bash
dotnet restore
```

3. **API'yi çalıştırın**:
```bash
cd DLP.RiskAnalyzer.Analyzer
dotnet run
```

4. **Dashboard'u çalıştırın**:
```bash
cd DLP.RiskAnalyzer.Dashboard
dotnet run
```

## 📤 GitHub'a Push

### Otomatik Push Script

Tüm değişiklikleri otomatik olarak commit ve push etmek için:

```bash
./push-to-github.sh
```

### Manuel Push

```bash
git add .
git commit -m "Açıklayıcı mesaj"
git push origin main
```

**Not:** İlk push için Personal Access Token gereklidir. Detaylar için `PUSH_INSTRUCTIONS.md` dosyasına bakın.

## 🔧 Yapılandırma

### API Base URL

Dashboard'da API URL'ini yapılandırmak için `appsettings.json`:

```json
{
  "ApiBaseUrl": "http://localhost:8000"
}
```

## 📝 Son Değişiklikler

- ✅ Login ekranı eklendi
- ✅ Authentication API endpoint'leri eklendi
- ✅ Token-based authentication implementasyonu
- ✅ GitHub push script'i eklendi

## 📄 Lisans

Bu proje şirket içi kullanım için geliştirilmiştir.

