using DLP.RiskAnalyzer.Analyzer.Helpers;
using DLP.RiskAnalyzer.Shared.Models;
using FluentAssertions;

namespace DLP.RiskAnalyzer.Tests.Unit;

/// <summary>
/// Unit tests for IncidentResponseMapper.
/// Ensures the centralized factory correctly maps every field from Incident to
/// IncidentResponse so that adding a new field only requires one change.
/// </summary>
public class IncidentResponseMapperTests
{
    private static Incident BuildFullIncident() => new()
    {
        Id                = 42,
        UserEmail         = "test@company.com",
        Department        = "Finance",
        Severity          = 3,
        DataType          = "PII",
        Timestamp         = new DateTime(2025, 6, 15, 10, 30, 0, DateTimeKind.Utc),
        Policy            = "Data Loss Prevention",
        RuleName          = "Credit Card",
        Channel           = "Email",
        RiskScore         = 75,
        RepeatCount       = 2,
        DataSensitivity   = 8,
        MaxMatches        = 42,
        Action            = "BLOCK",
        Destination       = "external@gmail.com",
        FileName          = "report.xlsx",
        LoginName         = "jdoe",
        HostName          = "PC-001",
        EmailAddress      = "john.doe@company.com",
        ViolationTriggers = "[{\"RuleName\":\"Credit Card\"}]",
        FullName          = "John Doe",
        Team              = "Risk Team",
        IsRemediated      = true,
        RemediatedAt      = new DateTime(2025, 6, 16, 9, 0, 0, DateTimeKind.Utc),
        RemediatedBy      = "admin",
        RemediationAction = "BLOCK",
        RemediationNotes  = "Escalated"
    };

    [Fact]
    public void Map_AllCoreFields_AreMappedCorrectly()
    {
        var incident = BuildFullIncident();
        var response = IncidentResponseMapper.Map(incident);

        response.Id.Should().Be(incident.Id);
        response.UserEmail.Should().Be(incident.UserEmail);
        response.Department.Should().Be(incident.Department);
        response.Severity.Should().Be(incident.Severity);
        response.DataType.Should().Be(incident.DataType);
        response.Timestamp.Should().Be(incident.Timestamp);
        response.Policy.Should().Be(incident.Policy);
        response.RuleName.Should().Be(incident.RuleName);
        response.Channel.Should().Be(incident.Channel);
        response.RiskScore.Should().Be(incident.RiskScore);
        response.RepeatCount.Should().Be(incident.RepeatCount);
        response.DataSensitivity.Should().Be(incident.DataSensitivity);
        response.MaxMatches.Should().Be(incident.MaxMatches);
    }

    [Fact]
    public void Map_AllExtendedDlpFields_AreMappedCorrectly()
    {
        var incident = BuildFullIncident();
        var response = IncidentResponseMapper.Map(incident);

        response.Action.Should().Be(incident.Action);
        response.Destination.Should().Be(incident.Destination);
        response.FileName.Should().Be(incident.FileName);
        response.LoginName.Should().Be(incident.LoginName);
        response.HostName.Should().Be(incident.HostName);
        response.EmailAddress.Should().Be(incident.EmailAddress);
        response.ViolationTriggers.Should().Be(incident.ViolationTriggers);
        response.FullName.Should().Be(incident.FullName);
        response.Team.Should().Be(incident.Team);
    }

    [Fact]
    public void Map_AllRemediationFields_AreMappedCorrectly()
    {
        var incident = BuildFullIncident();
        var response = IncidentResponseMapper.Map(incident);

        response.IsRemediated.Should().Be(incident.IsRemediated);
        response.RemediatedAt.Should().Be(incident.RemediatedAt);
        response.RemediatedBy.Should().Be(incident.RemediatedBy);
        response.RemediationAction.Should().Be(incident.RemediationAction);
        response.RemediationNotes.Should().Be(incident.RemediationNotes);
    }

    [Fact]
    public void Map_WithEnrichmentValues_PopulatesEnrichedFields()
    {
        var incident = BuildFullIncident();
        var iobs     = new List<string> { "IOB-511", "IOB-311" };

        var response = IncidentResponseMapper.Map(incident,
            riskLevel:         "High",
            recommendedAction: "Encrypt",
            iobs:              iobs);

        response.RiskLevel.Should().Be("High");
        response.RecommendedAction.Should().Be("Encrypt");
        response.IOBs.Should().BeEquivalentTo(iobs);
    }

    [Fact]
    public void Map_WithoutEnrichmentValues_EnrichedFieldsAreNull()
    {
        var response = IncidentResponseMapper.Map(BuildFullIncident());

        response.RiskLevel.Should().BeNull();
        response.RecommendedAction.Should().BeNull();
        response.IOBs.Should().BeNull();
    }

    [Fact]
    public void Map_NullableFieldsOnIncident_MappedAsNull()
    {
        var incident = new Incident { UserEmail = "x@y.com" };
        var response = IncidentResponseMapper.Map(incident);

        response.Department.Should().BeNull();
        response.DataType.Should().BeNull();
        response.Policy.Should().BeNull();
        response.RuleName.Should().BeNull();
        response.Channel.Should().BeNull();
        response.RiskScore.Should().BeNull();
        response.Action.Should().BeNull();
        response.Destination.Should().BeNull();
        response.RemediatedAt.Should().BeNull();
    }
}
