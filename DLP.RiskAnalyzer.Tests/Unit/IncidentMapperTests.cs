using DLP.RiskAnalyzer.Collector.Mappers;
using DLP.RiskAnalyzer.Collector.Services;
using FluentAssertions;

namespace DLP.RiskAnalyzer.Tests.Unit;

/// <summary>
/// Unit tests for IncidentMapper — validates the data mapping from DLP API models
/// to internal Incident models, including user fallback logic, MaxMatches calculation,
/// and team/full name extraction from the Manager field.
/// </summary>
public class IncidentMapperTests
{
    private static DLPIncident MakeDLPIncident(
        int id = 1,
        string? loginName = "testuser",
        string? emailAddress = "test@company.com",
        string? hostName = "WS-001",
        string? manager = null,
        string? department = "IT",
        string? channel = "Email",
        string? action = "BLOCK",
        string? severityString = "HIGH",
        List<DLPViolationTrigger>? triggers = null)
    {
        return new DLPIncident
        {
            Id = id,
            SeverityString = severityString,
            Source = new DLPIncidentSource
            {
                LoginName = loginName,
                EmailAddress = emailAddress,
                HostName = hostName,
                Manager = manager,
                Department = department
            },
            Channel = channel,
            Action = action,
            ViolationTriggers = triggers,
            IncidentTimeString = "15/03/2026 10:30:00"
        };
    }

    // ─── User Fallback Logic ─────────────────────────────────────────────────

    [Fact]
    public void MapFromDLPIncident_WithLoginName_UsesLoginNameAsUser()
    {
        var dlp = MakeDLPIncident(loginName: "john.doe", emailAddress: "john@company.com");
        var result = IncidentMapper.MapFromDLPIncident(dlp);
        result.UserEmail.Should().Be("john.doe");
    }

    [Fact]
    public void MapFromDLPIncident_NoLoginName_FallsBackToEmail()
    {
        var dlp = MakeDLPIncident(loginName: null, emailAddress: "john@company.com");
        var result = IncidentMapper.MapFromDLPIncident(dlp);
        result.UserEmail.Should().Be("john@company.com");
    }

    [Fact]
    public void MapFromDLPIncident_NoLoginNameNoEmail_FallsBackToHostName()
    {
        var dlp = MakeDLPIncident(loginName: null, emailAddress: null, hostName: "WS-FINANCE-01");
        var result = IncidentMapper.MapFromDLPIncident(dlp);
        result.UserEmail.Should().Be("WS-FINANCE-01");
    }

    [Fact]
    public void MapFromDLPIncident_AllEmpty_ReturnsUnknown()
    {
        var dlp = MakeDLPIncident(loginName: null, emailAddress: null, hostName: null);
        var result = IncidentMapper.MapFromDLPIncident(dlp);
        result.UserEmail.Should().Be("unknown");
    }

    [Fact]
    public void MapFromDLPIncident_EmptyStrings_ReturnsUnknown()
    {
        var dlp = MakeDLPIncident(loginName: "", emailAddress: "", hostName: "");
        var result = IncidentMapper.MapFromDLPIncident(dlp);
        result.UserEmail.Should().Be("unknown");
    }

    // ─── MaxMatches Calculation ──────────────────────────────────────────────

    [Fact]
    public void MapFromDLPIncident_WithClassifiers_CalculatesMaxMatches()
    {
        var triggers = new List<DLPViolationTrigger>
        {
            new()
            {
                PolicyNameSnake = "DLP-Policy",
                RuleNameSnake = "SSN Rule",
                Classifiers = new List<DLPClassifier>
                {
                    new() { NumberMatchesSnake = 5 },
                    new() { NumberMatchesSnake = 25 },
                    new() { NumberMatchesSnake = 3 }
                }
            }
        };

        var dlp = MakeDLPIncident(triggers: triggers);
        var result = IncidentMapper.MapFromDLPIncident(dlp);
        result.MaxMatches.Should().Be(25);
    }

    [Fact]
    public void MapFromDLPIncident_NoTriggers_MaxMatchesIsZero()
    {
        var dlp = MakeDLPIncident(triggers: null);
        var result = IncidentMapper.MapFromDLPIncident(dlp);
        result.MaxMatches.Should().Be(0);
    }

