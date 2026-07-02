using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DLP.RiskAnalyzer.Analyzer.Data;
using DLP.RiskAnalyzer.Shared.Models;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using DLP.RiskAnalyzer.Analyzer.Services;

namespace DLP.RiskAnalyzer.Analyzer.Controllers
{
    [ApiController]
    [Route("api/policy-inventory")]
    public class PolicyInventoryController : ControllerBase
    {
        private readonly AnalyzerDbContext _context;
        private readonly IPolicyInventoryService _inventoryService;
        private readonly IBulkPolicyInventoryImportService _bulkImportService;

        public PolicyInventoryController(
            AnalyzerDbContext context,
            IPolicyInventoryService inventoryService,
            IBulkPolicyInventoryImportService bulkImportService)
        {
            _context = context;
            _inventoryService = inventoryService;
            _bulkImportService = bulkImportService;
        }

        // GET: api/policy-inventory
        [HttpGet]
        public async Task<IActionResult> GetPolicies()
        {
            var policies = await _context.PIPolicies
                .Include(p => p.Rules)
                    .ThenInclude(r => r.Classifiers)
                .Include(p => p.Rules)
                    .ThenInclude(r => r.SeverityActions)
                .Include(p => p.Rules)
                    .ThenInclude(r => r.Sources)
                .Include(p => p.Rules)
                    .ThenInclude(r => r.Destinations)
                        .ThenInclude(d => d.ChannelResources)
                .Include(p => p.Rules)
                    .ThenInclude(r => r.Exceptions)
                        .ThenInclude(e => e.Classifiers)
                .Include(p => p.Rules)
                    .ThenInclude(r => r.Exceptions)
                        .ThenInclude(e => e.SeverityActions)
                .Include(p => p.Rules)
                    .ThenInclude(r => r.Exceptions)
                        .ThenInclude(e => e.Sources)
                .Include(p => p.Rules)
                    .ThenInclude(r => r.Exceptions)
                        .ThenInclude(e => e.Destinations)
                            .ThenInclude(d => d.ChannelResources)
                .AsNoTracking()
                .AsSplitQuery()
                .ToListAsync();

            var options = new System.Text.Json.JsonSerializerOptions
            {
                ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles,
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower
            };
            return Content(System.Text.Json.JsonSerializer.Serialize(new { success = true, data = policies }, options), "application/json");
        }

