# DLP Risk Adaptive Protection - C# Application

Forcepoint DLP Risk Adaptive Protection için Windows Native C# uygulaması.

## 📋 Proje Yapısı

```
DLP_Adaptive Protection CSharp/
├── DLP.RiskAnalyzer.Analyzer/        # ASP.NET Core Web API
│   └── Controllers/
│       ├── AuthController.cs         # Authentication endpoints
│       ├── RemediationController.cs  # Incident remediation
│       ├── UsersController.cs        # User management
│       └── ReportsController.cs      # Report generation
├── DLP.RiskAnalyzer.Dashboard/       # WPF Desktop Application (Windows only)
│   ├── App.xaml
│   ├── LoginWindow.xaml
│   └── MainWindow.xaml
└── dashboard/                        # Next.js Web Dashboard
    ├── app/                          # Next.js app directory
    ├── components/                   # React components
    └── package.json
```

## 🚀 Özellikler

### ✅ Login Sistemi
- Modern Material Design login ekranı
- JWT token tabanlı authentication
- "Remember Me" özelliği
- Güvenli credential storage

### ✅ Web Dashboard (Next.js)
- Modern Tenable Security Center-like dark/light theme
- Real-time risk monitoring
- User investigation with timeline
- Incident remediation
- Report generation and download
- User management (Admin only)
- Role-based access control (Admin/Standard)

### ✅ WPF Desktop Dashboard (Windows)
- Material Design UI
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
- Node.js 18+ ve npm (Web Dashboard için)
- Visual Studio 2022 veya Rider (WPF Dashboard için - Windows only)
- MaterialDesignThemes NuGet paketi (WPF Dashboard için)

### Adımlar

1. **Projeyi klonlayın**:
```bash
git clone https://github.com/onurhan1/dlp-risk-adaptive-protection-csharp.git
cd "DLP_Adaptive Protection CSharp"
```

2. **API Dependencies'i yükleyin**:
```bash
dotnet restore
```

3. **Web Dashboard Dependencies'i yükleyin**:
```bash
cd dashboard
npm install
cd ..
```

4. **API'yi çalıştırın**:
```bash
cd DLP.RiskAnalyzer.Analyzer
dotnet run
# API http://localhost:8000 adresinde çalışacak
```

5. **Web Dashboard'u çalıştırın** (Yeni Terminal):
```bash
cd dashboard
npm run dev
# Dashboard http://localhost:3002 adresinde çalışacak
```

6. **WPF Dashboard'u çalıştırın** (Windows only):
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

- ✅ Next.js Web Dashboard eklendi (Port 3002)
- ✅ Dark/Light theme toggle özelliği
- ✅ Role-based access control (Admin/Standard)
- ✅ User management sistemi
- ✅ Incident remediation (DLP Manager API bağlantısı olmasa bile çalışır)
- ✅ Report generation ve download
- ✅ Login ekranı (WPF ve Web)
- ✅ Authentication API endpoint'leri
- ✅ Token-based authentication implementasyonu
- ✅ GitHub push script'i eklendi

## 🔧 Önemli Notlar

### Incident Remediation
RemediationService, DLP Manager API (port 8443) bağlantısı olmasa bile remediate işlemlerini başarılı olarak kaydeder. API bağlantısı sağlandığında gerçek remediate işlemleri yapılır.

### Dashboard Port
Web Dashboard varsayılan olarak **Port 3002**'de çalışır. Port bilgileri için `DASHBOARD_PORT.md` dosyasına bakın.

## 📄 Lisans

Bu proje şirket içi kullanım için geliştirilmiştir.

