using DLP.RiskAnalyzer.Analyzer.Data;
using DLP.RiskAnalyzer.Analyzer.Services;
using DLP.RiskAnalyzer.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text.Json;

namespace DLP.RiskAnalyzer.Analyzer.Controllers;

/// <summary>
/// Exception Entries API - Kalıcı İstisna ve İstisna Kaldırma kayıtlarını yönetir
/// Excel yükleme ve CRUD işlemlerini destekler
/// </summary>
[ApiController]
[Route("api/exception-entries")]
public class ExceptionEntriesController : ControllerBase
{
    private readonly AnalyzerDbContext _context;
    private readonly ILogger<ExceptionEntriesController> _logger;
    private readonly IAuditLogService _auditLogService;

    public ExceptionEntriesController(
        AnalyzerDbContext context,
        ILogger<ExceptionEntriesController> logger,
        IAuditLogService auditLogService)
    {
        _context = context;
        _logger = logger;
        _auditLogService = auditLogService;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // PERMANENT EXCEPTIONS (Kalıcı İstisna Listesi)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>Get permanent exceptions with pagination and search</summary>
    [HttpGet("permanent")]
    public async Task<IActionResult> GetPermanentExceptions(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? search = null)
    {
        var query = _context.PermanentExceptions.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            query = query.Where(e =>
                e.ExceptionName.ToLower().Contains(s) ||
                (e.ExceptionDomain != null && e.ExceptionDomain.ToLower().Contains(s)) ||
                (e.Team != null && e.Team.ToLower().Contains(s)) ||
                (e.Policies != null && e.Policies.ToLower().Contains(s)) ||
                (e.Rules != null && e.Rules.ToLower().Contains(s)) ||
                (e.ChangeNo != null && e.ChangeNo.Contains(s)));
        }

        var total = await query.CountAsync();
        var entries = await query
            .OrderByDescending(e => e.ActionDate ?? e.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new
        {
            entries,
            total,
            page,
            pageSize,
            totalPages = (int)Math.Ceiling(total / (double)pageSize)
        });
    }

