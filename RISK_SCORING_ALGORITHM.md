# Risk Scoring Algorithm Documentation

## 📋 Overview

This document describes the hybrid risk scoring algorithm used in the DLP Risk Analyzer system. The algorithm is designed to identify users with **persistent risky behavior** rather than one-time high-impact events.

---

## 🎯 Problem Statement

### Original Issue
The original formula could mark a user with a single high-severity incident as "Top Risky":

| User | Incidents | Max Matches | Original Score |
|------|-----------|-------------|----------------|
| User A | 1 | 500 | ~83 |
| User B | 50 | 200 | ~37 |

**Problem**: User A had one accidental high-match event, while User B has a consistent pattern of risky behavior. The old algorithm ranked User A higher.

### Solution
Introduce a **Consistency Factor** that penalizes one-time events and rewards persistent patterns.

---

## 📐 Algorithm Formula

### Base Score Formula
```
base_score = MIN(100, (Avg/500 × 50) + (Max/500 × 30) + MIN(20, LOG₁₀(Count+1) × 10))
```

| Component | Weight | Max Points | Description |
|-----------|--------|------------|-------------|
| Average Risk Score | 50% | 50 pts | `avgRiskScore / 500 * 50` |
| Maximum Risk Score | 30% | 30 pts | `maxRiskScore / 500 * 30` |
| Incident Count | 20% | 20 pts | `MIN(20, LOG₁₀(count + 1) * 10)` |

### Consistency Factor
```
consistency_factor = MIN(1.0, days_with_activity / min_days_required)
```

