using Microsoft.AspNetCore.Mvc;
using DLP.RiskAnalyzer.Analyzer.Data;
using DLP.RiskAnalyzer.Analyzer.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;

namespace DLP.RiskAnalyzer.Analyzer.Controllers;

[ApiController]
[Route("api/settings/ai")]
public class AISettingsController : ControllerBase
{
    private readonly AnalyzerDbContext _context;
    private readonly IDataProtector _protector;
    private readonly ILogger<AISettingsController> _logger;
    private readonly OpenAIService? _openAIService;
    private readonly CopilotService? _copilotService;
    private readonly AzureOpenAIService? _azureOpenAIService;

    private const string OpenAIKeyKey = "ai_openai_api_key_protected";
    private const string CopilotKeyKey = "ai_copilot_api_key_protected";
    private const string AzureKeyKey = "ai_azure_openai_key_protected";
    private const string AzureEndpointKey = "ai_azure_openai_endpoint";
    private const string ModelProviderKey = "ai_model_provider";
    private const string ModelNameKey = "ai_model_name";
    private const string TemperatureKey = "ai_temperature";
    private const string MaxTokensKey = "ai_max_tokens";
    private const string EnabledKey = "ai_enabled";

    public AISettingsController(
        AnalyzerDbContext context,
        IDataProtectionProvider dataProtectionProvider,
        ILogger<AISettingsController> logger,
        IServiceProvider serviceProvider)
    {
        _context = context;
        _protector = dataProtectionProvider.CreateProtector("AI.SettingsProtector");
        _logger = logger;
        
        // Get OpenAIService if available (optional dependency)
        try
        {
            _openAIService = serviceProvider.GetService<OpenAIService>();
        }
        catch
        {
            _openAIService = null;
        }

        // Get CopilotService if available (optional dependency)
        try
        {
            _copilotService = serviceProvider.GetService<CopilotService>();
        }
        catch
        {
            _copilotService = null;
        }

        // Get AzureOpenAIService if available (optional dependency)
        try
        {
            _azureOpenAIService = serviceProvider.GetService<AzureOpenAIService>();
        }
        catch
        {
            _azureOpenAIService = null;
        }
    }

    [HttpGet]
    public async Task<ActionResult<object>> GetAISettings()
    {
        try
        {
            // Clear change tracker to force fresh read from database
            _context.ChangeTracker.Clear();
            
            // Use AsNoTracking to avoid caching issues
            var settings = await _context.SystemSettings
                .AsNoTracking()
                .Where(s => s.Key.StartsWith("ai_"))
                .ToDictionaryAsync(s => s.Key, s => s.Value);

            var modelProvider = settings.GetValueOrDefault(ModelProviderKey, "local");
            _logger.LogInformation("Getting AI settings. Model Provider from DB: {Provider}", modelProvider);
            _logger.LogInformation("All AI settings keys found in DB: {Keys} (Count: {Count})", 
                string.Join(", ", settings.Keys), settings.Count);
            
            // Log all AI settings for debugging
            foreach (var setting in settings)
            {
                if (setting.Key == ModelProviderKey)
                {
                    _logger.LogInformation("Found model_provider in DB: {Key} = {Value}", setting.Key, setting.Value);
                }
            }
            
            // Check OpenAI key specifically
            var hasOpenAIKey = settings.ContainsKey(OpenAIKeyKey);
            var openAIKeyValue = settings.GetValueOrDefault(OpenAIKeyKey, "");
            var isOpenAIKeyEmpty = string.IsNullOrEmpty(openAIKeyValue);
            _logger.LogInformation("OpenAI key check - HasKey: {HasKey}, IsEmpty: {IsEmpty}, KeyValueLength: {Length}", 
                hasOpenAIKey, isOpenAIKeyEmpty, openAIKeyValue?.Length ?? 0);

            var response = new
            {
                openai_api_key_set = settings.ContainsKey(OpenAIKeyKey) && !string.IsNullOrEmpty(settings[OpenAIKeyKey]),
                copilot_api_key_set = settings.ContainsKey(CopilotKeyKey) && !string.IsNullOrEmpty(settings[CopilotKeyKey]),
                azure_openai_endpoint = settings.GetValueOrDefault(AzureEndpointKey, ""),
                azure_openai_key_set = settings.ContainsKey(AzureKeyKey) && !string.IsNullOrEmpty(settings[AzureKeyKey]),
                model_provider = modelProvider,
                model_name = settings.GetValueOrDefault(ModelNameKey, "gpt-4o-mini"),
                temperature = double.TryParse(settings.GetValueOrDefault(TemperatureKey, "0.7"), out var temp) ? temp : 0.7,
                max_tokens = int.TryParse(settings.GetValueOrDefault(MaxTokensKey, "1000"), out var tokens) ? tokens : 1000,
                enabled = bool.TryParse(settings.GetValueOrDefault(EnabledKey, "true"), out var enabled) ? enabled : true
            };
            
            _logger.LogInformation("Returning AI settings. Model Provider: {Provider}", response.model_provider);
            
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting AI settings");
            return StatusCode(500, new { detail = "Failed to get AI settings" });
        }
    }

