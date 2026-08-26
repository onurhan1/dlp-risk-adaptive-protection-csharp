# RADAR Project Navigation and Cleanup Audit / RADAR Proje Navigasyon ve Temizlik Denetimi

This document is a practical map for engineers working on RADAR. It answers two questions:

1. When a behavior is broken, which files should be inspected first?
2. Which files are likely historical, generated, or unused and should be reviewed before cleanup?

Bu doküman RADAR üzerinde çalışan geliştiriciler için pratik bir mimari özeti ve temizlik denetimidir. İki temel soruyu yanıtlar:

1. Bir sorun olduğunda hangi dosyalar incelenmelidir?
2. Hangi dosyalar üretilmiş, tarihsel veya muhtemelen kullanım dışıdır ve temizlik öncesi incelenmelidir?

Audit date / Denetim tarihi: 2026-08-26

For the file-by-file learning path, frontend route map and controller/service/data traces, start with [the English HTML guide](project-file-atlas.en.html). This document focuses on the shorter architecture summary and repository cleanup audit.

Dosya dosya öğrenme rotası, frontend ekran haritası ve controller/service/data izleri için önce [Türkçe HTML rehberini](proje-dosya-atlasi.tr.html) okuyun. Bu doküman daha kısa mimari özeti ve repository temizlik denetimine odaklanır.

## 1. Runtime Architecture

RADAR has four important runtime parts:

| Part | Responsibility | Entry point |
| --- | --- | --- |
| Web UI | Next.js user interface | `dashboard/app/layout.tsx`, route-level `page.tsx` files |
| API | ASP.NET API, business rules, background jobs | `DLP.RiskAnalyzer.Analyzer/Program.cs` |
| Collector | Pulls DLP events and feeds the analyzer pipeline | `DLP.RiskAnalyzer.Collector/Program.cs` |
| Shared | Common domain models and risk helpers | `DLP.RiskAnalyzer.Shared` |

PostgreSQL is the primary RADAR database. Redis is used by the collector/analyzer pipeline. LDAP, IMAP/SMTP, DLP API and AI services are integrations configured through the API settings.

### API startup sequence

1. `DLP.RiskAnalyzer.Analyzer/Program.cs` builds the application.
2. `Auth/AuthBootstrapper.cs` loads JWT and protected configuration values.
3. `Extensions/ServiceCollectionExtensions.cs` registers repositories, services, integrations and infrastructure.
4. `Extensions/WebApplicationExtensions.cs` configures middleware, authentication, CORS, Swagger and migrations.
5. `Program.cs` starts four background services:
   - `AnalyzerBackgroundService`
   - `PlaybookSchedulerService`
   - `ScheduledJobBackgroundService`
   - `InvestigationMailAutomationBackgroundService`

## 2. Where To Look First

Follow the path from UI to API instead of changing a service in isolation:

`dashboard route/component -> /api/... request -> controller -> injected interface -> service -> DbContext/model/integration`

| Problem area | Start in the UI | API and service path | Data/configuration |
| --- | --- | --- | --- |
| Login, JWT, local/LDAP users | `dashboard/app/login/page.tsx`, `dashboard/app/user-management/page.tsx` | `Controllers/AuthController.cs`, `Controllers/UsersController.cs`, `Services/UserService.cs` | `Auth/AuthBootstrapper.cs`, `Data/UserEntity.cs` |
| LDAP settings, lookup and login attributes | `dashboard/app/settings/page.tsx`, `components/settings/UsersTab.tsx` | `Controllers/DirectorySettingsController.cs`, `Services/DirectorySettingsService.cs` | `Models/DirectorySettingsModels.cs`, `Data/SystemSetting.cs` |
| SMTP, IMAP inbox and attachments | `dashboard/app/settings/page.tsx`, `dashboard/app/investigation/mailbox/page.tsx`, `components/investigation/MailBodyView.tsx` | `Controllers/EmailConfigurationController.cs`, `Controllers/DirectorySettingsController.cs`, `Services/EmailService.cs` | `Models/EmailSettingsModels.cs`, protected system settings |
| Investigation timeline and manual mail | `dashboard/app/investigation/page.tsx`, `components/InvestigationTimeline.tsx`, `components/investigation/SendMailModal.tsx` | `Controllers/InvestigationController.cs`, `Controllers/InvestigationQueriesController.cs` | `Models/InvestigationQueryRecord.cs`, `Data/InvestigationQuerySchema.cs` |
| Mail templates and template fields | `dashboard/app/investigation/mail-templates/page.tsx`, `components/investigation/MailTemplateManager.tsx` | `Controllers/MailTemplatesController.cs`, `Helpers/PlaybookMailRenderer.cs` | `Models/MailTemplate.cs` |
| Agentic workflows, report output and approval | `dashboard/app/investigation/agentic-workflows/page.tsx`, `app/investigation/agentic-workflows/[id]/page.tsx`, `components/investigation/playbook/*` | `Controllers/PlaybooksController.cs`, `Services/PlaybookEngine.cs`, `Services/PlaybookSchedulerService.cs` | `Models/Playbook.cs`, `Models/PlaybookGraph.cs`, `Data/PlaybookSchema.cs` |
| Service mailbox automation and replies | `dashboard/app/investigation/mailbox/page.tsx`, `dashboard/app/investigation/queries/page.tsx` | `Services/InvestigationMailAutomationBackgroundService.cs`, `Services/InvestigationMailAutomationService.cs` | `Models/InvestigationQueryRecord.cs`, inbound-mail schema |
| DLP incident ingestion and mapping | Dashboard incident pages and charts | `Controllers/IncidentsController.cs`, `Controllers/CollectorController.cs`, `Controllers/RiskIncidentsController.cs` | `DLP.RiskAnalyzer.Collector/Services/*`, `Collector/Mappers/IncidentMapper.cs`, `Shared/Models/Incident.cs` |
| Risk score, AI behavioral analysis and anomaly results | `dashboard/app/ai-behavioral/*`, `dashboard/app/page.tsx` | `Controllers/RiskController.cs`, `Controllers/AIBehavioralController.cs`, `Services/RiskScoringService.cs`, `Services/BehaviorEngineService*.cs` | `Shared/Models/UserDailyRiskScore.cs`, `Models/AIBehavioralAnalysis.cs` |
| Policy inventory and exceptions | `dashboard/app/exceptions/policy-inventory/page.tsx`, `dashboard/app/exceptions/exception-list/*` | `Controllers/PolicyInventoryController.cs`, `Controllers/PolicyExceptionsController.cs`, `Services/PolicyInventoryService.cs` | `Shared/Models/PolicyInventory.cs`, migrations in `database/migrations` |
| Scheduled jobs | `dashboard/app/scheduled-jobs/page.tsx` | `Controllers/ScheduledJobsController.cs`, `Services/ScheduledJobService.cs`, `Services/ScheduledJobBackgroundService.cs` | `Models/ScheduledJob.cs`, `Data/ScheduledJobSchema.cs` |

