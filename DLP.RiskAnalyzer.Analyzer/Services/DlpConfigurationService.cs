using System.Diagnostics;
using System.Text.Json;
using DLP.RiskAnalyzer.Analyzer.Data;
using DLP.RiskAnalyzer.Analyzer.Models;
using DLP.RiskAnalyzer.Shared.Constants;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace DLP.RiskAnalyzer.Analyzer.Services;

public class DlpConfigurationService
{
    private const string ManagerIpKey = "dlp_manager_ip";
    private const string ManagerPortKey = "dlp_manager_port";
    private const string UsernameKey = "dlp_username";
    private const string PasswordKey = "dlp_password_protected";
    private const string UseHttpsKey = "dlp_use_https";
    private const string TimeoutKey = "dlp_timeout_seconds";

    private readonly AnalyzerDbContext _context;
    private readonly IDataProtector _protector;
    private readonly ILogger<DlpConfigurationService> _logger;
    private readonly IConnectionMultiplexer _redis;

    public DlpConfigurationService(
        AnalyzerDbContext context,
        IDataProtectionProvider dataProtectionProvider,
        IConnectionMultiplexer redis,
        ILogger<DlpConfigurationService> logger)
    {
        _context = context;
        _protector = dataProtectionProvider.CreateProtector("DLP.ApiSecretProtector");
        _redis = redis;
        _logger = logger;
    }

    public async Task<DlpApiSettingsResponse> GetAsync(bool includeSensitive = false, CancellationToken cancellationToken = default)
    {
        var settings = await _context.SystemSettings.AsNoTracking()
            .Where(s => s.Key.StartsWith("dlp_", StringComparison.OrdinalIgnoreCase))
            .ToListAsync(cancellationToken);

        var dict = settings.ToDictionary(s => s.Key, s => s);

        var response = new DlpApiSettingsResponse
        {
            ManagerIp = dict.TryGetValue(ManagerIpKey, out var ip) ? ip.Value : "localhost",
            ManagerPort = dict.TryGetValue(ManagerPortKey, out var portSetting) && int.TryParse(portSetting.Value, out var port)
                ? port
                : 8443,
            Username = dict.TryGetValue(UsernameKey, out var user) ? user.Value : string.Empty,
            UseHttps = dict.TryGetValue(UseHttpsKey, out var httpsSetting) && bool.TryParse(httpsSetting.Value, out var https)
                ? https
                : true,
            TimeoutSeconds = dict.TryGetValue(TimeoutKey, out var timeoutSetting) && int.TryParse(timeoutSetting.Value, out var timeout)
                ? timeout
                : 30,
            PasswordSet = dict.ContainsKey(PasswordKey),
            UpdatedAt = dict.Values.OrderByDescending(s => s.UpdatedAt).FirstOrDefault()?.UpdatedAt
        };

        if (includeSensitive && dict.TryGetValue(PasswordKey, out var passwordSetting))
        {
            try
            {
                var decrypted = _protector.Unprotect(passwordSetting.Value);
                return new DlpApiSensitiveSettingsResponse
                {
                    ManagerIp = response.ManagerIp,
                    ManagerPort = response.ManagerPort,
                    Username = response.Username,
                    UseHttps = response.UseHttps,
                    TimeoutSeconds = response.TimeoutSeconds,
                    PasswordSet = response.PasswordSet,
                    UpdatedAt = response.UpdatedAt,
                    Password = decrypted
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to decrypt DLP API password");
                throw new InvalidOperationException("Stored DLP API password cannot be decrypted. Please re-enter credentials.");
            }
        }

        return response;
    }

    public async Task<DlpApiSettingsResponse> SaveAsync(DlpApiSettingsRequest request, CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);

        var existing = await GetExistingSensitiveConfigAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            if (existing == null || string.IsNullOrWhiteSpace(existing.Password))
            {
                throw new InvalidOperationException("Password must be provided when configuring the DLP API for the first time.");
            }
            request.Password = existing.Password;
        }

