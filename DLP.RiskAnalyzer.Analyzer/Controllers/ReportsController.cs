using DLP.RiskAnalyzer.Analyzer.Services;
using Microsoft.AspNetCore.Mvc;
using System.IO;

namespace DLP.RiskAnalyzer.Analyzer.Controllers;

[ApiController]
[Route("api/reports")]
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
                // Generate comprehensive daily summary report with real data
                pdfBytes = await _reportGenerator.GenerateDailySummaryReportAsync(startDate);
                filename = $"daily_report_{startDate:yyyyMMdd}.pdf";
            }
            else if (reportType == "department")
            {
                var reportData = await _riskAnalyzerService.GetDailyReportDataAsync(startDate);
                pdfBytes = _reportGenerator.GenerateDailyReport(startDate, reportData);
                filename = $"department_report_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.pdf";
            }
            else // user_risk
            {
                var reportData = await _riskAnalyzerService.GetDailyReportDataAsync(startDate);
                pdfBytes = _reportGenerator.GenerateDailyReport(startDate, reportData);
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

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummaryReport([FromQuery] string? start_date, [FromQuery] string? end_date)
    {
        try
        {
            var startDate = !string.IsNullOrEmpty(start_date)
                ? DateTime.Parse(start_date)
                : DateTime.UtcNow.AddDays(-7);
            var endDate = !string.IsNullOrEmpty(end_date)
                ? DateTime.Parse(end_date)
                : DateTime.UtcNow;

            // Generate comprehensive PDF report with real data
            var pdfBytes = await _reportGenerator.GenerateDailySummaryReportAsync(startDate);
            var filename = $"dlp_report_{startDate:yyyyMMdd}_to_{endDate:yyyyMMdd}.pdf";

            return File(pdfBytes, "application/pdf", filename);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { detail = $"Error generating summary report: {ex.Message}" });
        }
    }

    /// <summary>
    /// Get daily summary data as JSON for the Reports page
    /// </summary>
    [HttpGet("daily-summary")]
    public async Task<ActionResult<Dictionary<string, object>>> GetDailySummaryData([FromQuery] DateTime? date = null)
    {
        try
        {
            var targetDate = date ?? DateTime.UtcNow.Date;
            var result = await _riskAnalyzerService.GetDailyReportDataAsync(targetDate);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { detail = ex.Message });
        }
    }

    /// <summary>
    /// Download daily summary as PDF for a specific date
    /// </summary>
    [HttpGet("daily-summary/pdf")]
    public async Task<IActionResult> DownloadDailySummaryPdf([FromQuery] DateTime? date = null)
    {
        try
        {
            var targetDate = date ?? DateTime.UtcNow.Date;
            var pdfBytes = await _reportGenerator.GenerateDailySummaryReportAsync(targetDate);
            var filename = $"daily_summary_{targetDate:yyyyMMdd}.pdf";

            return File(pdfBytes, "application/pdf", filename);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { detail = $"Error generating daily summary PDF: {ex.Message}" });
        }
    }

    [HttpGet("{reportId}/download")]
    public IActionResult DownloadReport(int reportId)
    {
        try
        {
            // Ensure directory exists
            if (!Directory.Exists(_reportsDirectory))
            {
                return NotFound(new { detail = "Reports directory does not exist" });
            }

            var files = Directory.GetFiles(_reportsDirectory, "*.pdf")
                .OrderByDescending(f => System.IO.File.GetCreationTime(f))
                .ToList();

            if (reportId < 1 || reportId > files.Count)
            {
                return NotFound(new { detail = $"Report not found. Available reports: {files.Count}, requested: {reportId}" });
            }

            var filepath = files[reportId - 1];
            
            if (!System.IO.File.Exists(filepath))
            {
                return NotFound(new { detail = $"Report file does not exist: {filepath}" });
            }

            var filename = Path.GetFileName(filepath);
            var fileBytes = System.IO.File.ReadAllBytes(filepath);

            if (fileBytes == null || fileBytes.Length == 0)
            {
                return StatusCode(500, new { detail = "Report file is empty" });
            }

            return File(fileBytes, "application/pdf", filename);
        }
        catch (DirectoryNotFoundException ex)
        {
            return StatusCode(500, new { detail = $"Directory not found: {ex.Message}" });
        }
        catch (FileNotFoundException ex)
        {
            return NotFound(new { detail = $"File not found: {ex.Message}" });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(500, new { detail = $"Access denied: {ex.Message}" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { detail = $"Error downloading report: {ex.Message}", type = ex.GetType().Name });
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