## 3. Frontend Route Index

The frontend is App Router based. Every `dashboard/app/**/page.tsx` is a route.

| Route group | Primary routes |
| --- | --- |
| Dashboard and AI | `/`, `/ai-behavioral`, `/ai-behavioral/overview`, `/ai-behavioral/ai-model`, `/ai-behavioral/rule-based`, `/ai-settings` |
| Investigation | `/investigation`, `/investigation/weekly-review`, `/investigation/queries`, `/investigation/mailbox`, `/investigation/mail-templates`, `/investigation/agentic-workflows` |
| Administration | `/settings`, `/user-management`, `/scheduled-jobs`, `/logs`, `/release-notes` |
| Exceptions | `/exceptions/*`, including policy inventory, permanent/removal lists, domain features and Mercek analysis |

Navigation visibility is controlled mainly by `dashboard/components/Sidebar.tsx` and `dashboard/components/Navigation.tsx`.

## 4. Backend Boundaries

### API and dependency injection

- `Program.cs`: application startup and hosted services.
- `Extensions/ServiceCollectionExtensions.cs`: the definitive dependency-injection registry. When a constructor dependency fails, inspect this file first.
- `Extensions/WebApplicationExtensions.cs`: HTTP pipeline, authorization and migration startup.
- `Data/AnalyzerDbContext.cs`: definitive EF Core table map. New persistent entities generally start here plus a migration.

### Controllers

Controllers are HTTP boundary files. They should validate/request-route and delegate; domain behavior normally belongs to a service. API route ownership can be found with:

```powershell
rg -n "\[Route|\[Http" DLP.RiskAnalyzer.Analyzer/Controllers
```

### Services and models

- `Services/I*.cs`: service contracts.
- `Services/*.cs`: domain/integration implementations.
- `Models/*.cs` and `DLP.RiskAnalyzer.Shared/Models/*.cs`: transport/domain structures.
- `Data/*Schema.cs`: idempotent schema helpers for features that are not introduced through the normal EF migration chain.
- `Migrations/*`: versioned EF database migrations. Do not edit an already-applied migration; add a new one.

## 5. Workflow and Mail Flow

This is the preferred model for scheduled/custom reports.

1. A workflow is saved by `PlaybooksController` as `Playbook.GraphJson`.
2. `PlaybookGraph.cs` defines graph shape and node configuration.
3. `PlaybookEngine.cs` validates nodes, reads incidents, creates report data and queues/sends mail.
4. `PlaybookSchedulerService.cs` starts enabled scheduled workflows.
5. `InvestigationMailAutomationBackgroundService.cs` scans the IMAP inbox.
6. `InvestigationMailAutomationService.cs` interprets replies, report requests and reminders.
7. `PlaybookMailRenderer.cs` expands mail-template fields such as `{{tam_ad}}`, `{{destination}}` and `{{olay_tarihi}}`.

For a workflow report request by email, configure a unique `report_request_keywords` value on the workflow. The mailbox service finds that workflow and sends its report to the requesting user. Avoid reusing the same keyword in more than one workflow.

