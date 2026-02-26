using DLP.RiskAnalyzer.Analyzer.Services;
using Microsoft.AspNetCore.Mvc;

namespace DLP.RiskAnalyzer.Analyzer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PoliciesController : ControllerBase
{
    private readonly PolicyService _policyService;
    private readonly DLP.RiskAnalyzer.Shared.Services.RiskAnalyzer _riskAnalyzer;

    public PoliciesController(PolicyService policyService, DLP.RiskAnalyzer.Shared.Services.RiskAnalyzer riskAnalyzer)
    {
        _policyService = policyService;
        _riskAnalyzer = riskAnalyzer;
    }

    [HttpGet]
    public async Task<ActionResult<Dictionary<string, object>>> GetPolicies()
    {
        try
        {
            var policies = await _policyService.FetchPoliciesAsync();
            return Ok(new { policies, total = policies.Count });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { detail = ex.Message });
        }
    }

    [HttpGet("{policyId}")]
    public async Task<ActionResult<Dictionary<string, object>>> GetPolicy(string policyId)
    {
        try
        {
            var policy = await _policyService.FetchPolicyAsync(policyId);
            return Ok(policy);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { detail = ex.Message });
        }
    }

    [HttpPost("recommendations")]
    public Task<ActionResult<Dictionary<string, object>>> GetPolicyRecommendations(
        [FromBody] Dictionary<string, object> request)
    {
        try
        {
            var riskScore = Convert.ToInt32(request.GetValueOrDefault("risk_score", 0));
            var riskLevel = request.GetValueOrDefault("risk_level", "Medium").ToString() ?? "Medium";
            var channel = request.GetValueOrDefault("channel", "Email").ToString() ?? "Email";

            var recommendation = _policyService.GetPolicyRecommendation(riskScore, riskLevel, channel);
            return Task.FromResult<ActionResult<Dictionary<string, object>>>(Ok(recommendation));
        }
        catch (Exception ex)
        {
            return Task.FromResult<ActionResult<Dictionary<string, object>>>(StatusCode(500, new { detail = ex.Message }));
        }
    }
}

