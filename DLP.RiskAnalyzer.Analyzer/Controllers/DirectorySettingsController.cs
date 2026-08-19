using DLP.RiskAnalyzer.Analyzer.Models;
using DLP.RiskAnalyzer.Analyzer.Services;
using Microsoft.AspNetCore.Mvc;

namespace DLP.RiskAnalyzer.Analyzer.Controllers;

[ApiController]
[Route("api/settings")]
public class DirectorySettingsController : ControllerBase
{
    private readonly IDirectorySettingsService _settingsService;
    private readonly IExternalUserDirectoryService _externalUserDirectoryService;
    private readonly ILogger<DirectorySettingsController> _logger;

    public DirectorySettingsController(
        IDirectorySettingsService settingsService,
        IExternalUserDirectoryService externalUserDirectoryService,
        ILogger<DirectorySettingsController> logger)
    {
        _settingsService = settingsService;
        _externalUserDirectoryService = externalUserDirectoryService;
        _logger = logger;
    }

    [HttpGet("imap")]
    public async Task<ActionResult<ImapSettingsResponse>> GetImap(CancellationToken ct) =>
        Ok(await _settingsService.GetImapAsync(ct));

    [HttpPost("imap")]
    public async Task<ActionResult> SaveImap([FromBody] ImapSettingsRequest request, CancellationToken ct)
    {
        try
        {
            var saved = await _settingsService.SaveImapAsync(request, ct);
            return Ok(new { success = true, settings = saved });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { success = false, detail = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save IMAP settings");
            return StatusCode(500, new { success = false, detail = "IMAP ayarlari kaydedilemedi" });
        }
    }

    [HttpPost("imap/test")]
    public async Task<ActionResult<DirectorySettingsTestResult>> TestImap([FromBody] ImapSettingsRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _settingsService.TestImapAsync(request, ct);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { success = false, message = ex.Message, tested_at = DateTime.UtcNow });
        }
    }

    [HttpPost("imap/inbox")]
    public async Task<ActionResult<ImapInboxPreviewResponse>> PreviewInbox([FromBody] ImapInboxRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _settingsService.PreviewInboxAsync(request, ct);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { success = false, message = ex.Message, tested_at = DateTime.UtcNow });
        }
    }

    [HttpPost("imap/message")]
    public async Task<ActionResult<ImapMessageContentResponse>> GetInboxMessage([FromBody] ImapMessageContentRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _settingsService.GetInboxMessageAsync(request, ct);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { success = false, message = ex.Message, tested_at = DateTime.UtcNow });
        }
    }

    [HttpGet("ldap")]
    public async Task<ActionResult<LdapSettingsResponse>> GetLdap(CancellationToken ct) =>
        Ok(await _settingsService.GetLdapAsync(ct));

    [HttpPost("ldap")]
    public async Task<ActionResult> SaveLdap([FromBody] LdapSettingsRequest request, CancellationToken ct)
    {
        try
        {
            var saved = await _settingsService.SaveLdapAsync(request, ct);
            return Ok(new { success = true, settings = saved });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { success = false, detail = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save LDAP settings");
            return StatusCode(500, new { success = false, detail = "LDAP ayarlari kaydedilemedi" });
        }
    }

    [HttpPost("ldap/test")]
    public async Task<ActionResult<DirectorySettingsTestResult>> TestLdap([FromBody] LdapSettingsRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _settingsService.TestLdapAsync(request, ct);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { success = false, message = ex.Message, tested_at = DateTime.UtcNow });
        }
    }

    [HttpGet("external-user-db")]
    public async Task<ActionResult<ExternalUserDbSettingsResponse>> GetExternalUserDb(CancellationToken ct) =>
        Ok(await _externalUserDirectoryService.GetSettingsAsync(ct));

    [HttpPost("external-user-db")]
    public async Task<ActionResult> SaveExternalUserDb([FromBody] ExternalUserDbSettingsRequest request, CancellationToken ct)
    {
        try
        {
            var saved = await _externalUserDirectoryService.SaveSettingsAsync(request, ct);
            return Ok(new { success = true, settings = saved });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { success = false, detail = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save external user DB settings");
            return StatusCode(500, new { success = false, detail = "Harici kullanici veritabani ayarlari kaydedilemedi" });
        }
    }

    [HttpPost("external-user-db/test")]
    public async Task<ActionResult<ExternalUserLookupResult>> TestExternalUserDb([FromBody] ExternalUserDbSettingsRequest request, CancellationToken ct)
    {
        var result = await _externalUserDirectoryService.TestConnectionAsync(request, ct);
        return Ok(result);
    }

    [HttpPost("external-user-db/lookup")]
    public async Task<ActionResult<ExternalUserLookupResult>> TestExternalUserLookup([FromBody] ExternalUserLookupRequest request, CancellationToken ct)
    {
        var result = await _externalUserDirectoryService.TestLookupAsync(request, ct);
        return Ok(result);
    }
}
