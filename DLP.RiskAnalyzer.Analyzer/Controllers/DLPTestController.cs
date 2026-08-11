using DLP.RiskAnalyzer.Analyzer.Services;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DLP.RiskAnalyzer.Analyzer.Controllers;

/// <summary>
/// DLP API Test Controller - Swagger üzerinden Forcepoint DLP API bağlantısını test etmek için
/// SECURITY: This controller is only available in Development environment
/// </summary>
#if DEBUG
[ApiController]
[Route("api/[controller]")]
#endif
public class DLPTestController : ControllerBase
{
    private readonly IDlpTestService _dlpTestService;

    public DLPTestController(IDlpTestService dlpTestService)
    {
        _dlpTestService = dlpTestService;
    }

    [HttpPost("auth")]
    public async Task<ActionResult> TestAuthentication()
    {
        var result = await _dlpTestService.TestAuthenticationAsync();
        return StatusCode(result.StatusCode, result.Content);
    }

    [HttpGet("connection")]
    public async Task<ActionResult> TestConnection()
    {
        var result = await _dlpTestService.TestConnectionAsync();
        return StatusCode(result.StatusCode, result.Content);
    }

    [HttpPost("incidents")]
    public async Task<ActionResult> TestFetchIncidents([FromQuery] int hours = 24)
    {
        var result = await _dlpTestService.TestFetchIncidentsAsync(hours);
        return StatusCode(result.StatusCode, result.Content);
    }

    [HttpGet("policy-rules")]
    public async Task<ActionResult> GetPolicyRules([FromQuery] string policyName)
    {
        var result = await _dlpTestService.GetPolicyRulesAsync(policyName);
        return StatusCode(result.StatusCode, result.Content);
    }

    [HttpGet("policy-enabled-names")]
    public async Task<ActionResult> GetEnabledPolicyNames([FromQuery] string type)
    {
        var result = await _dlpTestService.GetEnabledPolicyNamesAsync(type);
        return StatusCode(result.StatusCode, result.Content);
    }

    [HttpGet("policy-exceptions-all")]
    public async Task<ActionResult> GetAllPolicyRulesExceptions([FromQuery] string type)
    {
        var result = await _dlpTestService.GetAllPolicyRulesExceptionsAsync(type);
        return StatusCode(result.StatusCode, result.Content);
    }

    [HttpGet("policy-exceptions")]
    public async Task<ActionResult> GetPolicyRulesExceptions([FromQuery] string type, [FromQuery] string ruleName, [FromQuery] string? policyName = null)
    {
        var result = await _dlpTestService.GetPolicyRulesExceptionsAsync(type, ruleName, policyName);
        return StatusCode(result.StatusCode, result.Content);
    }

    [HttpGet("policy-exceptions-debug")]
    public async Task<ActionResult> DebugPolicyRulesExceptions([FromQuery] string type, [FromQuery] string ruleName, [FromQuery] string? policyName = null)
    {
        var result = await _dlpTestService.DebugPolicyRulesExceptionsAsync(type, ruleName, policyName);
        return StatusCode(result.StatusCode, result.Content);
    }

    [HttpGet("config")]
    public async Task<ActionResult> GetConfig()
    {
        var result = await _dlpTestService.GetConfigAsync();
        return StatusCode(result.StatusCode, result.Content);
    }
}
