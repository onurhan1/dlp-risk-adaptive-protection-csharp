using DLP.RiskAnalyzer.Analyzer.Services;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace DLP.RiskAnalyzer.Analyzer.Controllers;

/// <summary>
/// DLP API Test Controller - Swagger üzerinden Forcepoint DLP API bağlantısını test etmek için
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class DLPTestController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<DLPTestController> _logger;
    private readonly HttpClient _httpClient;

    public DLPTestController(IConfiguration configuration, ILogger<DLPTestController> logger)
    {
        _configuration = configuration;
        _logger = logger;
        
        var dlpIp = _configuration["DLP:ManagerIP"] ?? "localhost";
        var dlpPort = _configuration.GetValue<int>("DLP:ManagerPort", 8443);
        var useHttps = _configuration.GetValue<bool>("DLP:UseHttps", true);
        var timeout = _configuration.GetValue<int>("DLP:Timeout", 30);
        
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
        };
        
        var baseUrl = useHttps 
            ? $"https://{dlpIp}:{dlpPort}"
            : $"http://{dlpIp}:{dlpPort}";
        
        // Log configuration for debugging
        _logger.LogInformation("DLP API Configuration - IP: {IP}, Port: {Port}, UseHttps: {UseHttps}, BaseUrl: {BaseUrl}", 
            dlpIp, dlpPort, useHttps, baseUrl);
            
        _httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri(baseUrl),
            Timeout = TimeSpan.FromSeconds(timeout)
        };
    }

    /// <summary>
    /// Test DLP API Authentication - Swagger'dan test edebilirsiniz
    /// POST /api/dlptest/auth
    /// </summary>
    [HttpPost("auth")]
    public async Task<ActionResult<Dictionary<string, object>>> TestAuthentication()
    {
        try
        {
            var username = _configuration["DLP:Username"] ?? "";
            var password = _configuration["DLP:Password"] ?? "";
            var dlpIp = _configuration["DLP:ManagerIP"] ?? "localhost";
            var dlpPort = _configuration.GetValue<int>("DLP:ManagerPort", 8443);

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "DLP Username or Password not configured in appsettings.json",
                    config = new
                    {
                        managerIP = dlpIp,
                        managerPort = dlpPort,
                        usernameConfigured = !string.IsNullOrEmpty(username),
                        passwordConfigured = !string.IsNullOrEmpty(password)
                    }
                });
            }

            // Forcepoint DLP REST API v1 Authentication endpoint
            // Note: Forcepoint DLP API expects application/x-www-form-urlencoded format, not JSON
            var formData = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("username", username),
                new KeyValuePair<string, string>("password", password)
            };
            var content = new FormUrlEncodedContent(formData);

            // Debug: Log the exact request being sent (before sending)
            var requestBody = $"username={Uri.EscapeDataString(username)}&password={Uri.EscapeDataString(password)}";
            var actualBaseUrl = _httpClient.BaseAddress?.ToString();
            var actualUseHttps = actualBaseUrl?.StartsWith("https://") == true;
            
            _logger.LogInformation("Testing DLP API authentication to {BaseAddress}", _httpClient.BaseAddress);
            _logger.LogInformation("Actual Base URL: {BaseUrl}, Is HTTPS: {IsHttps}", actualBaseUrl, actualUseHttps);
            _logger.LogDebug("Request URL: {BaseAddress}/dlp/rest/v1/auth/access-token", _httpClient.BaseAddress);
            _logger.LogDebug("Request Body (form-urlencoded): {RequestBody}", requestBody);
            _logger.LogDebug("Content-Type: {ContentType}", content.Headers.ContentType?.ToString());
            _logger.LogDebug("Username: {Username}", username);

            // Log all request headers for comparison with Postman
            _logger.LogDebug("Request Headers:");
            foreach (var header in _httpClient.DefaultRequestHeaders)
            {
                _logger.LogDebug("  {Key}: {Value}", header.Key, string.Join(", ", header.Value));
            }
            _logger.LogDebug("Content Headers:");
            foreach (var header in content.Headers)
            {
                _logger.LogDebug("  {Key}: {Value}", header.Key, string.Join(", ", header.Value));
            }
            
            var response = await _httpClient.PostAsync("/dlp/rest/v1/auth/access-token", content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("DLP API authentication failed. Status: {Status}, Response: {Response}",
                    response.StatusCode, errorContent);
                _logger.LogError("Request URL was: {BaseAddress}/dlp/rest/v1/auth/access-token", _httpClient.BaseAddress);
                _logger.LogError("Request Body was: {RequestBody}", requestBody);

                // Extract error message from HTML if possible
                var errorMessage = errorContent;
                if (errorContent.Contains("<html>") || errorContent.Contains("<!DOCTYPE"))
                {
                    // Try to extract meaningful error from HTML
                    var titleMatch = System.Text.RegularExpressions.Regex.Match(errorContent, @"<title>(.*?)</title>", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (titleMatch.Success)
                    {
                        errorMessage = titleMatch.Groups[1].Value;
                    }
                    else
                    {
                        errorMessage = "DLP Manager returned HTML error page (check credentials and permissions)";
                    }
                }

                return StatusCode((int)response.StatusCode, new
                {
                    success = false,
                    message = "DLP API authentication failed",
                    statusCode = (int)response.StatusCode,
                    statusText = response.StatusCode.ToString(),
                    error = errorMessage,
                    errorRaw = errorContent.Length > 500 ? errorContent.Substring(0, 500) + "..." : errorContent,
                    debug = new
                    {
                        requestUrl = $"{actualBaseUrl}/dlp/rest/v1/auth/access-token",
                        requestBody = requestBody,
                        contentType = content.Headers.ContentType?.ToString(),
                        responseHeaders = response.Headers.ToDictionary(h => h.Key, h => string.Join(", ", h.Value)),
                        actualBaseUrl = actualBaseUrl,
                        httpClientBaseAddress = _httpClient.BaseAddress?.ToString(),
                        useHttps = actualUseHttps,
                        configUseHttps = _configuration.GetValue<bool>("DLP:UseHttps", true)
                    },
                    config = new
                    {
                        baseUrl = _httpClient.BaseAddress?.ToString(),
                        managerIP = dlpIp,
                        managerPort = dlpPort,
                        useHttps = _configuration.GetValue<bool>("DLP:UseHttps", true),
                        username = username,
                        troubleshooting = new
                        {
                            check1 = "Verify username and password are correct (test in Postman first)",
                            check2 = "Verify user is Application Administrator type in Forcepoint DLP Manager (not regular Administrator)",
                            check3 = "Verify user has API access permissions enabled in Forcepoint DLP Manager",
                            check4 = "Verify UseHttps is set to true in appsettings.json (port 9443 requires HTTPS)",
                            check5 = "Check if IP whitelist restrictions exist in DLP Manager (your IP: check network settings)",
                            check6 = "Compare request format with Postman - ensure form-urlencoded format matches exactly",
                            check7 = "Check Forcepoint DLP Manager logs for detailed authentication failure reason",
                            note = "If Postman works but code doesn't, compare exact request headers and body format"
                        }
                    }
                });
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var tokenResponse = JsonSerializer.Deserialize<Dictionary<string, object>>(responseContent);

            var accessToken = tokenResponse?.ContainsKey("accessToken") == true
                ? tokenResponse["accessToken"].ToString()
                : tokenResponse?.ContainsKey("token") == true
                    ? tokenResponse["token"].ToString()
                    : null;

            if (string.IsNullOrEmpty(accessToken))
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "No access token received from DLP API",
                    response = responseContent
                });
            }

            // Mask token for security (show only first 20 chars)
            var maskedToken = accessToken.Length > 20
                ? accessToken.Substring(0, 20) + "..."
                : "***";

            return Ok(new
            {
                success = true,
                message = "DLP API authentication successful",
                token = maskedToken,
                tokenLength = accessToken.Length,
                config = new
                {
                    baseUrl = _httpClient.BaseAddress?.ToString(),
                    managerIP = dlpIp,
                    managerPort = dlpPort,
                    username = username
                },
                rawResponse = tokenResponse
            });
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "DLP API connection timeout");
            return StatusCode(408, new
            {
                success = false,
                message = "DLP API connection timeout - Check if DLP Manager is accessible",
                error = ex.Message,
                config = new
                {
                    baseUrl = _httpClient.BaseAddress?.ToString(),
                    timeout = _httpClient.Timeout.TotalSeconds
                }
            });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "DLP API connection error");
            return StatusCode(503, new
            {
                success = false,
                message = "DLP API connection error - Check network connectivity and firewall",
                error = ex.Message,
                config = new
                {
                    baseUrl = _httpClient.BaseAddress?.ToString()
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during DLP API authentication test");
            return StatusCode(500, new
            {
                success = false,
                message = "Unexpected error during DLP API authentication test",
                error = ex.Message,
                stackTrace = ex.StackTrace
            });
        }
    }

    /// <summary>
    /// Test DLP API Connection (without authentication) - Swagger'dan test edebilirsiniz
    /// GET /api/dlptest/connection
    /// </summary>
    [HttpGet("connection")]
    public async Task<ActionResult<Dictionary<string, object>>> TestConnection()
    {
        try
        {
            var dlpIp = _configuration["DLP:ManagerIP"] ?? "localhost";
            var dlpPort = _configuration.GetValue<int>("DLP:ManagerPort", 8443);
            var useHttps = _configuration.GetValue<bool>("DLP:UseHttps", true);
            var baseUrl = useHttps
                ? $"https://{dlpIp}:{dlpPort}"
                : $"http://{dlpIp}:{dlpPort}";

            _logger.LogInformation("Testing DLP API connection to {BaseUrl}", baseUrl);

            // Try to connect to a simple endpoint (health check if available, or just test connection)
            var response = await _httpClient.GetAsync("/");

            return Ok(new
            {
                success = true,
                message = "DLP API connection successful",
                statusCode = (int)response.StatusCode,
                statusText = response.StatusCode.ToString(),
                config = new
                {
                    baseUrl = baseUrl,
                    managerIP = dlpIp,
                    managerPort = dlpPort,
                    useHttps = useHttps
                }
            });
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "DLP API connection timeout");
            return StatusCode(408, new
            {
                success = false,
                message = "DLP API connection timeout",
                error = ex.Message,
                config = new
                {
                    baseUrl = _httpClient.BaseAddress?.ToString()
                }
            });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "DLP API connection error");
            return StatusCode(503, new
            {
                success = false,
                message = "DLP API connection error - Check network connectivity and firewall",
                error = ex.Message,
                config = new
                {
                    baseUrl = _httpClient.BaseAddress?.ToString()
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during DLP API connection test");
            return StatusCode(500, new
            {
                success = false,
                message = "Unexpected error during DLP API connection test",
                error = ex.Message
            });
        }
    }

    /// <summary>
    /// Test DLP API - Fetch Incidents (requires authentication) - Swagger'dan test edebilirsiniz
    /// GET /api/dlptest/incidents?hours=24
    /// </summary>
    [HttpGet("incidents")]
    public async Task<ActionResult<Dictionary<string, object>>> TestFetchIncidents([FromQuery] int hours = 24)
    {
        try
        {
            // Step 1: Authenticate
            var username = _configuration["DLP:Username"] ?? "";
            var password = _configuration["DLP:Password"] ?? "";

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "DLP Username or Password not configured"
                });
            }

            // Forcepoint DLP API expects application/x-www-form-urlencoded format
            var formData = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("username", username),
                new KeyValuePair<string, string>("password", password)
            };
            var authContent = new FormUrlEncodedContent(formData);

            var authResponse = await _httpClient.PostAsync("/dlp/rest/v1/auth/access-token", authContent);
            
            if (!authResponse.IsSuccessStatusCode)
            {
                var errorContent = await authResponse.Content.ReadAsStringAsync();
                return StatusCode((int)authResponse.StatusCode, new
                {
                    success = false,
                    message = "Authentication failed",
                    error = errorContent
                });
            }

            var authResponseContent = await authResponse.Content.ReadAsStringAsync();
            var tokenResponse = JsonSerializer.Deserialize<Dictionary<string, object>>(authResponseContent);

            var accessToken = tokenResponse?.ContainsKey("accessToken") == true
                ? tokenResponse["accessToken"].ToString()
                : tokenResponse?.ContainsKey("token") == true
                    ? tokenResponse["token"].ToString()
                    : null;

            if (string.IsNullOrEmpty(accessToken))
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "No access token received"
                });
            }

            // Step 2: Fetch incidents
            var endTime = DateTime.UtcNow;
            var startTime = endTime.AddHours(-hours);

            var incidentsUrl = $"/dlp/rest/v1/incidents?" +
                             $"startTime={Uri.EscapeDataString(startTime.ToString("yyyy-MM-ddTHH:mm:ssZ"))}&" +
                             $"endTime={Uri.EscapeDataString(endTime.ToString("yyyy-MM-ddTHH:mm:ssZ"))}&" +
                             $"page=1&pageSize=10";

            var request = new HttpRequestMessage(HttpMethod.Get, incidentsUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var incidentsResponse = await _httpClient.SendAsync(request);

            if (!incidentsResponse.IsSuccessStatusCode)
            {
                var errorContent = await incidentsResponse.Content.ReadAsStringAsync();
                return StatusCode((int)incidentsResponse.StatusCode, new
                {
                    success = false,
                    message = "Failed to fetch incidents",
                    statusCode = (int)incidentsResponse.StatusCode,
                    error = errorContent
                });
            }

            var incidentsContent = await incidentsResponse.Content.ReadAsStringAsync();
            var incidentsData = JsonSerializer.Deserialize<Dictionary<string, object>>(incidentsContent);

            return Ok(new
            {
                success = true,
                message = "Incidents fetched successfully",
                timeRange = new
                {
                    startTime = startTime.ToString("O"),
                    endTime = endTime.ToString("O"),
                    hours = hours
                },
                incidents = incidentsData,
                config = new
                {
                    baseUrl = _httpClient.BaseAddress?.ToString()
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing DLP API incidents fetch");
            return StatusCode(500, new
            {
                success = false,
                message = "Error testing DLP API incidents fetch",
                error = ex.Message
            });
        }
    }

    /// <summary>
    /// Get DLP Configuration (for debugging) - Swagger'dan test edebilirsiniz
    /// GET /api/dlptest/config
    /// </summary>
    [HttpGet("config")]
    public ActionResult<Dictionary<string, object>> GetConfig()
    {
        var dlpIp = _configuration["DLP:ManagerIP"] ?? "localhost";
        var dlpPort = _configuration.GetValue<int>("DLP:ManagerPort", 8443);
        var username = _configuration["DLP:Username"] ?? "";
        var password = _configuration["DLP:Password"] ?? "";
        var useHttps = _configuration.GetValue<bool>("DLP:UseHttps", true);
        var timeout = _configuration.GetValue<int>("DLP:Timeout", 30);

        var baseUrl = useHttps
            ? $"https://{dlpIp}:{dlpPort}"
            : $"http://{dlpIp}:{dlpPort}";

        return Ok(new
        {
            config = new
            {
                managerIP = dlpIp,
                managerPort = dlpPort,
                useHttps = useHttps,
                timeout = timeout,
                baseUrl = baseUrl,
                usernameConfigured = !string.IsNullOrEmpty(username),
                passwordConfigured = !string.IsNullOrEmpty(password),
                username = username.Length > 0 ? username.Substring(0, Math.Min(3, username.Length)) + "***" : "not configured",
                password = password.Length > 0 ? "***" : "not configured"
            },
            note = "This endpoint shows configuration without exposing sensitive data"
        });
    }
}

