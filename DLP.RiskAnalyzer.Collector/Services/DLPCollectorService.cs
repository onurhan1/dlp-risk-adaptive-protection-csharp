using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using StackExchange.Redis;
using System.Net;
using System.Text;
using DLP.RiskAnalyzer.Shared.Models;

namespace DLP.RiskAnalyzer.Collector.Services;

/// <summary>
/// Forcepoint DLP API Collector Service
/// </summary>
public class DLPCollectorService
{
    private readonly HttpClient _httpClient;
    private readonly IConnectionMultiplexer _redis;
    private readonly DLPConfig _dlpConfig;
    private readonly RedisConfig _redisConfig;
    private readonly ILogger<DLPCollectorService> _logger;
    
    private string? _accessToken;
    private DateTime _tokenExpiry = DateTime.MinValue;

    public DLPCollectorService(
        HttpClient httpClient,
        IConnectionMultiplexer redis,
        IOptions<DLPConfig> dlpConfig,
        IOptions<RedisConfig> redisConfig,
        ILogger<DLPCollectorService> logger)
    {
        _httpClient = httpClient;
        _redis = redis;
        _dlpConfig = dlpConfig.Value;
        _redisConfig = redisConfig.Value;
        _logger = logger;

        // Base address is set by HttpClient factory in Program.cs
        // SSL certificate validation bypass is handled by HttpClientHandler
        if (_httpClient.BaseAddress == null)
        {
            _httpClient.BaseAddress = new Uri($"https://{_dlpConfig.ManagerIP}:{_dlpConfig.ManagerPort}");
        }
    }

    /// <summary>
    /// Get JWT access token from Forcepoint DLP API
    /// </summary>
    public async Task<string> GetAccessTokenAsync()
    {
        // Check if token is still valid
        if (!string.IsNullOrEmpty(_accessToken) && DateTime.UtcNow < _tokenExpiry)
        {
            return _accessToken;
        }

        try
        {
            var url = "/dlp/rest/v1/auth/access-token";
            var requestBody = new
            {
                username = _dlpConfig.Username,
                password = _dlpConfig.Password
            };

            var json = JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, content);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            var tokenResponse = JsonConvert.DeserializeObject<AccessTokenResponse>(responseContent);

            _accessToken = tokenResponse?.AccessToken ?? 
                          tokenResponse?.Token ?? 
                          throw new Exception("No token received from DLP API");

            // Set expiry (subtract 60 seconds for safety)
            _tokenExpiry = DateTime.UtcNow.AddSeconds((tokenResponse?.ExpiresIn ?? 3600) - 60);

            _logger.LogInformation("Access token obtained, expires at {Expiry}", _tokenExpiry);
            return _accessToken;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get access token");
            throw;
        }
    }

    /// <summary>
    /// Fetch incidents from DLP API
    /// </summary>
    public async Task<List<DLPIncident>> FetchIncidentsAsync(DateTime startTime, DateTime endTime, int page = 1, int pageSize = 100)
    {
        try
        {
            var token = await GetAccessTokenAsync();

            var url = $"/dlp/rest/v1/incidents?" +
                     $"startTime={startTime:yyyy-MM-ddTHH:mm:ssZ}&" +
                     $"endTime={endTime:yyyy-MM-ddTHH:mm:ssZ}&" +
                     $"page={page}&pageSize={pageSize}";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var incidentResponse = JsonConvert.DeserializeObject<DLPIncidentResponse>(content);

            return incidentResponse?.Incidents ?? new List<DLPIncident>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch incidents");
            throw;
        }
    }

    /// <summary>
    /// Push incident to Redis stream
    /// </summary>
    public async Task PushToRedisStreamAsync(Incident incident)
    {
        try
        {
            var db = _redis.GetDatabase();
            var streamName = "dlp:incidents";

            var fields = new NameValueEntry[]
            {
                new("user", incident.UserEmail),
                new("department", incident.Department ?? ""),
                new("severity", incident.Severity.ToString()),
                new("data_type", incident.DataType ?? ""),
                new("timestamp", incident.Timestamp.ToString("O")),
                new("policy", incident.Policy ?? ""),
                new("channel", incident.Channel ?? "")
            };

            await db.StreamAddAsync(streamName, fields);
            _logger.LogDebug("Incident pushed to Redis stream: {UserEmail} at {Timestamp}", 
                incident.UserEmail, incident.Timestamp);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to push incident to Redis stream");
            throw;
        }
    }
}

/// <summary>
/// Access token response model
/// </summary>
public class AccessTokenResponse
{
    [JsonProperty("accessToken")]
    public string? AccessToken { get; set; }
    
    [JsonProperty("token")]
    public string? Token { get; set; }
    
    [JsonProperty("expiresIn")]
    public int ExpiresIn { get; set; }
}

/// <summary>
/// DLP Incident model (from API)
/// </summary>
public class DLPIncident
{
    public int Id { get; set; }
    public string User { get; set; } = string.Empty;
    public string? Department { get; set; }
    public int Severity { get; set; }
    public string? DataType { get; set; }
    public DateTime Timestamp { get; set; }
    public string? Policy { get; set; }
    public string? Channel { get; set; }
}

/// <summary>
/// DLP Incident response model
/// </summary>
public class DLPIncidentResponse
{
    public List<DLPIncident> Incidents { get; set; } = new();
    public int Total { get; set; }
}

