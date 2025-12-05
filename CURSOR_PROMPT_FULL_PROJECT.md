# Cursor AI - DLP Risk Adaptive Protection Full Project Prompt

## 🎯 Project Overview

Create a complete **Data Loss Prevention (DLP) Risk Adaptive Protection System** that integrates with Forcepoint DLP Manager API to collect, analyze, and visualize security incidents. The system must work entirely **offline** (no internet dependencies) and be deployable on **Windows Server 2025**.

## 🏗️ System Architecture

### Three-Tier Architecture:

1. **Collector Service** (.NET 8.0 Background Service)
   - Fetches incidents from Forcepoint DLP Manager API
   - Pushes incidents to Redis Stream
   - Handles JWT token caching and refresh
   - SSL certificate bypass for self-signed certificates

2. **Analyzer API** (ASP.NET Core 8.0 Web API)
   - Processes incidents from Redis Stream
   - Stores data in PostgreSQL with TimescaleDB extension
   - Provides RESTful API endpoints
   - Risk scoring and analysis engine
   - PDF report generation
   - AI behavioral analysis

3. **Web Dashboard** (Next.js 14+ with App Router)
   - Modern dark/light theme (Tenable Security Center-like)
   - Real-time risk monitoring
   - User investigation timeline
   - Incident remediation
   - Report generation and download
   - User management (Admin/Standard roles)
   - AI behavioral analysis visualization

## 📋 Technical Stack

### Backend (.NET 8.0)
- **Framework**: ASP.NET Core 8.0 Web API
- **Database**: PostgreSQL 16+ with TimescaleDB extension
- **ORM**: Entity Framework Core with Code-First Migrations
- **Caching/Streaming**: Redis 7+
- **Authentication**: JWT (JSON Web Tokens)
- **Password Hashing**: PBKDF2 with SHA-256
- **PDF Generation**: QuestPDF
- **Logging**: Built-in .NET logging
- **API Documentation**: Swagger/OpenAPI

### Frontend (Next.js 14+)
- **Framework**: Next.js 14+ with App Router
- **Language**: TypeScript
- **Styling**: Tailwind CSS
- **Charts**: React Plotly.js
- **HTTP Client**: Axios
- **State Management**: React Hooks
- **Fonts**: System fonts only (no Google Fonts - offline requirement)
- **Build**: Standalone output for offline deployment

### Infrastructure
- **Containerization**: Docker Compose (optional, for development)
- **Windows Service**: NSSM (Non-Sucking Service Manager) for production
- **Database**: PostgreSQL 16+ with TimescaleDB
- **Cache/Stream**: Redis 7+
- **Deployment**: Windows Server 2025 native (no Docker in production)

## 🔑 Core Features

### 1. Authentication & Authorization
- JWT-based authentication
- Role-based access control (Admin/Standard)
- Default admin user: `admin` / `admin123` (configurable in appsettings.json)
- Password hashing with PBKDF2 (100,000 iterations, SHA-256)
- Token expiration and refresh
- Secure credential storage
- **IMPORTANT**: Password hash must be stored in database (not in-memory) to persist across API restarts

### 2. Incident Collection & Processing
- Fetch incidents from Forcepoint DLP Manager API (HTTPS, port 8443)
- JWT token authentication with DLP Manager
- SSL certificate validation bypass for self-signed certificates
- Redis Stream processing for real-time incident ingestion
- Automatic retry on failure
- Background service with configurable polling interval

### 3. Risk Analysis Engine
- **Risk Score Calculation**: `risk = (severity * 3) + (repeat_count * 2) + (data_sensitivity * 5)`
- **Risk Levels**:
  - Critical: 91-100
  - High: 61-90
  - Medium: 41-60
  - Low: 0-40
- **IOB (Insider of Business) Detection**: Identifies internal threats
- **Anomaly Detection**: Statistical analysis for unusual patterns
- **Policy Action Recommendations**: Based on risk level and channel
- **Risk Decay Simulation**: Time-based risk reduction

### 4. Data Models

#### Incident Model
```csharp
- Id (int, primary key)
- IncidentId (string, unique from DLP Manager)
- UserEmail (string)
- UserName (string)
- Department (string)
- Channel (string: Web, Email, Print, Removable Storage, etc.)
- Severity (int: 1-5)
- DataSensitivity (int: 1-5)
- RepeatCount (int)
- RiskScore (int: 0-100)
- RiskLevel (string: Critical, High, Medium, Low)
- PolicyName (string)
- ActionTaken (string)
- Timestamp (DateTime)
- CreatedAt (DateTime)
- UpdatedAt (DateTime)
- IsRemediated (bool)
- RemediatedAt (DateTime?)
```

