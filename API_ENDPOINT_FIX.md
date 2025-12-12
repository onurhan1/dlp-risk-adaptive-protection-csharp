# API Endpoint Düzeltmesi

## ❌ Sorun

Dashboard'daki API çağrıları `/api/` prefix'i olmadan yapılıyordu.

**Yanlış**: `${API_URL}/reports`
**Doğru**: `${API_URL}/api/reports`

## ✅ Düzeltme

Tüm endpoint çağrıları `/api/` prefix'i ile güncellendi:

### Reports
- `/reports` → `/api/reports`
- `/reports/generate` → `/api/reports/generate`
- `/reports/{id}/download` → `/api/reports/{id}/download`

### Risk
- `/risk/trends` → `/api/risk/trends`
- `/risk/daily-summary` → `/api/risk/daily-summary`
- `/risk/department-summary` → `/api/risk/department-summary`
- `/risk/user-list` → `/api/risk/user-list`
- `/risk/channel-activity` → `/api/risk/channel-activity`

### Incidents
- `/incidents` → `/api/incidents`
- `/incidents/{id}` → `/api/incidents/{id}`

### Settings
- `/settings` → `/api/settings`

### Policies
- `/policies` → `/api/policies`

## 📋 C# API Route Yapısı

Tüm controller'lar `[Route("api/[controller]")]` attribute'u kullanıyor:

- `ReportsController` → `/api/reports`
- `RiskController` → `/api/risk`
- `IncidentsController` → `/api/incidents`
- `SettingsController` → `/api/settings`
- `PoliciesController` → `/api/policies`

## 🔧 Sonraki Adım

Dashboard'ı yeniden yükleyin (hard refresh: Cmd+Shift+R veya Ctrl+Shift+R)