    /// <summary>Create a new permanent exception entry</summary>
    [HttpPost("permanent")]
    public async Task<IActionResult> CreatePermanentException([FromBody] PermanentExceptionEntry entry)
    {
        try
        {
            entry.CreatedAt = DateTime.UtcNow;
            entry.UpdatedAt = DateTime.UtcNow;

            _context.PermanentExceptions.Add(entry);
            await _context.SaveChangesAsync();

            await _auditLogService.LogAsync(
                eventType: "ExceptionCreate",
                userName: entry.CreatedBy ?? "System",
                userRole: null,
                action: $"Permanent exception created: {entry.ExceptionName}",
                resource: $"PermanentException:{entry.Id}",
                success: true);

            return Ok(new { success = true, entry });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating permanent exception");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>Update a permanent exception entry</summary>
    [HttpPut("permanent/{id}")]
    public async Task<IActionResult> UpdatePermanentException(int id, [FromBody] PermanentExceptionEntry entry)
    {
        try
        {
            var existing = await _context.PermanentExceptions.FindAsync(id);
            if (existing == null) return NotFound();

            existing.ExceptionName = entry.ExceptionName;
            existing.ExceptionDomain = entry.ExceptionDomain;
            existing.Team = entry.Team;
            existing.Policies = entry.Policies;
            existing.Rules = entry.Rules;
            existing.Channel = entry.Channel;
            existing.Duration = entry.Duration;
            existing.ActionDate = entry.ActionDate;
            existing.ChangeNo = entry.ChangeNo;
            existing.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Ok(new { success = true, entry = existing });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating permanent exception {Id}", id);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>Delete a permanent exception entry</summary>
    [HttpDelete("permanent/{id}")]
    public async Task<IActionResult> DeletePermanentException(int id)
    {
        try
        {
            var entry = await _context.PermanentExceptions.FindAsync(id);
            if (entry == null) return NotFound();

            _context.PermanentExceptions.Remove(entry);
            await _context.SaveChangesAsync();

            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting permanent exception {Id}", id);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>Upload Excel file for permanent exceptions</summary>
    [HttpPost("permanent/upload")]
    public async Task<IActionResult> UploadPermanentExceptions(
        IFormFile file,
        [FromQuery] string? uploadedBy)
    {
        if (file == null || file.Length == 0)
            return BadRequest("No file uploaded");

        try
        {
            using var stream = file.OpenReadStream();
            var entries = ParsePermanentExceptionExcel(stream, uploadedBy);

            if (entries.Count == 0)
                return BadRequest("No valid entries found in the Excel file");

            await _context.PermanentExceptions.AddRangeAsync(entries);
            await _context.SaveChangesAsync();

            await _auditLogService.LogAsync(
                eventType: "ExceptionUpload",
                userName: uploadedBy ?? "System",
                userRole: null,
                action: $"Uploaded {entries.Count} permanent exceptions from Excel",
                resource: $"PermanentExceptions",
                details: JsonSerializer.Serialize(new { fileName = file.FileName, count = entries.Count }),
                success: true);

            _logger.LogInformation("Uploaded {Count} permanent exceptions from {FileName}", entries.Count, file.FileName);

            return Ok(new { success = true, imported = entries.Count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading permanent exceptions");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // EXCEPTION REMOVALS (İstisna Kaldırma Listesi)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>Get exception removals with pagination and search</summary>
    [HttpGet("removal")]
    public async Task<IActionResult> GetExceptionRemovals(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null)
    {
        var query = _context.ExceptionRemovals.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            query = query.Where(e =>
                e.ExceptionName.ToLower().Contains(s) ||
                (e.Team != null && e.Team.ToLower().Contains(s)) ||
                (e.Rule != null && e.Rule.ToLower().Contains(s)) ||
                (e.RemovalReason != null && e.RemovalReason.ToLower().Contains(s)));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(e => e.Status == status);
        }

        var total = await query.CountAsync();
        var entries = await query
            .OrderByDescending(e => e.ActionDate ?? e.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new
        {
            entries,
            total,
            page,
            pageSize,
            totalPages = (int)Math.Ceiling(total / (double)pageSize)
        });
    }

    /// <summary>Create a new exception removal entry</summary>
    [HttpPost("removal")]
    public async Task<IActionResult> CreateExceptionRemoval([FromBody] ExceptionRemovalEntry entry)
    {
        try
        {
            entry.CreatedAt = DateTime.UtcNow;
            entry.UpdatedAt = DateTime.UtcNow;

            _context.ExceptionRemovals.Add(entry);
            await _context.SaveChangesAsync();

            await _auditLogService.LogAsync(
                eventType: "ExceptionCreate",
                userName: entry.CreatedBy ?? "System",
                userRole: null,
                action: $"Exception removal created: {entry.ExceptionName}",
                resource: $"ExceptionRemoval:{entry.Id}",
                success: true);

            return Ok(new { success = true, entry });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating exception removal");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>Update an exception removal entry</summary>
    [HttpPut("removal/{id}")]
    public async Task<IActionResult> UpdateExceptionRemoval(int id, [FromBody] ExceptionRemovalEntry entry)
    {
        try
        {
            var existing = await _context.ExceptionRemovals.FindAsync(id);
            if (existing == null) return NotFound();

            existing.Team = entry.Team;
            existing.Rule = entry.Rule;
            existing.ExceptionName = entry.ExceptionName;
            existing.Status = entry.Status;
            existing.UsageCount = entry.UsageCount;
            existing.RemovalReason = entry.RemovalReason;
            existing.ActionDate = entry.ActionDate;
            existing.ChangeNo = entry.ChangeNo;
            existing.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Ok(new { success = true, entry = existing });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating exception removal {Id}", id);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>Delete an exception removal entry</summary>
    [HttpDelete("removal/{id}")]
    public async Task<IActionResult> DeleteExceptionRemoval(int id)
    {
        try
        {
            var entry = await _context.ExceptionRemovals.FindAsync(id);
            if (entry == null) return NotFound();

            _context.ExceptionRemovals.Remove(entry);
            await _context.SaveChangesAsync();

            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting exception removal {Id}", id);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>Upload Excel file for exception removals</summary>
    [HttpPost("removal/upload")]
    public async Task<IActionResult> UploadExceptionRemovals(
        IFormFile file,
        [FromQuery] string? uploadedBy)
    {
        if (file == null || file.Length == 0)
            return BadRequest("No file uploaded");

        try
        {
            using var stream = file.OpenReadStream();
            var entries = ParseExceptionRemovalExcel(stream, uploadedBy);

            if (entries.Count == 0)
                return BadRequest("No valid entries found in the Excel file");

            await _context.ExceptionRemovals.AddRangeAsync(entries);
            await _context.SaveChangesAsync();

            await _auditLogService.LogAsync(
                eventType: "ExceptionUpload",
                userName: uploadedBy ?? "System",
                userRole: null,
                action: $"Uploaded {entries.Count} exception removals from Excel",
                resource: $"ExceptionRemovals",
                details: JsonSerializer.Serialize(new { fileName = file.FileName, count = entries.Count }),
                success: true);

            _logger.LogInformation("Uploaded {Count} exception removals from {FileName}", entries.Count, file.FileName);

            return Ok(new { success = true, imported = entries.Count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading exception removals");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Excel Parsing Helpers
    // ═══════════════════════════════════════════════════════════════════════════

    private static List<PermanentExceptionEntry> ParsePermanentExceptionExcel(Stream stream, string? uploadedBy)
    {
        var entries = new List<PermanentExceptionEntry>();

        // Use ClosedXML or simple OOXML parsing
        // For now we use a minimal approach with the SpreadsheetDocument
        // Since we already have exceljs in frontend, we'll parse in frontend and send JSON
        // But for direct upload, we use a simple xlsx parser

        try
        {
            using var package = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Read);
            var sharedStrings = ReadSharedStrings(package);
            var sheetData = ReadSheetData(package, "sheet1.xml");

            // Skip header row (row 1)
            foreach (var row in sheetData.Skip(1))
            {
                if (row.Count < 2) continue;

                var entry = new PermanentExceptionEntry
                {
                    ExceptionName = GetCellValue(row, 0, sharedStrings) ?? "",
                    ExceptionDomain = GetCellValue(row, 1, sharedStrings),
                    Team = GetCellValue(row, 2, sharedStrings),
                    Policies = GetCellValue(row, 3, sharedStrings),
                    Rules = GetCellValue(row, 4, sharedStrings),
                    Channel = GetCellValue(row, 5, sharedStrings),
                    Duration = GetCellValue(row, 6, sharedStrings),
                    ActionDate = ParseExcelDate(GetCellValue(row, 7, sharedStrings)),
                    ChangeNo = GetCellValue(row, 8, sharedStrings),
                    CreatedBy = uploadedBy,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                if (!string.IsNullOrWhiteSpace(entry.ExceptionName))
                    entries.Add(entry);
            }
        }
        catch (Exception)
        {
            // If OOXML parsing fails, return empty - frontend will handle Excel parsing
        }

        return entries;
    }

    private static List<ExceptionRemovalEntry> ParseExceptionRemovalExcel(Stream stream, string? uploadedBy)
    {
        var entries = new List<ExceptionRemovalEntry>();

        try
        {
            using var package = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Read);
            var sharedStrings = ReadSharedStrings(package);
            var sheetData = ReadSheetData(package, "sheet1.xml");

            // Skip header row
            foreach (var row in sheetData.Skip(1))
            {
                if (row.Count < 2) continue;

                var usageStr = GetCellValue(row, 4, sharedStrings);
                int.TryParse(usageStr, out int usageCount);

                var entry = new ExceptionRemovalEntry
                {
                    Team = GetCellValue(row, 0, sharedStrings),
                    Rule = GetCellValue(row, 1, sharedStrings),
                    ExceptionName = GetCellValue(row, 2, sharedStrings) ?? "",
                    Status = GetCellValue(row, 3, sharedStrings),
                    UsageCount = usageCount,
                    RemovalReason = GetCellValue(row, 5, sharedStrings),
                    ActionDate = ParseExcelDate(GetCellValue(row, 6, sharedStrings)),
                    ChangeNo = GetCellValue(row, 7, sharedStrings),
                    CreatedBy = uploadedBy,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                if (!string.IsNullOrWhiteSpace(entry.ExceptionName))
                    entries.Add(entry);
            }
        }
        catch (Exception)
        {
            // If parsing fails, return empty
        }

        return entries;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Low-level XLSX helpers (avoid external dependency)
    // ═══════════════════════════════════════════════════════════════════════════

    private static List<string> ReadSharedStrings(System.IO.Compression.ZipArchive package)
    {
        var strings = new List<string>();
        var entry = package.GetEntry("xl/sharedStrings.xml");
        if (entry == null) return strings;

        using var reader = new System.IO.StreamReader(entry.Open());
        var xml = reader.ReadToEnd();

        // Simple XML parsing for <t> elements
        var startTag = "<t";
        var endTag = "</t>";
        int pos = 0;
        while ((pos = xml.IndexOf(startTag, pos, StringComparison.Ordinal)) >= 0)
        {
            var tagEnd = xml.IndexOf('>', pos);
            if (tagEnd < 0) break;
            var closePos = xml.IndexOf(endTag, tagEnd, StringComparison.Ordinal);
            if (closePos < 0) break;
            strings.Add(System.Net.WebUtility.HtmlDecode(xml.Substring(tagEnd + 1, closePos - tagEnd - 1)));
            pos = closePos + endTag.Length;
        }

        return strings;
    }

    private static List<List<(int col, string value, string? type)>> ReadSheetData(
        System.IO.Compression.ZipArchive package, string sheetName)
    {
        var rows = new List<List<(int col, string value, string? type)>>();

        // Try common sheet paths
        var entry = package.GetEntry($"xl/worksheets/{sheetName}")
                    ?? package.GetEntry("xl/worksheets/sheet1.xml");
        if (entry == null) return rows;

        using var reader = new System.IO.StreamReader(entry.Open());
        var xml = reader.ReadToEnd();

        // Parse <row> elements
        var rowTag = "<row";
        int pos = 0;
        while ((pos = xml.IndexOf(rowTag, pos, StringComparison.Ordinal)) >= 0)
        {
            var rowEnd = xml.IndexOf("</row>", pos, StringComparison.Ordinal);
            if (rowEnd < 0) break;

            var rowXml = xml.Substring(pos, rowEnd - pos + 6);
            var cells = new List<(int col, string value, string? type)>();

            // Parse <c> elements within row
            var cTag = "<c ";
            int cPos = 0;
            while ((cPos = rowXml.IndexOf(cTag, cPos, StringComparison.Ordinal)) >= 0)
            {
                var cEnd = rowXml.IndexOf("</c>", cPos, StringComparison.Ordinal);
                var selfClose = rowXml.IndexOf("/>", cPos, StringComparison.Ordinal);
                
                int cellEnd;
                if (cEnd >= 0 && (selfClose < 0 || cEnd < selfClose))
                    cellEnd = cEnd + 4;
                else if (selfClose >= 0)
                    cellEnd = selfClose + 2;
                else
                    break;

                var cellXml = rowXml.Substring(cPos, cellEnd - cPos);

                // Extract r attribute (cell reference like A1, B2)
                var rMatch = System.Text.RegularExpressions.Regex.Match(cellXml, @"r=""([A-Z]+)\d+""");
                var tMatch = System.Text.RegularExpressions.Regex.Match(cellXml, @"t=""(\w+)""");
                var vMatch = System.Text.RegularExpressions.Regex.Match(cellXml, @"<v>(.*?)</v>");

                if (rMatch.Success && vMatch.Success)
                {
                    var colLetter = rMatch.Groups[1].Value;
                    var colIndex = ColumnLetterToIndex(colLetter);
                    var type = tMatch.Success ? tMatch.Groups[1].Value : null;
                    cells.Add((colIndex, vMatch.Groups[1].Value, type));
                }

                cPos = cellEnd;
            }

            rows.Add(cells);
            pos = rowEnd + 6;
        }

        return rows;
    }

    private static string? GetCellValue(List<(int col, string value, string? type)> row, int colIndex, List<string> sharedStrings)
    {
        var cell = row.FirstOrDefault(c => c.col == colIndex);
        if (cell == default) return null;

        if (cell.type == "s" && int.TryParse(cell.value, out int ssIndex) && ssIndex < sharedStrings.Count)
            return sharedStrings[ssIndex];

        return cell.value;
    }

    private static int ColumnLetterToIndex(string letter)
    {
        int index = 0;
        foreach (char c in letter)
        {
            index = index * 26 + (c - 'A');
        }
        return index;
    }

    private static DateTime? ParseExcelDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        // Try various Turkish date formats
        string[] formats = { "dd.MM.yyyy", "dd/MM/yyyy", "yyyy-MM-dd", "d.M.yyyy", "d/M/yyyy" };
        if (DateTime.TryParseExact(value, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return date;

        // Try Excel serial date
        if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out double serial) && serial > 1)
        {
            try { return DateTime.FromOADate(serial); } catch { }
        }

        // Try general parse
        if (DateTime.TryParse(value, CultureInfo.GetCultureInfo("tr-TR"), DateTimeStyles.None, out var parsed))
            return parsed;

        return null;
    }
}