#### User Model (for authentication)
```csharp
- Id (int, primary key)
- Username (string, unique)
- Email (string)
- Role (string: "admin" or "standard")
- PasswordHash (string)
- PasswordSalt (string)
- CreatedAt (DateTime)
- IsActive (bool)
```

#### System Settings Model
```csharp
- Id (int, primary key)
- Key (string, unique)
- Value (string)
- UpdatedAt (DateTime)
```

### 5. API Endpoints

#### Authentication
- `POST /api/auth/login` - User login, returns JWT token
- `GET /api/auth/me` - Get current user info (requires authentication)

#### Incidents
- `GET /api/incidents` - Get incidents (filtered, paginated)
  - Query params: `startDate`, `endDate`, `userEmail`, `department`, `channel`, `riskLevel`, `page`, `pageSize`
- `GET /api/incidents/{id}` - Get incident by ID
- `PUT /api/incidents/{id}` - Update incident
- `POST /api/incidents/{id}/remediate` - Remediate incident (calls DLP Manager API)

#### Risk Analysis
- `GET /api/risk/trends` - User risk trends over time
- `GET /api/risk/daily-summary` - Daily risk summaries
- `GET /api/risk/department-summary` - Department-wise summaries
- `GET /api/risk/heatmap` - Risk heatmap data (dimension: department/user/channel)
- `GET /api/risk/user-list` - Paginated user list with risk scores
- `GET /api/risk/channel-activity` - Channel activity breakdown
- `GET /api/risk/iob-detections` - IOB (Insider of Business) detections
- `GET /api/risk/decay/simulation` - Risk decay simulation
- `POST /api/risk/anomaly/calculate` - Calculate anomalies
- `GET /api/risk/anomaly/detections` - Get anomaly detections

#### Classification
- `GET /api/incidents/{id}/classification` - Get incident classification details
- `GET /api/incidents/{id}/files` - Get incident files
- `GET /api/users/{email}/classification` - Get user classification summary

#### Policies
- `GET /api/policies` - Get all policies
- `GET /api/policies/{id}` - Get policy by ID
- `POST /api/policies/recommendations` - Get policy action recommendations

#### Reports
- `GET /api/reports` - List all generated reports
- `POST /api/reports/generate` - Generate PDF report
  - Body: `{ "startDate": "2024-01-01", "endDate": "2024-01-31", "format": "pdf" }`
- `GET /api/reports/{id}/download` - Download report PDF

#### Settings
- `GET /api/settings` - Get system settings
- `POST /api/settings` - Save system settings (DLP API credentials, etc.)
- `GET /api/settings/dlp` - Get DLP API configuration
- `POST /api/settings/dlp` - Save DLP API configuration
- `POST /api/settings/dlp/test-connection` - Test DLP API connection

#### Users (Admin only)
- `GET /api/users` - Get all users
- `GET /api/users/{id}` - Get user by ID
- `POST /api/users` - Create new user
- `PUT /api/users/{id}` - Update user
- `DELETE /api/users/{id}` - Delete user

#### AI Behavioral Analysis
- `GET /api/ai-behavioral/analysis` - Get AI behavioral analysis
- `POST /api/ai-behavioral/analyze` - Trigger AI analysis
- `GET /api/ai-behavioral/settings` - Get AI settings
- `POST /api/ai-behavioral/settings` - Update AI settings

#### Logs
- `GET /api/logs` - Get application logs (filtered, paginated)
- `GET /api/logs/audit` - Get audit logs

#### Database
- `GET /api/database/status` - Get database connection status
- `POST /api/database/migrate` - Apply database migrations (if auto-migration disabled)

#### Health
- `GET /health` - Health check endpoint

### 6. Dashboard Pages

#### Main Dashboard (`/`)
- Real-time risk metrics
- Top users by risk score
- Top matched rules
- Daily incident trends chart
- Data movement by channel (pie chart)
- Risk heatmap visualization

#### Investigation (`/investigation`)
- User investigation timeline
- Filter by user email, date range
- Interactive timeline visualization
- Incident details modal

#### Reports (`/reports`)
- List of generated reports
- Generate new report (date range selection)
- Download PDF reports
- Report preview