## 6. Fast Debugging Checklist

1. Reproduce the issue and identify the visible route.
2. Find the UI API call with `rg -n "/api/" dashboard/app dashboard/components`.
3. Open the matching controller route.
4. Follow the injected interface to its implementation via `ServiceCollectionExtensions.cs`.
5. Check the corresponding entity/model and database table in `AnalyzerDbContext.cs`.
6. For configuration failures, inspect the saved setting through the settings API; secrets are protected and may not be visible in plain text.
7. For scheduled behavior, inspect the relevant background service and its run-history table before changing cron or mail logic.

## 7. Repository Hygiene Audit

The following categories are not part of the normal web application runtime. This is an audit, not a deletion instruction.

### Confirmed cleanup candidates

| Item | Evidence | Recommended action |
| --- | --- | --- |
| Root `build*.txt`, `build.log`, `build_errors*.txt`, `build_output.txt` | Historical command output; not runtime input | Remove from Git and retain only in CI artifacts if needed |
| `DLP.RiskAnalyzer.Analyzer/*.log`, `err.txt`, `build_*.txt`, `Services/methods.txt` | Generated diagnostic/build output | Remove from Git; ignore future logs |
| `DLP.RiskAnalyzer.Tests/test_err.txt` | Generated test error output | Remove from Git; ignore future test output |
| `Politika_Envanteri/~$maskeli_politika_envanteri.xlsx` | Microsoft Office temporary lock file | Remove from Git immediately |
| `dashboard/next-dev-investigation.*.log` | Local Next.js development output | Remove from Git; ignore future logs |
| `dashboard/lib/userUtils.ts` | No imports/references found in dashboard source | Verify once in a clean build, then remove |
| `IScheduledReportService.cs` and `ScheduledReportService.cs` | Only found in their own files and DI registration after workflow-based reporting | Regression-test reporting, then remove both files and their DI registration if no legacy endpoint is intentionally retained |

### Likely legacy or separate tools - owner decision required

| Area | Current evidence | Recommendation |
| --- | --- | --- |
| `DLP.RiskAnalyzer.Dashboard` | WPF desktop project in solution; the active UI is Next.js under `dashboard` | Mark as legacy or assign an owner. Remove from solution/repository only after confirming no desktop deployment exists |
| `figma/` | Standalone Vite/Figma prototype; no dependency from `dashboard` | Move to a design/prototype repository or archive after design-owner approval |
| `ExcelTest/` | Standalone console project and not part of the solution | Move to `tools/` or remove after its Excel validation purpose is no longer needed |
| `DLP.RiskAnalyzer.Importer` | Historical incident importer console application | Keep as an operational tool only if backfills are still supported; otherwise archive with runbook |
| `DLP.RiskAnalyzer.RawDumper` | Standalone raw data export console application | Keep only with a documented operational use case |
| `duzenleme_dosyalari/` | UI screenshots/reference images | Move to design documentation or issue attachments, not production source |
| `Politika_Envanteri/` scripts and spreadsheets | Policy-import preparation materials | Move to a versioned data/import-tools area if still used |

### Generated or large data that needs classification

| Area | Risk | Recommendation |
| --- | --- | --- |
| `database/*.csv`, `database/user_daily/*.csv` | Repository size and possible sensitive data retention | Keep only explicitly approved sanitized fixtures; move operational exports outside Git |
| `DLP.RiskAnalyzer.Analyzer/reports/*.pdf` | Generated report output committed as source | Remove from Git unless deliberately used as sanitized test fixtures |
| `dashboard/libraries-bundle.txt`, `dashboard/offline-packages/*` | Large offline dependency cache | Keep only if offline deployment depends on it; document restore/install procedure and exclude generated listings |
| Root analysis files such as `hardcoded_audit.txt`, `line_counts.txt`, `ras_methods.txt` | Historical one-off analysis output | Convert durable findings to `docs/`; remove raw outputs |

## 8. Safe Cleanup Order

1. Add ignore rules for logs, build output and Office temporary files.
2. Remove confirmed generated files from Git in a separate commit.
3. Build API, tests and dashboard.
4. Remove confirmed dead code one unit at a time, beginning with `userUtils.ts` and the legacy scheduled-report service after workflow regression checks.
5. Decide ownership/retention for WPF, Figma and console utilities in a separate architectural decision.
6. Move sanitized fixtures and operational exports to clearly named `samples/` or `tools/` locations.

Never delete a service solely because it has no direct UI caller: background services, reflection, database-driven workflows and operational consoles can be invoked indirectly. The evidence column above states what was checked and where manual confirmation is still required.

## 9. Keeping This Guide Useful

When a new feature is added, update this document in the same pull request with:

- the visible route;
- the API controller and service owner;
- persistent entities/settings it writes;
- background or scheduled behavior, if any;
- any new external integration and operational dependency.

This keeps AI-assisted development reviewable: an engineer can start with the user-visible behavior, trace one explicit path to the implementation, and avoid editing an unrelated subsystem.