    [HttpPost]
    public async Task<ActionResult> SaveAISettings([FromBody] AISettingsRequest request)
    {
        try
        {
            _logger.LogInformation("Received SaveAISettings request. ModelProvider: {Provider}, ModelName: {ModelName}", 
                request.ModelProvider, request.ModelName);
            
            // Always save model_provider, even if it's null (will default to "local")
            var modelProvider = !string.IsNullOrWhiteSpace(request.ModelProvider) 
                ? request.ModelProvider 
                : "local";
            
            _logger.LogInformation("Saving AI settings. Model Provider: {Provider}", modelProvider);
            
            // Save API keys only if provided (not null and not empty)
            if (!string.IsNullOrWhiteSpace(request.OpenAIApiKey))
            {
                _logger.LogDebug("Saving OpenAI API key");
                await SaveSettingAsync(OpenAIKeyKey, request.OpenAIApiKey, encrypt: true);
            }
            if (!string.IsNullOrWhiteSpace(request.CopilotApiKey))
            {
                _logger.LogDebug("Saving Copilot API key");
                await SaveSettingAsync(CopilotKeyKey, request.CopilotApiKey, encrypt: true);
            }
            if (!string.IsNullOrWhiteSpace(request.AzureOpenAIEndpoint))
            {
                _logger.LogDebug("Saving Azure OpenAI endpoint: {Endpoint}", request.AzureOpenAIEndpoint);
                await SaveSettingAsync(AzureEndpointKey, request.AzureOpenAIEndpoint, encrypt: false);
            }
            if (!string.IsNullOrWhiteSpace(request.AzureOpenAIKey))
            {
                _logger.LogDebug("Saving Azure OpenAI API key");
                await SaveSettingAsync(AzureKeyKey, request.AzureOpenAIKey, encrypt: true);
            }
            
            // Always save these settings (they should always have values)
            _logger.LogDebug("Saving model_provider: {Provider}", modelProvider);
            await SaveSettingAsync(ModelProviderKey, modelProvider, encrypt: false);
            
            var modelName = !string.IsNullOrWhiteSpace(request.ModelName) ? request.ModelName : "gpt-4o-mini";
            _logger.LogDebug("Saving model_name: {ModelName}", modelName);
            await SaveSettingAsync(ModelNameKey, modelName, encrypt: false);
            
            await SaveSettingAsync(TemperatureKey, request.Temperature?.ToString() ?? "0.7", encrypt: false);
            await SaveSettingAsync(MaxTokensKey, request.MaxTokens?.ToString() ?? "1000", encrypt: false);
            await SaveSettingAsync(EnabledKey, request.Enabled?.ToString() ?? "true", encrypt: false);

            // Note: SaveSettingAsync already calls SaveChangesAsync for each setting
            // But we call it once more here to ensure all changes are saved
            var savedCount = await _context.SaveChangesAsync();
            _logger.LogInformation("AI settings saved successfully. Changes: {Count}, Model Provider: {Provider}", 
                savedCount, modelProvider);
            
            // Clear change tracker and verify the saved value from database
            _context.ChangeTracker.Clear();
            var savedProvider = await _context.SystemSettings
                .AsNoTracking()
                .Where(s => s.Key == ModelProviderKey)
                .Select(s => s.Value)
                .FirstOrDefaultAsync();
            _logger.LogInformation("Verified saved model_provider value from DB: {Provider}", savedProvider);
            
            if (savedProvider != modelProvider)
            {
                _logger.LogError("WARNING: Saved model_provider ({Saved}) does not match expected value ({Expected})!", 
                    savedProvider, modelProvider);
            }

            return Ok(new { success = true, message = "AI settings saved successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving AI settings");
            return StatusCode(500, new { detail = "Failed to save AI settings", message = ex.Message });
        }
    }

    [HttpPost("test")]
    public async Task<ActionResult> TestConnection([FromBody] TestConnectionRequest request)
    {
        try
        {
            var provider = request.Provider?.ToLower() ?? "local";

            if (provider == "local")
            {
                return Ok(new { success = true, message = "Local model (Z-score) is always available" });
            }

            // Get API key from request or from stored settings
            string? apiKey = null;
            string? model = request.Model;

            if (provider == "openai")
            {
                // Try to get API key from request first
                if (!string.IsNullOrWhiteSpace(request.ApiKey))
                {
                    apiKey = request.ApiKey;
                }
                else
                {
                    // Get from stored settings
                    var settings = await _context.SystemSettings
                        .Where(s => s.Key == OpenAIKeyKey)
                        .FirstOrDefaultAsync();

                    if (settings != null && !string.IsNullOrEmpty(settings.Value))
                    {
                        try
                        {
                            apiKey = _protector.Unprotect(settings.Value);
                        }
                        catch
                        {
                            return BadRequest(new { success = false, message = "Failed to decrypt stored API key" });
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    return BadRequest(new { success = false, message = "OpenAI API key is required" });
                }

                // Test OpenAI connection
                if (_openAIService != null)
                {
                    var success = await _openAIService.TestConnectionAsync(apiKey, model);
                    if (success)
                    {
                        return Ok(new { success = true, message = "OpenAI API connection test successful" });
                    }
                    else
                    {
                        return BadRequest(new { success = false, message = "OpenAI API connection test failed. Please check your API key." });
                    }
                }
                else
                {
                    return StatusCode(500, new { success = false, message = "OpenAI service is not available" });
                }
            }

            // For Copilot and Azure, get API key from request or stored settings
            if (provider == "copilot")
            {
                string? copilotApiKey = null;
                
                // Try to get API key from request first
                if (!string.IsNullOrWhiteSpace(request.ApiKey))
                {
                    copilotApiKey = request.ApiKey;
                }
                else
                {
                    // Get from stored settings
                    var copilotSetting = await _context.SystemSettings
                        .Where(s => s.Key == CopilotKeyKey)
                        .FirstOrDefaultAsync();

                    if (copilotSetting != null && !string.IsNullOrEmpty(copilotSetting.Value))
                    {
                        try
                        {
                            copilotApiKey = _protector.Unprotect(copilotSetting.Value);
                        }
                        catch
                        {
                            return BadRequest(new { success = false, message = "Failed to decrypt stored Copilot API key" });
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(copilotApiKey))
                {
                    return BadRequest(new { success = false, message = "GitHub Copilot API key is required. Please enter your API key and try again." });
                }

                // Basic validation: GitHub Copilot API keys typically start with "ghp_" or are GitHub tokens
                if (!copilotApiKey.StartsWith("ghp_") && !copilotApiKey.StartsWith("github_pat_") && !copilotApiKey.StartsWith("gho_"))
                {
                    // Still allow it, but log a warning
                    _logger.LogWarning("Copilot API key format may be incorrect (expected ghp_, github_pat_, or gho_ prefix)");
                }

                // Test GitHub Copilot connection
                if (_copilotService != null)
                {
                    var success = await _copilotService.TestConnectionAsync(copilotApiKey);
                    if (success)
                    {
                        // Optionally check Copilot access (may fail for some token types, that's OK)
                        var hasCopilotAccess = await _copilotService.CheckCopilotAccessAsync(copilotApiKey);
                        var message = hasCopilotAccess 
                            ? "GitHub Copilot API connection test successful. Copilot access confirmed." 
                            : "GitHub API connection test successful. Note: Copilot-specific access could not be verified.";
                        
                        return Ok(new { success = true, message = message });
                    }
                    else
                    {
                        return BadRequest(new { success = false, message = "GitHub Copilot API connection test failed. Please check your API key and ensure it has the required permissions." });
                    }
                }
                else
                {
                    return StatusCode(500, new { success = false, message = "GitHub Copilot service is not available" });
                }
            }

            // For Azure OpenAI
            if (provider == "azure")
            {
                string? azureApiKey = null;
                string? endpoint = null;

                // Try to get from request first
                if (!string.IsNullOrWhiteSpace(request.ApiKey))
                {
                    azureApiKey = request.ApiKey;
                }
                else
                {
                    var azureKeySetting = await _context.SystemSettings
                        .Where(s => s.Key == AzureKeyKey)
                        .FirstOrDefaultAsync();

                    if (azureKeySetting != null && !string.IsNullOrEmpty(azureKeySetting.Value))
                    {
                        try
                        {
                            azureApiKey = _protector.Unprotect(azureKeySetting.Value);
                        }
                        catch
                        {
                            return BadRequest(new { success = false, message = "Failed to decrypt stored Azure OpenAI API key" });
                        }
                    }
                }

                if (!string.IsNullOrWhiteSpace(request.Endpoint))
                {
                    endpoint = request.Endpoint;
                }
                else
                {
                    var endpointSetting = await _context.SystemSettings
                        .Where(s => s.Key == AzureEndpointKey)
                        .FirstOrDefaultAsync();

                    if (endpointSetting != null)
                    {
                        endpoint = endpointSetting.Value;
                    }
                }

                if (string.IsNullOrWhiteSpace(azureApiKey))
                {
                    return BadRequest(new { success = false, message = "Azure OpenAI API key is required" });
                }

                if (string.IsNullOrWhiteSpace(endpoint))
                {
                    return BadRequest(new { success = false, message = "Azure OpenAI endpoint is required" });
                }

                // Test Azure OpenAI connection
                if (_azureOpenAIService != null)
                {
                    var success = await _azureOpenAIService.TestConnectionAsync(endpoint, azureApiKey);
                    if (success)
                    {
                        return Ok(new { success = true, message = "Azure OpenAI API connection test successful" });
                    }
                    else
                    {
                        return BadRequest(new { success = false, message = "Azure OpenAI API connection test failed. Please check your endpoint URL and API key." });
                    }
                }
                else
                {
                    return StatusCode(500, new { success = false, message = "Azure OpenAI service is not available" });
                }
            }

            return BadRequest(new { success = false, message = $"Unknown provider: {provider}" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing connection for {Provider}", request.Provider);
            return StatusCode(500, new { detail = "Failed to test connection", message = ex.Message });
        }
    }

    private async Task SaveSettingAsync(string key, string? value, bool encrypt)
    {
        if (value == null || string.IsNullOrWhiteSpace(value)) 
        {
            _logger.LogWarning("Attempted to save setting {Key} with null or empty value. Skipping.", key);
            return;
        }

        var finalValue = encrypt ? _protector.Protect(value) : value;

        // Clear change tracker to avoid conflicts
        _context.ChangeTracker.Clear();

        // Use AsNoTracking to avoid change tracker conflicts
        var existing = await _context.SystemSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == key);

        if (existing == null)
        {
            // Add new setting
            var newSetting = new SystemSetting
            {
                Key = key,
                Value = finalValue,
                UpdatedAt = DateTime.UtcNow
            };
            _context.SystemSettings.Add(newSetting);
            _logger.LogInformation("Adding new setting: {Key}", key);
        }
        else
        {
            // Update existing setting - must use Update() to mark as modified
            existing.Value = finalValue;
            existing.UpdatedAt = DateTime.UtcNow;
            _context.SystemSettings.Update(existing);
            _logger.LogInformation("Updating existing setting: {Key}", key);
        }
        
        // Save changes immediately for this setting
        try
        {
            var savedCount = await _context.SaveChangesAsync();
            _logger.LogInformation("Saved setting {Key}. Rows affected: {Count}", key, savedCount);
            
            if (savedCount == 0)
            {
                _logger.LogWarning("WARNING: SaveChangesAsync returned 0 rows affected for {Key}!", key);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving setting {Key} = {Value}", key, value);
            throw;
        }
    }
}

public class AISettingsRequest
{
    [JsonPropertyName("openai_api_key")]
    public string? OpenAIApiKey { get; set; }
    
    [JsonPropertyName("copilot_api_key")]
    public string? CopilotApiKey { get; set; }
    
    [JsonPropertyName("azure_openai_endpoint")]
    public string? AzureOpenAIEndpoint { get; set; }
    
    [JsonPropertyName("azure_openai_key")]
    public string? AzureOpenAIKey { get; set; }
    
    [JsonPropertyName("model_provider")]
    public string? ModelProvider { get; set; }
    
    [JsonPropertyName("model_name")]
    public string? ModelName { get; set; }
    
    [JsonPropertyName("temperature")]
    public double? Temperature { get; set; }
    
    [JsonPropertyName("max_tokens")]
    public int? MaxTokens { get; set; }
    
    [JsonPropertyName("enabled")]
    public bool? Enabled { get; set; }
}

public class TestConnectionRequest
{
    public string? Provider { get; set; }
    public string? ApiKey { get; set; }
    public string? Model { get; set; }
    public string? Endpoint { get; set; }
}