#### Settings (`/settings`)
- DLP API configuration (Manager IP, Username, Password, Port)
- Test DLP API connection
- Email configuration
- AI settings
- System settings

#### Users (`/users`) - Admin only
- User list with roles
- Create new user
- Edit user
- Delete user
- Change user password

#### AI Behavioral (`/ai-behavioral`)
- AI behavioral analysis visualization
- User behavior patterns
- Risk predictions

#### AI Settings (`/ai-settings`)
- Configure AI models
- Set analysis parameters
- Enable/disable AI features

#### Logs (`/logs`)
- Application logs viewer
- Filter by log level, date range
- Search functionality

### 7. Database Schema

#### TimescaleDB Hypertables
- `incidents` - Main incidents table (hypertable for time-series data)
- `audit_logs` - Audit trail (hypertable)

#### Regular Tables
- `users` - User accounts
- `system_settings` - System configuration
- `reports` - Generated reports metadata
- `ai_behavioral_analysis` - AI analysis results

#### Entity Framework Migrations
- Automatic migrations on application startup (configurable)
- Migration files in `Migrations/` folder
- Connection string in `appsettings.json`

### 8. Configuration Files

#### `appsettings.json` (Analyzer API)
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=dlp_analyzer;Username=postgres;Password=postgres"
  },
  "Database": {
    "AutoMigrate": true
  },
  "Authentication": {
    "Username": "admin",
    "Password": "admin123",
    "JwtSecret": "your-secret-key-min-32-chars",
    "JwtExpirationHours": 8
  },
  "Redis": {
    "Host": "localhost",
    "Port": 6379,
    "StreamName": "dlp_incidents"
  },
  "DLP": {
    "ManagerIP": "YOUR_DLP_MANAGER_IP",
    "Username": "YOUR_DLP_USERNAME",
    "Password": "YOUR_DLP_PASSWORD",
    "Port": 8443,
    "UseHttps": true
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  },
  "AllowedHosts": "*",
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://0.0.0.0:5001"
      }
    }
  }
}
```

#### `appsettings.json` (Collector)
```json
{
  "Redis": {
    "Host": "localhost",
    "Port": 6379,
    "StreamName": "dlp_incidents"
  },
  "DLP": {
    "ManagerIP": "YOUR_DLP_MANAGER_IP",
    "Username": "YOUR_DLP_USERNAME",
    "Password": "YOUR_DLP_PASSWORD",
    "Port": 8443,
    "UseHttps": true
  },
  "Collector": {
    "PollIntervalSeconds": 60,
    "BatchSize": 100
  },
  "AnalyzerApi": {
    "BaseUrl": "http://localhost:5001"
  }
}
```

#### `next.config.js` (Dashboard)
```javascript
/** @type {import('next').NextConfig} */
const nextConfig = {
  output: 'standalone',
  experimental: {
    optimizePackageImports: ['react-plotly.js', 'plotly.js'],
  },
  turbopack: {
    root: process.cwd(),
  },
}

