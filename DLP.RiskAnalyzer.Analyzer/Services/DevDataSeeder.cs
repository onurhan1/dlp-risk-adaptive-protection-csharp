using DLP.RiskAnalyzer.Analyzer.Data;
using DLP.RiskAnalyzer.Shared.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace DLP.RiskAnalyzer.Analyzer.Services;

/// <summary>
/// Development-only data seeder.
/// Activated when "SeedData:Enabled" = true in configuration (appsettings.Development.json).
/// Populates all major tables with realistic dummy data so the frontend can be tested
/// locally WITHOUT a Redis/DLP API connection.
///
/// On production the flag is absent → this class does nothing.
/// </summary>
public class DevDataSeeder : IDevDataSeeder
{
    private readonly AnalyzerDbContext _context;
    private readonly ILogger<DevDataSeeder> _logger;
    private readonly IConfiguration _config;
    private readonly Random _rng = new(42); // fixed seed → repeatable data

    // ─── Realistic test data pools ──────────────────────────────────────────

    private static readonly string[] Users =
    [
        "ahmet.yilmaz", "fatma.kaya", "mehmet.demir", "ayse.celik", "mustafa.sahin",
        "zeynep.ozturk", "ibrahim.arslan", "hande.kurt", "burak.yildiz", "selin.acar",
        "emre.bulut", "derya.polat", "can.erdem", "pinar.gul", "ozan.tas"
    ];

    private static readonly string[] FullNames =
    [
        "Ahmet Yılmaz", "Fatma Kaya", "Mehmet Demir", "Ayşe Çelik", "Mustafa Şahin",
        "Zeynep Öztürk", "İbrahim Arslan", "Hande Kurt", "Burak Yıldız", "Selin Acar",
        "Emre Bulut", "Derya Polat", "Can Erdem", "Pınar Gül", "Ozan Taş"
    ];

    private static readonly string[] Teams =
    [
        "Operasyonel Risk", "Bilgi Teknolojileri", "Finans", "İnsan Kaynakları",
        "Hazine", "Uyum ve Denetim", "Satış", "Hukuk"
    ];

    private static readonly string[] Departments =
    [
        "IT", "Finance", "HR", "Operations", "Treasury", "Compliance", "Sales", "Legal"
    ];

    private static readonly string[] Policies =
    [
        "Kredi Kartı Verileri", "Kişisel Veri Koruması",
        "Finansal Raporlar", "Sözleşme ve NDA", "İnsan Kaynakları Bilgileri"
    ];

    private static readonly string[] Rules =
    [
        "Kredi Kartı Numarası", "TCKN Tespiti", "IBAN Numarası",
        "Gizli Belge", "Bordro & Maaş", "Müşteri Portföyü"
    ];

    private static readonly string[] Channels =
    [
        "Email", "USB", "Cloud", "Print", "Web", "ENDPOINT_LAN", "ENDPOINT_PRINTING"
    ];

    private static readonly string[] Actions =
    [
        "BLOCK", "BLOCK", "QUARANTINE", "AUTHORIZED", "AUTHORIZED", "AUTHORIZED"  // weighted toward AUTHORIZED
    ];

    private static readonly string[] Destinations =
    [
        "gmail.com", "hotmail.com", "yahoo.com", "outlook.com",
        "USB Drive (16GB)", "OneDrive Personal", "Dropbox",
        "PRINTER-FLOOR3", "192.168.1.45"
    ];

    private static readonly string[] DataTypes =
    [
        "PII", "Financial", "Health", "Intellectual Property", "Credentials", "Personal"
    ];

    private static readonly string[] SolutionMethods =
    [
        "Kullanıcı bilgilendirildi", "Şifreleme uygulandı", "Erişim kısıtlandı",
        "Yönetici onayı alındı", "Karantinaya alındı"
    ];

    private static readonly string[] MercekStatuses =
        ["Open", "Closed", "In Progress", "Pending", "Resolved"];

    // ─── Public entry point ──────────────────────────────────────────────────

    public DevDataSeeder(
        AnalyzerDbContext context,
        ILogger<DevDataSeeder> logger,
        IConfiguration config)
    {
        _context = context;
        _logger  = logger;
        _config  = config;
    }