| Period | Min Days Required | Description |
|--------|-------------------|-------------|
| 24h / daily | 1 | No penalty (today's activity) |
| weekly | 2 | At least 2 days in a week |
| monthly | 3 | At least 3 days in a month |
| quarterly (3 months) | 5 | At least 5 days in 3 months |
| 6 months | 7 | At least 7 days in 6 months |
| yearly | 10 | At least 10 days in a year |

### Adjusted Score (Final)
```
adjusted_score = base_score × consistency_factor
```

---

## 📊 Examples

### Example 1: One-Time High-Risk Event (Penalized)
```
User: john.doe@company.com
Period: Quarterly (3 months)
Days with Activity: 1
Min Days Required: 5

Base Score: 83.0
Consistency Factor: MIN(1, 1/5) = 0.2
Adjusted Score: 83.0 × 0.2 = 16.6 ⬇️
```

### Example 2: Persistent Risky Behavior (Full Score)
```
User: jane.smith@company.com
Period: Quarterly (3 months)
Days with Activity: 12
Min Days Required: 5

Base Score: 65.0
Consistency Factor: MIN(1, 12/5) = 1.0
Adjusted Score: 65.0 × 1.0 = 65.0 ✓
```

### Example 3: Moderate Activity (Partial Penalty)
```
User: bob.wilson@company.com
Period: Quarterly (3 months)
Days with Activity: 3
Min Days Required: 5

Base Score: 70.0
Consistency Factor: MIN(1, 3/5) = 0.6
Adjusted Score: 70.0 × 0.6 = 42.0
```

---

## 🔧 API Reference

### Endpoint
```
GET /api/risk-trends/top-users
```

### Parameters
| Parameter | Type | Default | Options |
|-----------|------|---------|---------|
| `period` | string | "24h" | `24h`, `daily`, `weekly`, `monthly`, `1month`, `quarterly`, `3month`, `6month`, `yearly`, `12month` |
| `limit` | int | 10 | 1-100 |

### Response Schema
```json
{
  "user_email": "user@example.com",
  "full_name": "John Doe",
  "team": "Engineering",
  "risk_score": 42.0,
  "base_score": 70.0,
  "consistency_factor": 0.6,
  "avg_daily_score": 35.5,
  "max_daily_score": 78.2,
  "total_incidents": 25,
  "total_blocks": 5,
  "total_quarantines": 2,
  "days_with_activity": 3,
  "min_days_required": 5,
  "period": "quarterly"
}
```

### Response Fields
| Field | Type | Description |
|-------|------|-------------|
| `risk_score` | float | **Adjusted score** (with consistency factor applied) |
| `base_score` | float | Original calculated score (before penalty) |
| `consistency_factor` | float | Multiplier (0.0 - 1.0) |
| `days_with_activity` | int | Number of days with at least 1 incident |
| `min_days_required` | int | Threshold for full score (no penalty) |

---

## 🖥️ Dashboard Implementation

### Top Risky Users Table
Location: `dashboard/app/page.tsx`

**Period Selector Options:**
- Last Week (weekly)
- Last 1 Month (monthly) 
- Last 3 Months (quarterly) ← Default
- Last 6 Months (6month)
- Last 1 Year (yearly)

**Table Columns:**
| Column | Data Field | Description |
|--------|------------|-------------|
| User | `user_email` | User's email address |
| Risk Score | `risk_score` | Adjusted score with color badge |
| Days Active | `days_with_activity` | Activity days in period |
| Total Incidents | `total_incidents` | Sum of all incidents |

**Risk Score Color Coding:**
| Score Range | Color | Level |
|-------------|-------|-------|
| 75-100 | 🔴 Red (#d32f2f) | Critical |
| 50-74 | 🟠 Orange (#f57c00) | High |
| 25-49 | 🟡 Yellow (#fbc02d) | Medium |
| 0-24 | 🟢 Green (#4caf50) | Low |

### Today's Active Users Table
- Shows 24-hour data (no consistency factor applied)
- Displays: User, Risk Score, Blocks, Incidents

---

## 📈 User Insights Modal

### Period Tabs
| Tab | Backend Period | Days |
|-----|----------------|------|
| Last 7 Days | `daily` | 7 |
| Last 2 Weeks | `weekly` | 14 |
| Last 1 Month | `monthly` | 30 |
| Last 3 Months | `quarterly` | 90 |

### Period Averages Comparison
The modal shows comparison metrics for:
- Weekly (7 days)
- Monthly (30 days)
- Quarterly (90 days)

---

## 🗃️ Database Schema

### Table: `user_daily_risk_scores`
```sql
CREATE TABLE user_daily_risk_scores (
    id SERIAL PRIMARY KEY,
    user_email VARCHAR(255) NOT NULL,
    date DATE NOT NULL,
    daily_risk_score DECIMAL(5,2),
    incident_count INT DEFAULT 0,
    avg_risk_score DECIMAL(5,2),
    max_risk_score DECIMAL(5,2),
    block_count INT DEFAULT 0,
    permit_count INT DEFAULT 0,
    quarantine_count INT DEFAULT 0,
    released_count INT DEFAULT 0,
    max_max_matches INT DEFAULT 0,
    avg_max_matches DECIMAL(10,2) DEFAULT 0,
    full_name VARCHAR(255),
    team VARCHAR(255),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    UNIQUE(user_email, date)
);

-- Index for performance
CREATE INDEX idx_user_daily_scores_date ON user_daily_risk_scores(date);
CREATE INDEX idx_user_daily_scores_email ON user_daily_risk_scores(user_email);
```

---

## 🔄 Data Flow

```
┌─────────────────┐     ┌──────────────────┐     ┌─────────────────┐
│  DLP Incidents  │────▶│ Daily Score Calc │────▶│ user_daily_     │
│  (raw_incidents)│     │ (backfill/daily) │     │ risk_scores     │
└─────────────────┘     └──────────────────┘     └────────┬────────┘
                                                          │
                        ┌──────────────────┐              │
                        │ GetTopRiskyUsers │◀─────────────┘
                        │ FromDailyScores  │
                        │ Async()          │
                        └────────┬─────────┘
                                 │
                    ┌────────────┼────────────┐
                    ▼            ▼            ▼
              ┌──────────┐ ┌──────────┐ ┌──────────┐
              │ Group by │ │ Calculate│ │ Apply    │
              │ User     │ │ Base     │ │ Consist. │
              │          │ │ Score    │ │ Factor   │
              └──────────┘ └──────────┘ └──────────┘
                                 │
                                 ▼
                        ┌──────────────────┐
                        │  Dashboard API   │
                        │  Response        │
                        └──────────────────┘
```

---

## ⚙️ Configuration

### Backend Settings
File: `DLP.RiskAnalyzer.Analyzer/Services/RiskAnalyzerService.cs`

```csharp
// Minimum days thresholds (can be adjusted)
switch (period.ToLower())
{
    case "24h":
    case "daily":
        minDaysRequired = 1;    // No penalty
        break;
    case "weekly":
        minDaysRequired = 2;    // 2+ days for full score
        break;
    case "monthly":
        minDaysRequired = 3;    // 3+ days for full score
        break;
    case "quarterly":
        minDaysRequired = 5;    // 5+ days for full score
        break;
    case "6month":
        minDaysRequired = 7;    // 7+ days for full score
        break;
    case "yearly":
        minDaysRequired = 10;   // 10+ days for full score
        break;
}
```

---

## 📝 Change Log

### v2.1 (January 2026)
- ✅ Added High Impact Alerts widget for detecting potential data exfiltration
- ✅ New endpoint: `/api/risk-trends/high-impact-alerts`
- ✅ Dashboard shows 🚨 Potential Data Exfiltration card with severity levels

### v2.0 (January 2026)
- ✅ Added consistency factor to penalize one-time events
- ✅ Added 6-month and yearly period options
- ✅ Added period selector dropdown in dashboard
- ✅ Fixed UserInsightsModal duplicate periods (4 weeks → 2 weeks)
- ✅ API response now includes `base_score`, `consistency_factor`, `min_days_required`

### v1.0 (Initial)
- Basic formula: `MIN(100, (Avg/500×50) + (Max/500×30) + MIN(20, LOG₁₀(Count+1)×10))`
- Fixed periods only (24h, quarterly)

---

## 🚨 High Impact Alerts (Data Exfiltration Detection)

### Purpose
Detects potential data exfiltration attempts that would be **penalized by the consistency factor** in the regular Top Risky Users scoring. These are single-day events with unusually high data volume.

### Detection Criteria
```
max_max_matches >= minMaxMatches (default: 50)
```

| Severity Level | Max Matches Threshold |
|----------------|----------------------|
| Critical | ≥ 500 |
| High | ≥ 200 |
| Medium | ≥ 100 |
| Low | ≥ 50 |

### Impact Score Formula
```
impact_score = MIN(100, (max_max_matches / 10) + (daily_risk_score * 0.5))
```

### API Endpoint
```
GET /api/risk-trends/high-impact-alerts
```

### Parameters
| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `days` | int | 7 | Look-back period in days |
| `minMaxMatches` | int | 100 | Minimum max_matches threshold |
| `limit` | int | 10 | Maximum number of alerts |

### Response Schema
```json
{
  "user_email": "user@example.com",
  "impact_score": 85.5,
  "max_max_matches": 650,
  "highest_risk_date": "2026-01-28",
  "daily_risk_score": 71.0,
  "incident_count": 3,
  "block_count": 1,
  "quarantine_count": 0,
  "days_with_activity": 1,
  "total_incidents_in_period": 3,
  "is_single_day_event": true,
  "severity_level": "Critical"
}
```

### Key Indicators
| Field | Meaning |
|-------|---------|
| `is_single_day_event` | True = One-time event, requires investigation |
| `severity_level` | Critical/High/Medium/Low based on data volume |
| `max_max_matches` | Number of sensitive data matches detected |

### Dashboard Widget
The "🚨 Potential Data Exfiltration" card shows:
- Alert count badge
- User email with severity indicator
- Max matches and impact score
- Date of the event
- Warning flag for single-day events

### Use Case Examples

#### Example 1: Malicious Insider
```
User: john.smith@company.com
Max Matches: 1200 (Critical)
Days Active: 1
Action: BLOCK

→ Single-day massive data transfer attempt
→ Blocked by DLP, but flagged for investigation
```

#### Example 2: Accidental Bulk Send
```
User: marketing@company.com
Max Matches: 350 (High)
Days Active: 1
Action: QUARANTINE

→ May be legitimate bulk email
→ Review quarantined content
```

### Relationship with Consistency Factor

| Scenario | Regular Score | High Impact Alert |
|----------|---------------|-------------------|
| 1 day, 500 matches | 16.6 (penalized) | ✅ Shows in widget |
| 10 days, 50 matches each | 65.0 (full score) | ❌ Not shown |
| 1 day, 30 matches | 12.4 (penalized) | ❌ Below threshold |

This ensures both **persistent threats** and **one-time exfiltration attempts** are visible to security teams.

---

## 🧪 Testing

### Test Scenarios

1. **Single-day user**: Should have low adjusted score
2. **Multi-day user**: Should have full adjusted score
3. **24h period**: No consistency penalty applied
4. **Period change**: Score should update with new period selection

### API Test Commands
```bash
# Get top users for different periods
curl "http://localhost:5062/api/risk-trends/top-users?period=24h&limit=5"
curl "http://localhost:5062/api/risk-trends/top-users?period=quarterly&limit=10"
curl "http://localhost:5062/api/risk-trends/top-users?period=yearly&limit=10"
```

---

## 📚 Related Documentation

- [DASHBOARD_VERI_AKISI_REHBERI.md](./DASHBOARD_VERI_AKISI_REHBERI.md) - Dashboard data flow
- [ENTITY_ANOMALY_CALCULATION.md](./ENTITY_ANOMALY_CALCULATION.md) - Entity anomaly detection
- [backfill-daily-risk-scores.sql](./backfill-daily-risk-scores.sql) - SQL script for historical data