        // GET: api/policy-inventory/stats
        [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
        {
            var totalPolicies = await _context.PIPolicies.CountAsync();
            var totalRules = await _context.PIRules.CountAsync();
            var totalExceptions = await _context.PIExceptions.CountAsync();
            
            var activeExceptions = await _context.PIExceptions.CountAsync(e => e.Enabled == "true");
            var activeExceptionsPercentage = totalExceptions == 0 ? 0 : (activeExceptions * 100) / totalExceptions;

            return Ok(new
            {
                totalPolicies,
                totalRules,
                totalExceptions,
                activeExceptionsPercentage
            });
        }

        // POST: api/policy-inventory/import
        [HttpPost("import")]
        public async Task<IActionResult> ImportFile()
        {
            var formFile = Request.Form.Files.FirstOrDefault();
            if (formFile == null || formFile.Length == 0)
            {
                return BadRequest("No file uploaded.");
            }

            var result = await _inventoryService.ImportFileAsync(formFile);

            if (result.Success)
            {
                return Ok(new { success = true, message = result.Message, stats = new { policies = result.Policies, rules = result.Rules, exceptions = result.Exceptions } });
            }

            return BadRequest(new { success = false, message = result.Message });
        }

        // POST: api/policy-inventory/import/bulk
        [HttpPost("import/bulk")]
        [DisableRequestSizeLimit]
        [RequestFormLimits(MultipartBodyLengthLimit = long.MaxValue)]
        public async Task<IActionResult> StartBulkImport(CancellationToken cancellationToken)
        {
            var files = Request.Form.Files;
            if (files == null || files.Count == 0)
            {
                return BadRequest(new { success = false, message = "No files uploaded." });
            }

            try
            {
                var status = await _bulkImportService.StartAsync(files, cancellationToken);
                return Accepted(new { success = true, data = status });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        // GET: api/policy-inventory/import/bulk/{jobId}
        [HttpGet("import/bulk/{jobId}")]
        public IActionResult GetBulkImportStatus(string jobId)
        {
            var status = _bulkImportService.GetStatus(jobId);
            if (status == null)
            {
                return NotFound(new { success = false, message = "Import job not found." });
            }

            return Ok(new { success = true, data = status });
        }

        // GET: api/policy-inventory/export/excel
        [HttpGet("export/excel")]
        public async Task<IActionResult> ExportExcel()
        {
            var bytes = await _inventoryService.ExportExcelAsync();
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Politika_Envanteri.xlsx");
        }

        // GET: api/policy-inventory/export/json
        [HttpGet("export/json")]
        public async Task<IActionResult> ExportJson()
        {
            var bytes = await _inventoryService.ExportJsonAsync();
            return File(bytes, "application/json", "Politika_Envanteri.json");
        }
        // CRUD Endpoints for Policies
        [HttpPost("policies")]
        public async Task<IActionResult> CreatePolicy([FromBody] PIPolicy policy)
        {
            var result = await _inventoryService.CreatePolicyAsync(policy);
            if (result.Success) return Ok(new { success = true, message = result.Message, data = result.Data });
            return BadRequest(new { success = false, message = result.Message });
        }

        [HttpPut("policies/{id}")]
        public async Task<IActionResult> UpdatePolicy(int id, [FromBody] PIPolicy policy)
        {
            var result = await _inventoryService.UpdatePolicyAsync(id, policy);
            if (result.Success) return Ok(new { success = true, message = result.Message, data = result.Data });
            return BadRequest(new { success = false, message = result.Message });
        }

        [HttpDelete("policies/{id}")]
        public async Task<IActionResult> DeletePolicy(int id)
        {
            var result = await _inventoryService.DeletePolicyAsync(id);
            if (result.Success) return Ok(new { success = true, message = result.Message });
            return BadRequest(new { success = false, message = result.Message });
        }

        // CRUD Endpoints for Rules
        [HttpPost("rules")]
        public async Task<IActionResult> CreateRule([FromBody] PIRule rule)
        {
            var result = await _inventoryService.CreateRuleAsync(rule);
            if (result.Success) return Ok(new { success = true, message = result.Message, data = result.Data });
            return BadRequest(new { success = false, message = result.Message });
        }

        [HttpPut("rules/{id}")]
        public async Task<IActionResult> UpdateRule(int id, [FromBody] PIRule rule)
        {
            var result = await _inventoryService.UpdateRuleAsync(id, rule);
            if (result.Success) return Ok(new { success = true, message = result.Message, data = result.Data });
            return BadRequest(new { success = false, message = result.Message });
        }

        [HttpDelete("rules/{id}")]
        public async Task<IActionResult> DeleteRule(int id)
        {
            var result = await _inventoryService.DeleteRuleAsync(id);
            if (result.Success) return Ok(new { success = true, message = result.Message });
            return BadRequest(new { success = false, message = result.Message });
        }

        // CRUD Endpoints for Exceptions
        [HttpPost("exceptions")]
        public async Task<IActionResult> CreateException([FromBody] PIException exc)
        {
            var result = await _inventoryService.CreateExceptionAsync(exc);
            if (result.Success) return Ok(new { success = true, message = result.Message, data = result.Data });
            return BadRequest(new { success = false, message = result.Message });
        }

        [HttpPut("exceptions/{id}")]
        public async Task<IActionResult> UpdateException(int id, [FromBody] PIException exc)
        {
            var result = await _inventoryService.UpdateExceptionAsync(id, exc);
            if (result.Success) return Ok(new { success = true, message = result.Message, data = result.Data });
            return BadRequest(new { success = false, message = result.Message });
        }

        [HttpDelete("exceptions/{id}")]
        public async Task<IActionResult> DeleteException(int id)
        {
            var result = await _inventoryService.DeleteExceptionAsync(id);
            if (result.Success) return Ok(new { success = true, message = result.Message });
            return BadRequest(new { success = false, message = result.Message });
        }
    }
}