module.exports = nextConfig
```

#### `package.json` (Dashboard)
```json
{
  "name": "dlp-dashboard",
  "version": "1.0.0",
  "scripts": {
    "dev": "next dev -p 3002",
    "build": "next build",
    "start": "next start -p 3002"
  },
  "dependencies": {
    "next": "^14.0.0",
    "react": "^18.0.0",
    "react-dom": "^18.0.0",
    "axios": "^1.6.0",
    "react-plotly.js": "^2.6.0",
    "plotly.js": "^2.27.0",
    "tailwindcss": "^3.4.0"
  }
}
```

### 9. Offline Deployment Requirements

#### No Internet Dependencies
- **No Google Fonts**: Use system fonts only
- **No CDN**: All assets must be bundled
- **No External APIs**: Except Forcepoint DLP Manager (internal network)
- **Standalone Build**: Next.js `output: 'standalone'` configuration
- **Pre-built Dependencies**: `node_modules` and build artifacts included in deployment

#### Font Configuration
- Use Windows system fonts: `'Segoe UI', 'Segoe UI Variable', Tahoma, Arial, Verdana`
- No external font loading
- CSS font-family stack optimized for Windows Server 2025

#### Build Artifacts
- Include `node_modules/` in deployment zip
- Include `.next/` build folder
- Include `bin/`, `obj/` folders
- Exclude only OS-specific files (`.DS_Store`, `._*` on Mac)

### 10. Windows Server 2025 Deployment

#### Service Configuration
- Use NSSM (Non-Sucking Service Manager) to run as Windows Services
- Analyzer API: Port 5001, listen on `0.0.0.0`
- Dashboard: Port 3002, listen on `0.0.0.0`
- Collector: Background service

#### Database Setup
- PostgreSQL 16+ installation
- TimescaleDB extension enabled
- Database: `dlp_analyzer`
- Automatic migrations on startup (configurable)

#### Redis Setup
- Redis 7+ installation
- Port 6379
- Stream name: `dlp_incidents`

#### Network Configuration
- Firewall rules for ports 5001, 3002, 5432, 6379
- API accessible from network (not just localhost)

### 11. Security Requirements

#### Authentication
- JWT tokens with expiration
- Password hashing: PBKDF2, 100,000 iterations, SHA-256
- Secure password storage in database (not in-memory)
- Role-based access control

#### API Security
- CORS configuration for dashboard origin
- Input validation and sanitization
- SQL injection prevention (EF Core parameterized queries)
- XSS prevention (React automatic escaping)

#### SSL/TLS
- Support for self-signed certificates (DLP Manager API)
- SSL certificate validation bypass option
- HTTPS for DLP Manager API communication

### 12. Error Handling & Logging

#### Logging
- Structured logging with .NET ILogger
- Log levels: Debug, Information, Warning, Error, Critical
- Log file: `api.log` in Analyzer project root
- Audit logging for user actions

#### Error Handling
- Global exception handling middleware
- Detailed error messages in development
- Generic error messages in production
- HTTP status codes: 200, 400, 401, 403, 404, 500

### 13. Testing & Validation

#### API Testing
- Swagger UI at `/swagger`
- Health check endpoint: `/health`
- DLP API connection test endpoint

#### Frontend Testing
- Browser console error checking
- Network tab request/response validation
- Login flow testing

### 14. Code Quality Requirements

#### C# Code
- Async/await for all I/O operations
- Dependency injection
- Repository pattern for data access
- Service layer for business logic
- Controller layer for HTTP handling
- Proper error handling and logging

#### TypeScript/React Code
- TypeScript strict mode
- Functional components with hooks
- Proper error boundaries
- Loading states
- Error states
- Responsive design

### 15. Documentation Requirements

#### Code Documentation
- XML comments for public APIs
- README.md with setup instructions
- API documentation via Swagger
- Configuration guides

#### Deployment Documentation
- Windows Server 2025 installation guide
- Offline deployment guide
- Troubleshooting guide
- API endpoint documentation

## 🚀 Implementation Steps

1. **Project Structure Setup**
   - Create .NET solution with 3 projects (Analyzer, Collector, Shared)
   - Create Next.js dashboard project
   - Configure dependencies

2. **Database Setup**
   - Entity Framework Core DbContext
   - Create migrations
   - Configure TimescaleDB hypertables

3. **Authentication System**
   - JWT token generation
   - Password hashing
   - User management
   - Role-based authorization

4. **Collector Service**
   - DLP Manager API integration
   - Redis Stream writing
   - Background service implementation

5. **Analyzer API**
   - RESTful endpoints
   - Risk analysis engine
   - Report generation
   - Settings management

6. **Dashboard Frontend**
   - Next.js setup with App Router
   - Dark/light theme
   - All dashboard pages
   - API integration

7. **Offline Configuration**
   - Remove Google Fonts
   - System fonts configuration
   - Standalone build setup
   - Deployment scripts

8. **Windows Server Deployment**
   - NSSM service configuration
   - Database migration automation
   - Service startup scripts

## ⚠️ Critical Requirements

1. **Password Hash Persistence**: Must be stored in database, NOT in-memory
2. **Offline Support**: No internet dependencies except internal DLP Manager API
3. **Windows Server 2025**: Native deployment without Docker
4. **Automatic Migrations**: Database migrations must run automatically on startup
5. **Network Access**: API and Dashboard must listen on `0.0.0.0` for network access
6. **Encoding**: UTF-8 encoding for all text processing
7. **Password Normalization**: Handle Windows line endings and control characters

## 📝 Additional Notes

- Use existing code patterns and naming conventions
- Follow .NET and React best practices
- Ensure all features are fully functional
- Test on Windows Server 2025 before deployment
- Provide comprehensive error messages
- Log all important operations
- Support both development and production configurations

---

**Start building this complete system step by step, ensuring all features are implemented correctly and the system works entirely offline on Windows Server 2025.**

