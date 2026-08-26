using DLP.RiskAnalyzer.Analyzer.Models;
using DLP.RiskAnalyzer.Analyzer.Services;
using Microsoft.AspNetCore.Mvc;

namespace DLP.RiskAnalyzer.Analyzer.Controllers;

[ApiController]
[Route("api/settings")]
public class DirectorySettingsController : ControllerBase
{
    private readonly IDirectorySettingsService _settingsService;
    private readonly ILogger<DirectorySettingsController> _logger;

    public DirectorySettingsController(
        IDirectorySettingsService settingsService,
        ILogger<DirectorySettingsController> logger)
    {
        _settingsService = settingsService;
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

    [HttpPost("imap/messages/{messageId}")]
    public async Task<ActionResult<ImapMessageContentResponse>> GetInboxMessageById(
        string messageId,
        [FromBody] ImapMessageContentRequest? request,
        CancellationToken ct)
    {
        try
        {
            request ??= new ImapMessageContentRequest();
            request.MessageId = messageId;
            var result = await _settingsService.GetInboxMessageAsync(request, ct);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { success = false, message = ex.Message, tested_at = DateTime.UtcNow });
        }
    }

    [HttpPost("imap/attachment")]
    public async Task<IActionResult> DownloadInboxAttachment([FromBody] ImapAttachmentRequest request, CancellationToken ct)
    {
        try
        {
            var attachment = await _settingsService.GetInboxAttachmentAsync(request, ct);
            return File(attachment.Content, attachment.ContentType, attachment.FileName);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "IMAP attachment download failed for message {MessageId}", request.MessageId);
            return StatusCode(500, new { success = false, message = "Mail eki indirilemedi" });
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

    [HttpGet("ldap/attributes")]
    public async Task<ActionResult<LdapAttributeDumpResult>> DumpLdapAttributes(
        [FromQuery] string username,
        [FromQuery(Name = "include_operational")] bool includeOperational,
        CancellationToken ct)
    {
        var result = await _settingsService.DumpLdapUserAttributesAsync(username, includeOperational, ct);
        return Ok(result);
    }

}
