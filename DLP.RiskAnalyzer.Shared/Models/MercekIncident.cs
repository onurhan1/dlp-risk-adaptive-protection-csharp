namespace DLP.RiskAnalyzer.Shared.Models;

/// <summary>
/// Mercek (Help Desk) Incident Model
/// Represents incidents from the merceks.csv database
/// </summary>
public class MercekIncident
{
    public int IncidentId { get; set; }
    public string? StatusId { get; set; }
    public string? FlowStatusId { get; set; }
    public int? AssignmentGroupId { get; set; }
    public string? SummaryDescription { get; set; }
    public string? IncidentDescription { get; set; }
    public string? ImpactId { get; set; }
    public string? PriorityId { get; set; }
    public int? CategoryId { get; set; }
    public string? AssignedUserCode { get; set; }
    public DateTime? OpenDate { get; set; }
    public DateTime? CloseDate { get; set; }
    public DateTime? StartDate { get; set; }
    public string? SolutionDescription { get; set; }
    public string? RequestTypeId { get; set; }
    public string? CallTypeId { get; set; }
    public string? SolutionMethod { get; set; }
    public string? UserName { get; set; }
    public DateTime? SystemDate { get; set; }
    public int? DefinitionCategoryId { get; set; }
    public string? DefinitionCategoryPath { get; set; }
}

/// <summary>
/// Paginated response for Mercek incidents
/// </summary>
public class MercekIncidentResponse
{
    public List<MercekIncident> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public bool HasNextPage { get; set; }
    public bool HasPreviousPage { get; set; }
}

/// <summary>
/// Filter options for Mercek incidents
/// </summary>
public class MercekFilterOptions
{
    public List<string> Users { get; set; } = new();
    public List<string> AssignedUsers { get; set; } = new();
    public List<string> StatusIds { get; set; } = new();
    public List<int> CategoryIds { get; set; } = new();
    public DateTime? MinDate { get; set; }
    public DateTime? MaxDate { get; set; }
}
