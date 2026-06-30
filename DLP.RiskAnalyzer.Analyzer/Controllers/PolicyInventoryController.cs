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

        public PolicyInventoryController(AnalyzerDbContext context, IPolicyInventoryService inventoryService)
        {
            _context = context;
            _inventoryService = inventoryService;
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
                .ToListAsync();

            return Ok(new { success = true, data = policies });
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
    }
}
