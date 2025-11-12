using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using StackExchange.Redis;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
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
    /// According to Forcepoint DLP REST API v1 documentation:
    /// POST https://&lt;DLP Manager IP&gt;:&lt;DLP Manager port&gt;/dlp/rest/v1/auth/access-token
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
            // Forcepoint DLP REST API v1 Authentication endpoint
            // According to Forcepoint DLP REST API documentation:
            // POST https://<DLP Manager IP>:<DLP Manager port>/dlp/rest/v1/auth/access-token
            // Request format: application/x-www-form-urlencoded (not JSON)
            var url = "/dlp/rest/v1/auth/access-token";
            
            // Request body: username=xxx&password=yyy (form-urlencoded format)
            var formData = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("username", _dlpConfig.Username),
                new KeyValuePair<string, string>("password", _dlpConfig.Password)
            };
            var content = new FormUrlEncodedContent(formData);

            _logger.LogDebug("Requesting access token from {BaseAddress}{Url}", _httpClient.BaseAddress, url);
            
            var response = await _httpClient.PostAsync(url, content);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to get access token. Status: {Status}, Response: {Response}", 
                    response.StatusCode, errorContent);
                response.EnsureSuccessStatusCode();
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var tokenResponse = JsonConvert.DeserializeObject<AccessTokenResponse>(responseContent);

            // Forcepoint DLP API returns access_token (snake_case), but some versions use accessToken (camelCase)
            _accessToken = tokenResponse?.AccessToken ?? 
                          tokenResponse?.Token ?? 
                          throw new Exception("No token received from DLP API. Response: " + responseContent);

            // Set expiry (subtract 60 seconds for safety)
            // Forcepoint DLP tokens typically expire in 1 hour (3600 seconds) or access_token_expires_in field
            var expiresIn = tokenResponse?.ExpiresIn ?? tokenResponse?.AccessTokenExpiresIn ?? 3600;
            _tokenExpiry = DateTime.UtcNow.AddSeconds(expiresIn - 60);

            _logger.LogInformation("Access token obtained successfully, expires at {Expiry}", _tokenExpiry);
            return _accessToken;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get access token from Forcepoint DLP API");
            throw;
        }
    }

    /// <summary>
    /// Fetch incidents from Forcepoint DLP API
    /// According to Forcepoint DLP REST API v1 documentation:
    /// POST https://&lt;DLP Manager IP&gt;:&lt;DLP Manager port&gt;/dlp/rest/v1/incidents
    /// Body: { "type": "INCIDENTS", "from_date": "dd/MM/yyyy HH:mm:ss", "to_date": "dd/MM/yyyy HH:mm:ss" }
    /// </summary>
    public async Task<List<DLPIncident>> FetchIncidentsAsync(DateTime startTime, DateTime endTime, int page = 1, int pageSize = 100)
    {
        try
        {
            // Step 1: Authenticate and get access token
            var token = await GetAccessTokenAsync();

            // Step 2: Build request body according to Forcepoint DLP API format
            // Format dates as "dd/MM/yyyy HH:mm:ss" (Forcepoint DLP API format)
            var fromDate = startTime.ToUniversalTime().ToString("dd/MM/yyyy HH:mm:ss");
            var toDate = endTime.ToUniversalTime().ToString("dd/MM/yyyy HH:mm:ss");

            var incidentsUrl = "/dlp/rest/v1/incidents/";
            var requestBody = new
            {
                type = "INCIDENTS",
                from_date = fromDate,
                to_date = toDate
            };

            var jsonBody = JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            _logger.LogDebug("Fetching incidents from {BaseAddress}{Url} with body: {Body}", 
                _httpClient.BaseAddress, incidentsUrl, jsonBody);

            // Step 3: Create POST request with Bearer token authentication
            var request = new HttpRequestMessage(HttpMethod.Post, incidentsUrl);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            request.Content = content;

            // Step 4: Send request
            var response = await _httpClient.SendAsync(request);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to fetch incidents. Status: {Status}, Response: {Response}", 
                    response.StatusCode, errorContent);
                response.EnsureSuccessStatusCode();
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            
            // Forcepoint DLP API may return incidents as array or object with incidents property
            List<DLPIncident> incidents;
            try
            {
                // Try to deserialize as DLPIncidentResponse first
                var incidentResponse = JsonConvert.DeserializeObject<DLPIncidentResponse>(responseContent);
                incidents = incidentResponse?.Incidents ?? new List<DLPIncident>();
            }
            catch
            {
                // If that fails, try to deserialize as array directly
                try
                {
                    incidents = JsonConvert.DeserializeObject<List<DLPIncident>>(responseContent) ?? new List<DLPIncident>();
                }
                catch
                {
                    _logger.LogWarning("Unexpected response format from DLP API: {Response}", responseContent);
                    incidents = new List<DLPIncident>();
                }
            }

            _logger.LogInformation("Fetched {Count} incidents from Forcepoint DLP API", incidents.Count);

            return incidents;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch incidents from Forcepoint DLP API");
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
    // Forcepoint DLP API returns access_token (snake_case), but some versions use accessToken (camelCase)
    // Support both formats by checking both property names during deserialization
    [JsonProperty("access_token")]
    public string? AccessTokenSnakeCase { get; set; }
    
    [JsonProperty("accessToken")]
    public string? AccessTokenCamelCase { get; set; }
    
    // Property that returns the token from either format
    public string? AccessToken => AccessTokenSnakeCase ?? AccessTokenCamelCase;
    
    [JsonProperty("token")]
    public string? Token { get; set; }
    
    [JsonProperty("access_token_expires_in")]
    public int? ExpiresInSnakeCase { get; set; }
    
    [JsonProperty("expiresIn")]
    public int? ExpiresInCamelCase { get; set; }
    
    // Property that returns expires_in from either format
    public int? ExpiresIn => ExpiresInSnakeCase ?? ExpiresInCamelCase;
    
    // Alias for ExpiresIn
    public int? AccessTokenExpiresIn => ExpiresIn;
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

