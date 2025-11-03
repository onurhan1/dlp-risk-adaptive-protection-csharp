using DLP.RiskAnalyzer.Analyzer.Services;
using Microsoft.AspNetCore.Mvc;
using System.IO;

namespace DLP.RiskAnalyzer.Analyzer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReportsController : ControllerBase
{
    private readonly ReportGeneratorService _reportGenerator;
    private readonly RiskAnalyzerService _riskAnalyzerService;
    private readonly string _reportsDirectory;

    public ReportsController(
        ReportGeneratorService reportGenerator,
        RiskAnalyzerService riskAnalyzerService,
        IConfiguration configuration)
    {
        _reportGenerator = reportGenerator;
        _riskAnalyzerService = riskAnalyzerService;
        _reportsDirectory = configuration["Reports:Directory"] ?? Path.Combine(Directory.GetCurrentDirectory(), "reports");
        Directory.CreateDirectory(_reportsDirectory);
    }

    [HttpGet]
    public ActionResult<Dictionary<string, object>> ListReports()
    {
        try
        {
            var files = Directory.GetFiles(_reportsDirectory, "*.pdf")
                .OrderByDescending(f => System.IO.File.GetCreationTime(f))
                .Select((f, idx) => new
                {
                    id = idx + 1,
                    report_type = ExtractReportType(f),
                    generated_at = System.IO.File.GetCreationTime(f).ToString("O"),
                    filename = Path.GetFileName(f),
                    status = "completed"
                })
                .ToList();

            return Ok(new { reports = files, total = files.Count });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { detail = ex.Message });
        }
    }

    [HttpPost("generate")]
    public async Task<ActionResult<Dictionary<string, object>>> GenerateReport(
        [FromBody] Dictionary<string, object> request)
    {
        try
        {
            var reportType = request.GetValueOrDefault("report_type")?.ToString() ?? "daily";
            var startDate = request.ContainsKey("start_date") 
                ? DateTime.Parse(request["start_date"].ToString()!) 
                : DateTime.UtcNow.AddDays(-7);
            var endDate = request.ContainsKey("end_date") 
                ? DateTime.Parse(request["end_date"].ToString()!) 
                : DateTime.UtcNow;

            byte[] pdfBytes;
            string filename;

            if (reportType == "daily")
            {
                pdfBytes = _reportGenerator.GenerateDailyReport(startDate, null);
                filename = $"daily_report_{startDate:yyyyMMdd}.pdf";
            }
            else if (reportType == "department")
            {
                pdfBytes = _reportGenerator.GenerateDailyReport(startDate, null); // Placeholder
                filename = $"department_report_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.pdf";
            }
            else // user_risk
            {
                pdfBytes = _reportGenerator.GenerateDailyReport(startDate, null); // Placeholder
                filename = $"risk_trends_report_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.pdf";
            }

            var filepath = Path.Combine(_reportsDirectory, filename);
            await System.IO.File.WriteAllBytesAsync(filepath, pdfBytes);

            return Ok(new
            {
                success = true,
                message = "Report generated successfully",
                filename,
                generated_at = DateTime.UtcNow.ToString("O")
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { detail = ex.Message });
        }
    }

    [HttpGet("{reportId}/download")]
    public ActionResult DownloadReport(int reportId)
    {
        try
        {
            var files = Directory.GetFiles(_reportsDirectory, "*.pdf")
                .OrderByDescending(f => System.IO.File.GetCreationTime(f))
                .ToList();

            if (reportId < 1 || reportId > files.Count)
            {
                return NotFound(new { detail = $"Report not found. Available reports: {files.Count}" });
            }

            var filepath = files[reportId - 1];
            
            if (!System.IO.File.Exists(filepath))
            {
                return NotFound(new { detail = "Report file does not exist on disk" });
            }

            var filename = Path.GetFileName(filepath);
            var fileBytes = System.IO.File.ReadAllBytes(filepath);

            return File(fileBytes, "application/pdf", filename);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { detail = $"Error downloading report: {ex.Message}", stackTrace = ex.StackTrace });
        }
    }

    private string ExtractReportType(string filepath)
    {
        var filename = Path.GetFileName(filepath).ToLower();
        if (filename.Contains("daily"))
            return "Daily Summary";
        if (filename.Contains("department"))
            return "Department Summary";
        if (filename.Contains("trend") || filename.Contains("risk"))
            return "User Risk Trends";
        return "Unknown";
    }
}