        await UpsertSettingAsync(ManagerIpKey, request.ManagerIp.Trim(), cancellationToken);
        await UpsertSettingAsync(ManagerPortKey, request.ManagerPort.ToString(), cancellationToken);
        await UpsertSettingAsync(UsernameKey, request.Username.Trim(), cancellationToken);
        await UpsertSettingAsync(UseHttpsKey, request.UseHttps.ToString(), cancellationToken);
        await UpsertSettingAsync(TimeoutKey, request.TimeoutSeconds.ToString(), cancellationToken);

        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            var protectedPassword = _protector.Protect(request.Password);
            await UpsertSettingAsync(PasswordKey, protectedPassword, cancellationToken);
        }

        var response = await GetAsync(false, cancellationToken);

        await BroadcastConfigAsync(new DlpConfigBroadcastMessage
        {
            ManagerIp = response.ManagerIp,
            ManagerPort = response.ManagerPort,
            UseHttps = response.UseHttps,
            TimeoutSeconds = response.TimeoutSeconds,
            Username = response.Username,
            Password = request.Password ?? string.Empty,
            UpdatedAt = response.UpdatedAt ?? DateTime.UtcNow
        }, cancellationToken);

        return response;
    }

    public async Task<DlpApiTestResult> TestConnectionAsync(DlpApiSettingsRequest request, CancellationToken cancellationToken = default)
    {
        ValidateRequest(request, allowEmptyPassword: true);

        var effectiveConfig = await BuildEffectiveConfigAsync(request, cancellationToken);
        if (string.IsNullOrWhiteSpace(effectiveConfig.Password))
        {
            throw new InvalidOperationException("Password is required to test the DLP API connection.");
        }

        var result = new DlpApiTestResult { TestedAt = DateTime.UtcNow };
        try
        {
            using var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (_, _, _, _) => true
            };

            var baseUrl = BuildBaseUrl(effectiveConfig);
            using var client = new HttpClient(handler)
            {
                BaseAddress = new Uri(baseUrl),
                Timeout = TimeSpan.FromSeconds(effectiveConfig.TimeoutSeconds)
            };

            var requestMessage = new HttpRequestMessage(HttpMethod.Post, "/dlp/rest/v1/auth/access-token");
            requestMessage.Headers.Add("username", effectiveConfig.Username);
            requestMessage.Headers.Add("password", effectiveConfig.Password);

            var stopwatch = Stopwatch.StartNew();
            var response = await client.SendAsync(requestMessage, cancellationToken);
            stopwatch.Stop();

            result.LatencyMs = stopwatch.ElapsedMilliseconds;
            result.StatusCode = (int)response.StatusCode;

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                result.Success = false;
                result.Message = $"Failed ({response.StatusCode}). Response: {body}";
                return result;
            }

            result.Success = true;
            result.Message = $"Connection successful in {stopwatch.ElapsedMilliseconds} ms.";
            return result;
        }
        catch (TaskCanceledException ex)
        {
            result.Success = false;
            result.Message = $"Connection timed out: {ex.Message}";
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DLP API connection test failed");
            result.Success = false;
            result.Message = $"Connection failed: {ex.Message}";
            return result;
        }
    }

    public async Task<DlpApiSensitiveSettingsResponse> GetSensitiveConfigAsync(CancellationToken cancellationToken = default)
    {
        var response = await GetAsync(includeSensitive: true, cancellationToken);
        
        // Validate that settings are actually configured (not just defaults)
        // If ManagerIp is "localhost" and Username is empty, it means settings are not configured
        if (response.ManagerIp == "localhost" && string.IsNullOrWhiteSpace(response.Username))
        {
            _logger.LogWarning("DLP API settings are not configured in database. Please configure via Settings page.");
            throw new InvalidOperationException("DLP API settings are not configured. Please configure via Settings page in the dashboard.");
        }
        
        // If password is not set, throw exception
        if (!response.PasswordSet)
        {
            _logger.LogWarning("DLP API password is not configured in database. Please configure via Settings page.");
            throw new InvalidOperationException("DLP API password is not configured. Please configure via Settings page in the dashboard.");
        }
        
        return (DlpApiSensitiveSettingsResponse)response;
    }

    private async Task<DlpApiSensitiveSettingsResponse?> GetExistingSensitiveConfigAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await GetSensitiveConfigAsync(cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    private static void ValidateRequest(DlpApiSettingsRequest request, bool allowEmptyPassword = false)
    {
        if (string.IsNullOrWhiteSpace(request.ManagerIp))
        {
            throw new ArgumentException("Manager IP/Hostname is required", nameof(request.ManagerIp));
        }

        if (request.ManagerPort <= 0 || request.ManagerPort > 65535)
        {
            throw new ArgumentException("Manager port must be between 1 and 65535", nameof(request.ManagerPort));
        }

        if (request.TimeoutSeconds < 5 || request.TimeoutSeconds > 300)
        {
            throw new ArgumentException("Timeout must be between 5 and 300 seconds", nameof(request.TimeoutSeconds));
        }

        if (string.IsNullOrWhiteSpace(request.Username))
        {
            throw new ArgumentException("Username is required", nameof(request.Username));
        }

        if (!allowEmptyPassword && string.IsNullOrWhiteSpace(request.Password))
        {
            throw new ArgumentException("Password is required", nameof(request.Password));
        }
    }

    private async Task UpsertSettingAsync(string key, string value, CancellationToken cancellationToken)
    {
        // Clear change tracker to avoid conflicts
        _context.ChangeTracker.Clear();
        
        // Use AsNoTracking to avoid change tracker conflicts
        var entity = await _context.SystemSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == key, cancellationToken);
            
        if (entity == null)
        {
            // Add new setting
            var newSetting = new SystemSetting
            {
                Key = key,
                Value = value,
                UpdatedAt = DateTime.UtcNow
            };
            _context.SystemSettings.Add(newSetting);
            _logger.LogInformation("Adding new DLP setting: {Key}", key);
        }
        else
        {
            // Update existing setting - must use Update() to mark as modified
            entity.Value = value;
            entity.UpdatedAt = DateTime.UtcNow;
            _context.SystemSettings.Update(entity);
            _logger.LogInformation("Updating existing DLP setting: {Key}", key);
        }

        try
        {
            var savedCount = await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Saved DLP setting {Key}. Rows affected: {Count}", key, savedCount);
            
            if (savedCount == 0)
            {
                _logger.LogWarning("WARNING: SaveChangesAsync returned 0 rows affected for DLP setting {Key}!", key);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving DLP setting {Key} = {Value}", key, value?.Substring(0, Math.Min(10, value?.Length ?? 0)) ?? "null");
            throw;
        }
    }

    private async Task BroadcastConfigAsync(DlpConfigBroadcastMessage message, CancellationToken cancellationToken)
    {
        try
        {
            var payload = JsonSerializer.Serialize(message);
            var subscriber = _redis.GetSubscriber();
            await subscriber.PublishAsync(RedisChannel.Literal(DlpConstants.DlpConfigChannel), payload);
            _logger.LogInformation("Published DLP config update to Redis channel {Channel}", DlpConstants.DlpConfigChannel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish DLP config update to Redis");
        }
    }

    private async Task<DlpApiSensitiveSettingsResponse> BuildEffectiveConfigAsync(DlpApiSettingsRequest request, CancellationToken cancellationToken)
    {
        var existing = await GetExistingSensitiveConfigAsync(cancellationToken);

        return new DlpApiSensitiveSettingsResponse
        {
            ManagerIp = string.IsNullOrWhiteSpace(request.ManagerIp) ? existing?.ManagerIp ?? "localhost" : request.ManagerIp,
            ManagerPort = request.ManagerPort > 0 ? request.ManagerPort : existing?.ManagerPort ?? 8443,
            UseHttps = request.UseHttps,
            TimeoutSeconds = request.TimeoutSeconds > 0 ? request.TimeoutSeconds : existing?.TimeoutSeconds ?? 30,
            Username = string.IsNullOrWhiteSpace(request.Username) ? existing?.Username ?? string.Empty : request.Username.Trim(),
            Password = string.IsNullOrWhiteSpace(request.Password) ? existing?.Password ?? string.Empty : request.Password,
            PasswordSet = existing?.PasswordSet ?? false,
            UpdatedAt = existing?.UpdatedAt
        };
    }

    private static string BuildBaseUrl(DlpApiSettingsResponse settings)
    {
        var scheme = settings.UseHttps ? "https" : "http";
        return $"{scheme}://{settings.ManagerIp}:{settings.ManagerPort}";
    }
}