    /// <summary>
    /// Seeds all tables. Skips if <c>SeedData:Enabled</c> is false or DB already has
    /// enough incidents (idempotent guard via <c>SeedData:MinIncidentsBeforeSkip</c>).
    /// </summary>
    public async Task SeedAsync()
    {
        if (!_config.GetValue<bool>("SeedData:Enabled"))
        {
            _logger.LogDebug("SeedData:Enabled is false — skipping DevDataSeeder");
            return;
        }

        int minBeforeSkip = _config.GetValue<int>("SeedData:MinIncidentsBeforeSkip", 500);
        int incidentCount = await _context.Incidents.CountAsync();

        if (incidentCount >= minBeforeSkip)
        {
            _logger.LogInformation(
                "DevDataSeeder: DB already has {Count} incidents (≥ {Min}) — skipping seed",
                incidentCount, minBeforeSkip);
            return;
        }

        _logger.LogInformation("=== DevDataSeeder: Starting data seed ===");
        var sw = System.Diagnostics.Stopwatch.StartNew();

        int targetIncidents = _config.GetValue<int>("SeedData:IncidentCount", 2000);
        int targetDays      = _config.GetValue<int>("SeedData:DaysBack", 60);

        await SeedIncidentsAsync(targetIncidents, targetDays);
        await SeedUserDailyRiskScoresAsync(targetDays);
        await SeedDailySummariesAsync(targetDays);
        await SeedMercekIncidentsAsync(200, targetDays);
        await SeedSystemSettingsAsync();

        sw.Stop();
        _logger.LogInformation(
            "=== DevDataSeeder: Finished in {Elapsed:F1}s ===", sw.Elapsed.TotalSeconds);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Table seeders
    // ─────────────────────────────────────────────────────────────────────────

    private async Task SeedIncidentsAsync(int count, int daysBack)
    {
        _logger.LogInformation("Seeding {Count} incidents over {Days} days...", count, daysBack);

        // Clear first so re-seeding produces consistent data
        if (await _context.Incidents.AnyAsync())
        {
            _context.Incidents.RemoveRange(_context.Incidents);
            await _context.SaveChangesAsync();
        }

        var incidents = new List<Incident>(count);
        var usedTimestamps = new HashSet<long>();

        for (int i = 0; i < count; i++)
        {
            int userIdx   = _rng.Next(Users.Length);
            int ruleIdx   = _rng.Next(Rules.Length);
            int policyIdx = _rng.Next(Policies.Length);
            int chanIdx   = _rng.Next(Channels.Length);
            int actionIdx = _rng.Next(Actions.Length);
            int destIdx   = _rng.Next(Destinations.Length);

            var action      = Actions[actionIdx];
            var channel     = Channels[chanIdx];
            var maxMatches  = _rng.Next(1, 500);
            var severity    = _rng.Next(1, 6);
            var dataSens    = _rng.Next(0, 10);

            // Build a unique timestamp (microsecond resolution prevents duplicate composite key)
            long ticks;
            do
            {
                var hoursBack = _rng.Next(0, daysBack * 24);
                ticks = DateTime.UtcNow.AddHours(-hoursBack).AddSeconds(_rng.Next(0, 3600)).Ticks;
            }
            while (!usedTimestamps.Add(ticks));

            var ts = new DateTime(ticks, DateTimeKind.Utc);

            var violationJson = BuildViolationTriggersJson(Policies[policyIdx], Rules[ruleIdx], maxMatches);

            incidents.Add(new Incident
            {
                Id                = i + 1,
                UserEmail         = $"{Users[userIdx]}@company.com.tr",
                LoginName         = Users[userIdx],
                FullName          = FullNames[userIdx],
                // Spread the 15 users over 5 departments (3 each) rather than 8 (1-2 each), so the
                // isolation forest's MinDeptSize=3 peer z-scores engage instead of silently
                // falling back to the whole population.
                Team              = Teams[userIdx % 5],
                Department        = Departments[userIdx % 5],
                Severity          = severity,
                DataType          = DataTypes[_rng.Next(DataTypes.Length)],
                Timestamp         = ts,
                Policy            = Policies[policyIdx],
                RuleName          = Rules[ruleIdx],
                Channel           = channel,
                Action            = action,
                Destination       = Destinations[destIdx],
                MaxMatches        = maxMatches,
                RepeatCount       = _rng.Next(0, 10),
                DataSensitivity   = dataSens,
                ViolationTriggers = violationJson,
                HostName          = $"TRIST-L-{_rng.Next(1000, 9999)}",
                EmailAddress      = $"{Users[userIdx]}@company.com.tr",
                FileName          = _rng.Next(2) == 0 ? $"document_{_rng.Next(100)}.xlsx" : null,
                RiskScore         = CalculateDummyRiskScore(maxMatches, channel, action),
                IsRemediated      = _rng.Next(10) < 2,   // 20% remediated
            });

            // Batch insert every 500 rows to avoid memory pressure
            if (incidents.Count >= 500)
            {
                await _context.Incidents.AddRangeAsync(incidents);
                await _context.SaveChangesAsync();
                incidents.Clear();
            }
        }

        if (incidents.Count > 0)
        {
            await _context.Incidents.AddRangeAsync(incidents);
            await _context.SaveChangesAsync();
        }

        var personas = BuildBehaviorPersonas(count + 1, usedTimestamps);
        await _context.Incidents.AddRangeAsync(personas);
        await _context.SaveChangesAsync();

        _logger.LogInformation("  → Incidents seeded: {Count} (+{Personas} behavior personas)", count, personas.Count);
    }

    /// <summary>
    /// Three deterministic personas that exercise the isolation-forest paths the uniform generator
    /// above never reaches. Without them every seeded user has a thick, statistically identical
    /// baseline, so the thin-baseline and quiet-user branches are dead in local testing.
    /// </summary>
    private List<Incident> BuildBehaviorPersonas(int firstId, HashSet<long> usedTimestamps)
    {
        var now = DateTime.UtcNow;
        var incidents = new List<Incident>();
        var id = firstId;

        Incident Make(string login, string dept, string team, DateTime ts, string channel,
                      string action, string destination, int severity, int sensitivity, int maxMatches)
        {
            // Preserve the composite-key invariant the main loop maintains.
            var stamp = ts;
            while (!usedTimestamps.Add(stamp.Ticks))
                stamp = stamp.AddTicks(1);

            var policy = Policies[maxMatches % Policies.Length];
            var rule = Rules[severity % Rules.Length];

            return new Incident
            {
                Id                = id++,
                UserEmail         = $"{login}@company.com.tr",
                LoginName         = login,
                FullName          = login,
                Team              = team,
                Department        = dept,
                Severity          = severity,
                DataType          = DataTypes[maxMatches % DataTypes.Length],
                Timestamp         = stamp,
                Policy            = policy,
                RuleName          = rule,
                Channel           = channel,
                Action            = action,
                Destination       = destination,
                MaxMatches        = maxMatches,
                RepeatCount       = 1,
                DataSensitivity   = sensitivity,
                ViolationTriggers = BuildViolationTriggersJson(policy, rule, maxMatches),
                HostName          = $"TRIST-L-{1000 + (id % 8999)}",
                EmailAddress      = $"{login}@company.com.tr",
                RiskScore         = CalculateDummyRiskScore(maxMatches, channel, action),
                IsRemediated      = false
            };
        }

        // 1) sizinti.test — 53 quiet days, then a hard break inside the scoring window:
        //    top sensitivity, off-hours, and a channel nobody else in the org uses.
        for (var d = 60; d > 7; d -= 2)
            incidents.Add(Make("sizinti.test", "Treasury", "Hazine",
                now.AddDays(-d).Date.AddHours(11), "Email", "BLOCK", "partner.com.tr",
                severity: 2, sensitivity: 1, maxMatches: 20));

        for (var d = 6; d >= 0; d--)
        {
            incidents.Add(Make("sizinti.test", "Treasury", "Hazine",
                now.AddDays(-d).Date.AddHours(2), "SSH_SCP", "AUTHORIZED", "185.42.19.7",
                severity: 5, sensitivity: 9, maxMatches: 480));
            incidents.Add(Make("sizinti.test", "Treasury", "Hazine",
                now.AddDays(-d).Date.AddHours(23), "Cloud", "AUTHORIZED", "Dropbox",
                severity: 5, sensitivity: 9, maxMatches: 420));
        }

        // 2) yeni.calisan — no history at all. Every self feature is unavailable, which must
        //    surface as low confidence rather than as a personal-baseline "reason".
        for (var d = 6; d >= 0; d--)
            incidents.Add(Make("yeni.calisan", "IT", "Bilgi Teknolojileri",
                now.AddDays(-d).Date.AddHours(14), "USB", "AUTHORIZED", "USB Drive (16GB)",
                severity: 4, sensitivity: 7, maxMatches: 260));

        // 3) sessizlesen — heavy, stable history and then near-silence. An isolation forest
        //    legitimately isolates this tail too; the explanation must say so without painting
        //    it green.
        for (var d = 60; d > 7; d--)
            incidents.Add(Make("sessizlesen", "Finance", "Finans",
                now.AddDays(-d).Date.AddHours(9 + d % 8), "Web", "AUTHORIZED", "onedrive.com",
                severity: 3, sensitivity: 6, maxMatches: 300));

        incidents.Add(Make("sessizlesen", "Finance", "Finans",
            now.AddDays(-3).Date.AddHours(10), "Email", "BLOCK", "partner.com.tr",
            severity: 1, sensitivity: 1, maxMatches: 5));

        return incidents;
    }

    private async Task SeedUserDailyRiskScoresAsync(int daysBack)
    {
        _logger.LogInformation("Seeding user_daily_risk_scores...");

        if (await _context.UserDailyRiskScores.AnyAsync())
        {
            _context.UserDailyRiskScores.RemoveRange(_context.UserDailyRiskScores);
            await _context.SaveChangesAsync();
        }

        var today  = DateOnly.FromDateTime(DateTime.UtcNow);
        var scores = new List<UserDailyRiskScore>();

        for (int u = 0; u < Users.Length; u++)
        {
            // Generate a baseline score with random volatility per user
            double baseScore = 20 + _rng.NextDouble() * 70;

            for (int d = 0; d < daysBack; d++)
            {
                // Skip a few days randomly (simulates no incidents on that day)
                if (_rng.Next(5) == 0) continue;

                var date          = today.AddDays(-d);
                var dailyScore    = Math.Min(100, Math.Max(0, baseScore + (_rng.NextDouble() - 0.4) * 25));
                var incidentCount = _rng.Next(1, 15);

                scores.Add(new UserDailyRiskScore
                {
                    UserEmail      = $"{Users[u]}@company.com.tr",
                    FullName       = FullNames[u],
                    Team           = Teams[u % Teams.Length],
                    Date           = date,
                    DailyRiskScore = (int)Math.Round(dailyScore),
                    MaxRiskScore   = (int)Math.Min(100, dailyScore + _rng.NextDouble() * 15),
                    AvgRiskScore   = (int)Math.Max(0, dailyScore - _rng.NextDouble() * 10),
                    IncidentCount  = incidentCount,
                    CreatedAt      = DateTime.UtcNow
                });
            }
        }

        await _context.UserDailyRiskScores.AddRangeAsync(scores);
        await _context.SaveChangesAsync();
        _logger.LogInformation("  → UserDailyRiskScores seeded: {Count}", scores.Count);
    }

    private async Task SeedDailySummariesAsync(int daysBack)
    {
        _logger.LogInformation("Seeding daily_summaries...");

        if (await _context.DailySummaries.AnyAsync())
        {
            _context.DailySummaries.RemoveRange(_context.DailySummaries);
            await _context.SaveChangesAsync();
        }

        var today     = DateOnly.FromDateTime(DateTime.UtcNow);
        var summaries = new List<DailySummary>();

        for (int d = 0; d < daysBack; d++)
        {
            var date = today.AddDays(-d);
            summaries.Add(new DailySummary
            {
                Date                = date,
                TotalIncidents      = _rng.Next(20, 120),
                HighRiskCount       = _rng.Next(2, 20),
                AvgRiskScore        = 30 + _rng.NextDouble() * 50,
                UniqueUsers         = _rng.Next(5, Users.Length),
                DepartmentsAffected = _rng.Next(2, Departments.Length)
            });
        }

        await _context.DailySummaries.AddRangeAsync(summaries);
        await _context.SaveChangesAsync();
        _logger.LogInformation("  → DailySummaries seeded: {Count}", summaries.Count);
    }

    private async Task SeedMercekIncidentsAsync(int count, int daysBack)
    {
        _logger.LogInformation("Seeding mercek incidents...");

        if (await _context.MercekIncidents.AnyAsync())
        {
            _context.MercekIncidents.RemoveRange(_context.MercekIncidents);
            await _context.SaveChangesAsync();
        }

        var incidents = new List<MercekIncident>();

        for (int i = 0; i < count; i++)
        {
            int userIdx = _rng.Next(Users.Length);
            var openDate = DateTime.UtcNow.AddDays(-_rng.Next(0, daysBack));
            var isClosed = _rng.Next(2) == 0;

            incidents.Add(new MercekIncident
            {
                IncidentId            = 20000 + i,
                UserName              = FullNames[userIdx],
                AssignedUserCode      = Users[userIdx],
                StatusId              = MercekStatuses[_rng.Next(MercekStatuses.Length)],
                FlowStatusId          = isClosed ? "CLOSED" : "OPEN",
                SummaryDescription    = $"Güvenlik ihlali tespiti — {Policies[_rng.Next(Policies.Length)]}",
                IncidentDescription   = $"{Rules[_rng.Next(Rules.Length)]} kuralı ihlal edildi. Kullanıcı {Users[userIdx]} tarafından hassas veri transferi gerçekleştirildi.",
                ImpactId              = new[] { "Low", "Medium", "High" }[_rng.Next(3)],
                PriorityId            = new[] { "P1", "P2", "P3", "P4" }[_rng.Next(4)],
                AssignmentGroupId     = _rng.Next(10, 50),
                CategoryId            = _rng.Next(1, 10),
                OpenDate              = openDate,
                CloseDate             = isClosed ? openDate.AddDays(_rng.Next(1, 10)) : null,
                StartDate             = openDate,
                SolutionDescription   = isClosed ? $"Olayın çözümü: {SolutionMethods[_rng.Next(SolutionMethods.Length)]}" : null,
                SolutionMethod        = isClosed ? SolutionMethods[_rng.Next(SolutionMethods.Length)] : null,
                RequestTypeId         = "Service Request",
                CallTypeId            = "Incident",
                SystemDate            = openDate,
                DefinitionCategoryId  = _rng.Next(1, 5),
                DefinitionCategoryPath = "IT > Security > DLP"
            });
        }

        await _context.MercekIncidents.AddRangeAsync(incidents);
        await _context.SaveChangesAsync();
        _logger.LogInformation("  → MercekIncidents seeded: {Count}", count);
    }

    private async Task SeedSystemSettingsAsync()
    {
        _logger.LogInformation("Seeding system settings...");

        // Only add if not already present (non-destructive)
        var existingKeys = await _context.SystemSettings.Select(s => s.Key).ToListAsync();
        var defaults = new Dictionary<string, string>
        {
            ["dlp.risk_threshold_high"]   = "50",
            ["dlp.risk_threshold_medium"] = "25",
            ["dlp.batch_size"]            = "500",
            ["email.enabled"]             = "false",
            ["ai.enabled"]                = "false",
            ["seed_mode"]                 = "true"
        };

        foreach (var (key, value) in defaults)
        {
            if (!existingKeys.Contains(key))
            {
                _context.SystemSettings.Add(new SystemSetting
                {
                    Key       = key,
                    Value     = value,
                    UpdatedAt = DateTime.UtcNow
                });
            }
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("  → SystemSettings seeded");
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static string BuildViolationTriggersJson(string policy, string rule, int maxMatches)
    {
        var payload = new[]
        {
            new
            {
                PolicyName  = policy,
                RuleName    = rule,
                Classifiers = new[]
                {
                    new { ClassifierName = rule, NumberMatches = maxMatches }
                }
            }
        };
        return JsonSerializer.Serialize(payload);
    }

    private static int CalculateDummyRiskScore(int maxMatches, string channel, string action)
    {
        // Simplified version of CalculateRiskScoreV2 for seeding
        int tier = maxMatches switch
        {
            <= 15  => 7,
            <= 30  => 14,
            <= 50  => 25,
            <= 100 => 40,
            <= 250 => 55,
            <= 500 => 70,
            _      => 85
        };

        double chanMult = channel switch
        {
            "ENDPOINT_LAN"      => 0.2,
            "ENDPOINT_PRINTING" => 0.4,
            _                   => 1.0
        };

        double actionMult = action switch
        {
            "BLOCK" or "QUARANTINE" => 1.0,
            "RELEASED"              => 0.0,
            _                       => 0.2
        };

        return (int)Math.Min(100, (tier * chanMult + 5) * actionMult);
    }
}
