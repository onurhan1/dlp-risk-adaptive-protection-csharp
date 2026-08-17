using DLP.RiskAnalyzer.Analyzer.Services;
using Microsoft.AspNetCore.Mvc;

namespace DLP.RiskAnalyzer.Analyzer.Controllers;

[ApiController]
[Route("api/scheduled-jobs")]
public class ScheduledJobsController : ControllerBase
{
    private readonly IScheduledJobService _scheduledJobService;
    private readonly ILogger<ScheduledJobsController> _logger;

    public ScheduledJobsController(
        IScheduledJobService scheduledJobService,
        ILogger<ScheduledJobsController> logger)
    {
        _scheduledJobService = scheduledJobService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetJobs(CancellationToken ct) =>
        Ok(await _scheduledJobService.GetJobsAsync(ct));

    [HttpGet("catalog")]
    public async Task<IActionResult> GetCatalog() =>
        Ok(await _scheduledJobService.GetCatalogAsync());

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ScheduledJobRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await _scheduledJobService.CreateAsync(request, ct));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] ScheduledJobRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await _scheduledJobService.UpdateAsync(id, request, ct));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { detail = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
    }

    [HttpPost("{id:int}/toggle")]
    public async Task<IActionResult> Toggle(int id, CancellationToken ct)
    {
        try
        {
            return Ok(await _scheduledJobService.ToggleAsync(id, ct));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { detail = ex.Message });
        }
    }

    [HttpPost("{id:int}/run")]
    public async Task<IActionResult> RunNow(int id, CancellationToken ct)
    {
        try
        {
            return Ok(await _scheduledJobService.RunNowAsync(id, ct));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { detail = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Manual scheduled job run failed");
            return StatusCode(500, new { detail = ex.Message });
        }
    }

    [HttpGet("runs")]
    public async Task<IActionResult> GetRuns([FromQuery] int? jobId, [FromQuery] int limit, CancellationToken ct) =>
        Ok(await _scheduledJobService.GetRunsAsync(jobId, limit, ct));
}
