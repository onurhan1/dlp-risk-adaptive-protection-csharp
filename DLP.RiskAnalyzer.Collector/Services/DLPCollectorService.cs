using DLP.RiskAnalyzer.Shared.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using StackExchange.Redis;
using System.Globalization;
using System.Net.Http;
using System.Text;

namespace DLP.RiskAnalyzer.Collector.Services;

/// <summary>
/// Forcepoint DLP API Collector Service
/// </summary>
public class DLPCollectorService : IDisposable
{
    private readonly DlpRuntimeConfigProvider _configProvider;
    private readonly IConnectionMultiplexer _redis;
    private readonly RedisConfig _redisConfig;
    private readonly ILogger<DLPCollectorService> _logger;
    private readonly object _clientLock = new();
    private HttpClient _httpClient;
    private DLPConfig _currentConfig;
    
    private string? _accessToken;
    private DateTime _tokenExpiry = DateTime.MinValue;

    public DLPCollectorService(
        DlpRuntimeConfigProvider configProvider,
        IConnectionMultiplexer redis,
        Microsoft.Extensions.Options.IOptions<RedisConfig> redisConfig,
        ILogger<DLPCollectorService> logger)
    {
        _configProvider = configProvider;
        _redis = redis;
        _redisConfig = redisConfig.Value;
        _logger = logger;

        _currentConfig = _configProvider.GetCurrent();
        _httpClient = CreateHttpClient(_currentConfig);
        _configProvider.ConfigChanged += OnConfigChanged;
        _logger.LogInformation("DLPCollectorService initialized for {Manager}:{Port}", _currentConfig.ManagerIP, _currentConfig.ManagerPort);
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
            // Get current config at runtime (may have been updated via UI)
            var config = _configProvider.GetCurrent();
            
            // Update HttpClient if config changed
            if (!ConfigEquals(_currentConfig, config))
            {
                lock (_clientLock)
                {
                    _currentConfig = config;
                    _httpClient?.Dispose();
                    _httpClient = CreateHttpClient(_currentConfig);
                    _logger.LogInformation("HttpClient updated with new DLP configuration");
                }
            }
            
            // Forcepoint DLP REST API v1 Authentication endpoint
            // According to Forcepoint DLP REST API documentation:
            // POST https://<DLP Manager IP>:<DLP Manager port>/dlp/rest/v1/auth/access-token
            // Note: Some DLP versions (8.9-9.0) expect username/password in headers, not body
            // Postman works with headers, so we'll use header-based authentication (same as DLPTestController)
            var url = "/dlp/rest/v1/auth/access-token";
            
            // Use header-based authentication (matching Postman format and DLPTestController)
            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("username", _currentConfig.Username);
            request.Headers.Add("password", _currentConfig.Password);

            _logger.LogDebug("Requesting access token from {BaseAddress}{Url} using header-based authentication", _httpClient.BaseAddress, url);
            
            var response = await _httpClient.SendAsync(request);
            
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
            
            // Log raw response for debugging (first 500 chars to avoid huge logs)
            _logger.LogDebug("DLP API Response (first 500 chars): {Response}", 
                responseContent.Length > 500 ? responseContent.Substring(0, 500) + "..." : responseContent);
            
            // Forcepoint DLP API may return incidents as array or object with incidents property
            List<DLPIncident> incidents;
            try
            {
                // Try to deserialize as DLPIncidentResponse first
                var incidentResponse = JsonConvert.DeserializeObject<DLPIncidentResponse>(responseContent);
                incidents = incidentResponse?.Incidents ?? new List<DLPIncident>();
                _logger.LogInformation("Deserialized as DLPIncidentResponse: {Count} incidents", incidents.Count);
                
                // Log first incident details for debugging
                if (incidents.Count > 0)
                {
                    var first = incidents[0];
                    _logger.LogDebug("First incident: Id={Id}, User={User}, Severity={Severity}, Timestamp={Timestamp}, Channel={Channel}", 
                        first.Id, first.User, first.Severity, first.Timestamp, first.Channel);
                }
            }
            catch (Exception ex1)
            {
                _logger.LogWarning("Failed to deserialize as DLPIncidentResponse: {Error}", ex1.Message);
                _logger.LogWarning("Deserialization error details: {StackTrace}", ex1.StackTrace);
                
                // If that fails, try to deserialize as array directly
                try
                {
                    incidents = JsonConvert.DeserializeObject<List<DLPIncident>>(responseContent) ?? new List<DLPIncident>();
                    _logger.LogInformation("Deserialized as List<DLPIncident>: {Count} incidents", incidents.Count);
                }
                catch (Exception ex2)
                {
                    _logger.LogError("Failed to deserialize response. DLPIncidentResponse error: {Error1}, List error: {Error2}", 
                        ex1.Message, ex2.Message);
                    _logger.LogError("Deserialization error details - Response: {Error1Details}, List: {Error2Details}", 
                        ex1.StackTrace, ex2.StackTrace);
                    _logger.LogWarning("Raw response (first 1000 chars): {Response}", 
                        responseContent.Length > 1000 ? responseContent.Substring(0, 1000) + "..." : responseContent);
                    incidents = new List<DLPIncident>();
                }
            }

            _logger.LogInformation("Fetched {Count} incidents from Forcepoint DLP API (Date range: {FromDate} to {ToDate})", 
                incidents.Count, fromDate, toDate);

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

            var messageId = await db.StreamAddAsync(streamName, fields);
            _logger.LogInformation("Incident pushed to Redis stream: MessageId={MessageId}, UserEmail={UserEmail}, Timestamp={Timestamp}", 
                messageId, incident.UserEmail, incident.Timestamp);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to push incident to Redis stream");
            throw;
        }
    }

    private HttpClient CreateHttpClient(DLPConfig config)
    {
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true
        };

        var scheme = config.UseHttps ? "https" : "http";
        var baseUrl = $"{scheme}://{config.ManagerIP}:{config.ManagerPort}";

        return new HttpClient(handler)
        {
            BaseAddress = new Uri(baseUrl),
            Timeout = TimeSpan.FromSeconds(config.Timeout <= 0 ? 30 : config.Timeout)
        };
    }

    private void OnConfigChanged(DLPConfig newConfig)
    {
        lock (_clientLock)
        {
            var oldClient = _httpClient;
            _currentConfig = newConfig;
            _httpClient = CreateHttpClient(newConfig);
            _accessToken = null;
            _tokenExpiry = DateTime.MinValue;
            oldClient?.Dispose();
            _logger.LogInformation("DLP Collector HTTP client reconfigured for {Manager}:{Port}", newConfig.ManagerIP, newConfig.ManagerPort);
        }
    }
    
    private static bool ConfigEquals(DLPConfig a, DLPConfig b)
    {
        if (a == null || b == null) return a == b;
        return a.ManagerIP == b.ManagerIP &&
               a.ManagerPort == b.ManagerPort &&
               a.Username == b.Username &&
               a.Password == b.Password &&
               a.UseHttps == b.UseHttps &&
               a.Timeout == b.Timeout;
    }

    public void Dispose()
    {
        _configProvider.ConfigChanged -= OnConfigChanged;
        _httpClient?.Dispose();
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
/// DLP Incident Source model (from API)
/// </summary>
public class DLPIncidentSource
{
    [JsonProperty("manager")]
    public string? Manager { get; set; }
    
    [JsonProperty("department")]
    public string? Department { get; set; }
    
    [JsonProperty("login_name")]
    public string? LoginName { get; set; }
    
    [JsonProperty("host_name")]
    public string? HostName { get; set; }
    
    [JsonProperty("business_unit")]
    public string? BusinessUnit { get; set; }
}

/// <summary>
/// DLP Incident model (from API)
/// </summary>
public class DLPIncident
{
    [JsonProperty("id")]
    public int Id { get; set; }
    
    [JsonProperty("severity")]
    public string? SeverityString { get; set; }
    
    [JsonIgnore]
    public int Severity
    {
        get
        {
            if (string.IsNullOrEmpty(SeverityString))
                return 0;
            
            return SeverityString.ToUpper() switch
            {
                "LOW" => 1,
                "MEDIUM" => 2,
                "HIGH" => 3,
                "CRITICAL" => 4,
                _ => 0
            };
        }
    }
    
    [JsonProperty("source")]
    public DLPIncidentSource? Source { get; set; }
    
    [JsonIgnore]
    public string User => Source?.LoginName ?? string.Empty;
    
    [JsonIgnore]
    public string? Department => Source?.Department;
    
    [JsonProperty("event_time")]
    public string? EventTimeString { get; set; }
    
    [JsonProperty("incident_time")]
    public string? IncidentTimeString { get; set; }
    
    [JsonIgnore]
    public DateTime Timestamp
    {
        get
        {
            // Try to parse incident_time first
            if (!string.IsNullOrEmpty(IncidentTimeString))
            {
                // Try multiple date formats
                var formats = new[] { 
                    "dd/MM/yyyy HH:mm:ss",
                    "MM/dd/yyyy HH:mm:ss",
                    "yyyy-MM-dd HH:mm:ss",
                    "dd-MM-yyyy HH:mm:ss"
                };
                
                foreach (var format in formats)
                {
                    if (DateTime.TryParseExact(IncidentTimeString, format, CultureInfo.InvariantCulture, 
                        DateTimeStyles.None, out var incidentTime))
                    {
                        return incidentTime;
                    }
                }
                
                // Fallback to standard parse
                if (DateTime.TryParse(IncidentTimeString, CultureInfo.InvariantCulture, 
                    DateTimeStyles.None, out var parsedTime))
                {
                    return parsedTime;
                }
            }
            
            // Try to parse event_time
            if (!string.IsNullOrEmpty(EventTimeString))
            {
                var formats = new[] { 
                    "dd/MM/yyyy HH:mm:ss",
                    "MM/dd/yyyy HH:mm:ss",
                    "yyyy-MM-dd HH:mm:ss",
                    "dd-MM-yyyy HH:mm:ss"
                };
                
                foreach (var format in formats)
                {
                    if (DateTime.TryParseExact(EventTimeString, format, CultureInfo.InvariantCulture, 
                        DateTimeStyles.None, out var eventTime))
                    {
                        return eventTime;
                    }
                }
                
                // Fallback to standard parse
                if (DateTime.TryParse(EventTimeString, CultureInfo.InvariantCulture, 
                    DateTimeStyles.None, out var parsedTime))
                {
                    return parsedTime;
                }
            }
            
            return DateTime.UtcNow;
        }
    }
    
    [JsonProperty("policies")]
    public string? Policy { get; set; }
    
    [JsonProperty("channel")]
    public string? Channel { get; set; }
    
    [JsonProperty("data_type")]
    public string? DataType { get; set; }
}

/// <summary>
/// DLP Incident response model
/// </summary>
public class DLPIncidentResponse
{
    public List<DLPIncident> Incidents { get; set; } = new();
    public int Total { get; set; }
}

