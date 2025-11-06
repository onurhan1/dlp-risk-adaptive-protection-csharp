# Dashboard Port Bilgileri

## 🚀 Çalışan Port

Dashboard şu anda **Port 3001**'de çalışıyor.

**URL**: http://localhost:3001

## 📋 Port Yapılandırması

### Değişken Port ile Başlatma

```bash
# Port 3001 (varsayılan)
./start-dashboard.sh

# VEYA farklı port ile
DASHBOARD_PORT=3002 ./start-dashboard.sh
```

### Manuel Başlatma

```bash
cd dashboard

# Port 3001 (varsayılan)
npm run dev

# Port 3000
npm run dev:3000

# Özel port
PORT=3002 npm run dev
```

## 🔧 Port Değiştirme

### Yöntem 1: Environment Variable

```bash
export DASHBOARD_PORT=3002
./start-dashboard.sh
```

### Yöntem 2: package.json Script'i Değiştirme

`dashboard/package.json` dosyasında:
```json
"dev": "next dev -p 3002"
```

### Yöntem 3: Doğrudan Komut

```bash
cd dashboard
next dev -p 3002
```

## 📍 Erişim URL'leri (Port 3001)

- **Ana Dashboard**: http://localhost:3001
- **Investigation**: http://localhost:3001/investigation
- **Reports**: http://localhost:3001/reports
- **Settings**: http://localhost:3001/settings

## ⚙️ Diğer Servisler

- **Analyzer API**: http://localhost:8000
- **Swagger UI**: http://localhost:8000/swagger
- **TimescaleDB**: localhost:5432
- **Redis**: localhost:6379