    [Fact]
    public void MapFromDLPIncident_EmptyClassifiers_MaxMatchesIsZero()
    {
        var triggers = new List<DLPViolationTrigger>
        {
            new()
            {
                PolicyNameSnake = "DLP-Policy",
                RuleNameSnake = "Rule1",
                Classifiers = new List<DLPClassifier>()
            }
        };

        var dlp = MakeDLPIncident(triggers: triggers);
        var result = IncidentMapper.MapFromDLPIncident(dlp);
        result.MaxMatches.Should().Be(0);
    }

    // ─── Manager → FullName / Team Parsing ───────────────────────────────────

    [Fact]
    public void MapFromDLPIncident_WithManager_ParsesFullName()
    {
        var dlp = MakeDLPIncident(manager: "John Doe / DIV-Finance Team");
        var result = IncidentMapper.MapFromDLPIncident(dlp);
        result.FullName.Should().Be("John Doe");
    }

    [Fact]
    public void MapFromDLPIncident_WithManagerAndDash_ParsesTeam()
    {
        var dlp = MakeDLPIncident(manager: "Jane Smith / DEPT-Risk Management");
        var result = IncidentMapper.MapFromDLPIncident(dlp);
        result.Team.Should().Be("Risk Management");
    }

    [Fact]
    public void MapFromDLPIncident_WithManagerNoDash_ParsesTeamAsIs()
    {
        var dlp = MakeDLPIncident(manager: "Alex Brown / Marketing");
        var result = IncidentMapper.MapFromDLPIncident(dlp);
        result.Team.Should().Be("Marketing");
    }

    [Fact]
    public void MapFromDLPIncident_NoManager_FullNameAndTeamAreNull()
    {
        var dlp = MakeDLPIncident(manager: null);
        var result = IncidentMapper.MapFromDLPIncident(dlp);
        result.FullName.Should().BeNull();
        result.Team.Should().BeNull();
    }

    // ─── Core Field Mapping ──────────────────────────────────────────────────

    [Fact]
    public void MapFromDLPIncident_MapsAllCoreFields()
    {
        var dlp = MakeDLPIncident(
            id: 42,
            channel: "USB",
            action: "QUARANTINE",
            severityString: "CRITICAL",
            department: "Finance");

        var result = IncidentMapper.MapFromDLPIncident(dlp);

        result.Id.Should().Be(42);
        result.Channel.Should().Be("USB");
        result.Action.Should().Be("QUARANTINE");
        result.Severity.Should().Be(4); // CRITICAL = 4
        result.Department.Should().Be("Finance");
    }

    // ─── ViolationTriggers JSON Serialization ────────────────────────────────

    [Fact]
    public void MapFromDLPIncident_WithTriggers_SerializesToJson()
    {
        var triggers = new List<DLPViolationTrigger>
        {
            new()
            {
                PolicyNameSnake = "PII Protection",
                RuleNameSnake = "SSN Detection",
                Classifiers = new List<DLPClassifier>
                {
                    new() { ClassifierNameSnake = "SSN", NumberMatchesSnake = 10 }
                }
            }
        };

        var dlp = MakeDLPIncident(triggers: triggers);
        var result = IncidentMapper.MapFromDLPIncident(dlp);

        result.ViolationTriggers.Should().NotBeNull();
        result.ViolationTriggers.Should().Contain("PII Protection");
        result.ViolationTriggers.Should().Contain("SSN Detection");
    }

    [Fact]
    public void MapFromDLPIncident_WithTriggers_ExtractsRuleName()
    {
        var triggers = new List<DLPViolationTrigger>
        {
            new() { RuleNameSnake = "Rule A" },
            new() { RuleNameSnake = "Rule B" },
            new() { RuleNameSnake = "Rule A" } // duplicate
        };

        var dlp = MakeDLPIncident(triggers: triggers);
        var result = IncidentMapper.MapFromDLPIncident(dlp);

        result.RuleName.Should().Be("Rule A; Rule B",
            "rule names should be distinct and joined with semicolons");
    }
}
