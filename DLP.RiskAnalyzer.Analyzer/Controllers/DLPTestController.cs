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
/// SECURITY: This controller is only available in Development environment
/// </summary>
#if DEBUG
[ApiController]
[Route("api/[controller]")]
#endif
public class DLPTestController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<DLPTestController> _logger;
    private readonly DlpConfigurationService _dlpConfigService;

    public DLPTestController(
        IConfiguration configuration, 
        ILogger<DLPTestController> logger,
        DlpConfigurationService dlpConfigService)
    {
        _configuration = configuration;
        _logger = logger;
        _dlpConfigService = dlpConfigService;
    }

    /// <summary>
    /// Create HttpClient dynamically from database configuration
    /// </summary>
    private async Task<HttpClient> CreateHttpClientAsync()
    {
        try
        {
            // Get DLP settings from database (with sensitive data)
            var config = await _dlpConfigService.GetSensitiveConfigAsync();
            
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
            };
            
            var baseUrl = config.UseHttps 
                ? $"https://{config.ManagerIp}:{config.ManagerPort}"
                : $"http://{config.ManagerIp}:{config.ManagerPort}";
            
            _logger.LogInformation("DLP API Configuration (from DB) - IP: {IP}, Port: {Port}, UseHttps: {UseHttps}, BaseUrl: {BaseUrl}", 
                config.ManagerIp, config.ManagerPort, config.UseHttps, baseUrl);
                
            return new HttpClient(handler)
            {
                BaseAddress = new Uri(baseUrl),
                Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds)
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load DLP config from database, falling back to appsettings.json");
            
            // Fallback to appsettings.json if database config not available
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
            
            return new HttpClient(handler)
            {
                BaseAddress = new Uri(baseUrl),
                Timeout = TimeSpan.FromSeconds(timeout)
            };
        }
    }

    /// <summary>
    /// Test DLP API Authentication - Swagger'dan test edebilirsiniz
    /// POST /api/dlptest/auth
    /// </summary>
    [HttpPost("auth")]
    public async Task<ActionResult<Dictionary<string, object>>> TestAuthentication()
    {
        HttpClient? httpClient = null;
        try
        {
            // Get DLP settings from database
            var config = await _dlpConfigService.GetSensitiveConfigAsync();
            httpClient = await CreateHttpClientAsync();

            if (string.IsNullOrEmpty(config.Username) || string.IsNullOrEmpty(config.Password))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "DLP Username or Password not configured. Please configure DLP settings via UI (Settings → DLP API Configuration) or appsettings.json",
                    config = new
                    {
                        managerIP = config.ManagerIp,
                        managerPort = config.ManagerPort,
                        usernameConfigured = !string.IsNullOrEmpty(config.Username),
                        passwordConfigured = !string.IsNullOrEmpty(config.Password),
                        source = "database"
                    }
                });
            }

            // Forcepoint DLP REST API v1 Authentication endpoint
            // Note: Some DLP versions (8.9-9.0) expect username/password in headers, not body
            // Postman works with headers, so we'll use header-based authentication
            
            var actualBaseUrl = httpClient.BaseAddress?.ToString();
            var actualUseHttps = actualBaseUrl?.StartsWith("https://") == true;
            
            _logger.LogInformation("Testing DLP API authentication to {BaseAddress}", httpClient.BaseAddress);
            _logger.LogInformation("Actual Base URL: {BaseUrl}, Is HTTPS: {IsHttps}", actualBaseUrl, actualUseHttps);
            _logger.LogInformation("Using header-based authentication (username/password in headers)");
            
            // Create request with username/password in headers (matching Postman format)
            var request = new HttpRequestMessage(HttpMethod.Post, "/dlp/rest/v1/auth/access-token");
            request.Headers.Add("username", config.Username);
            request.Headers.Add("password", config.Password);
            
            // Log request details
            _logger.LogDebug("Request URL: {BaseAddress}/dlp/rest/v1/auth/access-token", httpClient.BaseAddress);
            _logger.LogDebug("Request Method: POST");
            _logger.LogDebug("Username header: {Username}", config.Username);
            _logger.LogDebug("Password header: [REDACTED]");
            
            // Log all request headers
            _logger.LogDebug("Request Headers:");
            foreach (var header in request.Headers)
            {
                if (header.Key.Equals("password", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogDebug("  {Key}: [REDACTED]", header.Key);
                }
                else
                {
                    _logger.LogDebug("  {Key}: {Value}", header.Key, string.Join(", ", header.Value));
                }
            }
            
            var response = await httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("DLP API authentication failed. Status: {Status}, Response: {Response}",
                    response.StatusCode, errorContent);
                _logger.LogError("Request URL was: {BaseAddress}/dlp/rest/v1/auth/access-token", httpClient.BaseAddress);
                _logger.LogError("Authentication method: Header-based (username/password in headers)");

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
                        authenticationMethod = "Header-based (username/password in headers)",
                        requestHeaders = request.Headers.ToDictionary(h => h.Key, h => 
                            h.Key.Equals("password", StringComparison.OrdinalIgnoreCase) 
                                ? "[REDACTED]" 
                                : string.Join(", ", h.Value)),
                        responseHeaders = response.Headers.ToDictionary(h => h.Key, h => string.Join(", ", h.Value)),
                        actualBaseUrl = actualBaseUrl,
                        httpClientBaseAddress = httpClient.BaseAddress?.ToString(),
                        useHttps = actualUseHttps,
                        configUseHttps = config.UseHttps
                    },
                    config = new
                    {
                        baseUrl = httpClient.BaseAddress?.ToString(),
                        managerIP = config.ManagerIp,
                        managerPort = config.ManagerPort,
                        useHttps = config.UseHttps,
                        username = config.Username,
                        source = "database"
                    },
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
                });
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var tokenResponse = JsonSerializer.Deserialize<Dictionary<string, object>>(responseContent);

            // Forcepoint DLP API returns access_token (snake_case), but some versions use accessToken (camelCase)
            var accessToken = tokenResponse?.ContainsKey("access_token") == true
                ? tokenResponse["access_token"].ToString()
                : tokenResponse?.ContainsKey("accessToken") == true
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
                    baseUrl = httpClient.BaseAddress?.ToString(),
                    managerIP = config.ManagerIp,
                    managerPort = config.ManagerPort,
                    username = config.Username,
                    source = "database"
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
                    baseUrl = httpClient?.BaseAddress?.ToString(),
                    timeout = httpClient?.Timeout.TotalSeconds
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
                    baseUrl = httpClient?.BaseAddress?.ToString()
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
        finally
        {
            httpClient?.Dispose();
        }
    }

    /// <summary>
    /// Test DLP API Connection (without authentication) - Swagger'dan test edebilirsiniz
    /// GET /api/dlptest/connection
    /// </summary>
    [HttpGet("connection")]
    public async Task<ActionResult<Dictionary<string, object>>> TestConnection()
    {
        HttpClient? httpClient = null;
        try
        {
            var config = await _dlpConfigService.GetAsync();
            httpClient = await CreateHttpClientAsync();
            
            var baseUrl = config.UseHttps
                ? $"https://{config.ManagerIp}:{config.ManagerPort}"
                : $"http://{config.ManagerIp}:{config.ManagerPort}";

            _logger.LogInformation("Testing DLP API connection to {BaseUrl}", baseUrl);

            // Try to connect to a simple endpoint (health check if available, or just test connection)
            var response = await httpClient.GetAsync("/");

            return Ok(new
            {
                success = true,
                message = "DLP API connection successful",
                statusCode = (int)response.StatusCode,
                statusText = response.StatusCode.ToString(),
                config = new
                {
                    baseUrl = baseUrl,
                    managerIP = config.ManagerIp,
                    managerPort = config.ManagerPort,
                    useHttps = config.UseHttps,
                    source = "database"
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
                    baseUrl = httpClient?.BaseAddress?.ToString()
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
                    baseUrl = httpClient?.BaseAddress?.ToString()
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
        finally
        {
            httpClient?.Dispose();
        }
    }

    /// <summary>
    /// Test DLP API - Fetch Incidents (requires authentication) - Swagger'dan test edebilirsiniz
    /// POST /api/dlptest/incidents?hours=24
    /// </summary>
    [HttpPost("incidents")]
    public async Task<ActionResult<Dictionary<string, object>>> TestFetchIncidents([FromQuery] int hours = 24)
    {
        HttpClient? httpClient = null;
        try
        {
            // Get DLP settings from database
            var config = await _dlpConfigService.GetSensitiveConfigAsync();
            httpClient = await CreateHttpClientAsync();

            if (string.IsNullOrEmpty(config.Username) || string.IsNullOrEmpty(config.Password))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "DLP Username or Password not configured. Please configure DLP settings via UI (Settings → DLP API Configuration) or appsettings.json"
                });
            }

            // Use header-based authentication (matching Postman format)
            var authRequest = new HttpRequestMessage(HttpMethod.Post, "/dlp/rest/v1/auth/access-token");
            authRequest.Headers.Add("username", config.Username);
            authRequest.Headers.Add("password", config.Password);

            var authResponse = await httpClient.SendAsync(authRequest);
            
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

            // Forcepoint DLP API returns access_token (snake_case), but some versions use accessToken (camelCase)
            var accessToken = tokenResponse?.ContainsKey("access_token") == true
                ? tokenResponse["access_token"].ToString()
                : tokenResponse?.ContainsKey("accessToken") == true
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

            // Step 2: Fetch incidents using POST method with body
            // According to Forcepoint DLP API documentation:
            // POST /dlp/rest/v1/incidents/
            // Body: { "type": "INCIDENTS", "from_date": "dd/MM/yyyy HH:mm:ss", "to_date": "dd/MM/yyyy HH:mm:ss" }
            var endTime = DateTime.UtcNow;
            var startTime = endTime.AddHours(-hours);
            
            // Format dates as "dd/MM/yyyy HH:mm:ss" (Forcepoint DLP API format)
            var fromDate = startTime.ToUniversalTime().ToString("dd/MM/yyyy HH:mm:ss");
            var toDate = endTime.ToUniversalTime().ToString("dd/MM/yyyy HH:mm:ss");

            // Note: Some DLP versions require trailing slash, others don't
            var incidentsUrl = "/dlp/rest/v1/incidents/";
            var requestBody = new
            {
                type = "INCIDENTS",
                from_date = fromDate,
                to_date = toDate
            };

            var jsonBody = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, incidentsUrl);
            // Authorization: Bearer token (required)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Content = content;

            _logger.LogDebug("Fetching incidents using POST method with body: {Body}", jsonBody);
            
            HttpResponseMessage incidentsResponse;
            try
            {
                incidentsResponse = await httpClient.SendAsync(request);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching incidents");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error fetching incidents",
                    error = ex.Message
                });
            }

            if (!incidentsResponse.IsSuccessStatusCode)
            {
                var errorContent = await incidentsResponse.Content.ReadAsStringAsync();
                _logger.LogError("Failed to fetch incidents. Method: POST, URL: {Url}, Status: {Status}, Response: {Response}",
                    incidentsUrl, incidentsResponse.StatusCode, errorContent);
                
                return StatusCode((int)incidentsResponse.StatusCode, new
                {
                    success = false,
                    message = "Failed to fetch incidents",
                    statusCode = (int)incidentsResponse.StatusCode,
                    statusText = incidentsResponse.StatusCode.ToString(),
                    method = "POST",
                    url = incidentsUrl,
                    requestBody = requestBody,
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
                    fromDate = fromDate,
                    toDate = toDate,
                    startTime = startTime.ToString("O"),
                    endTime = endTime.ToString("O"),
                    hours = hours
                },
                incidents = incidentsData,
                requestBody = requestBody,
                config = new
                {
                    baseUrl = httpClient?.BaseAddress?.ToString(),
                    source = "database"
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
        finally
        {
            httpClient?.Dispose();
        }
    }

    /// <summary>
    /// Get DLP Configuration (for debugging) - Swagger'dan test edebilirsiniz
    /// GET /api/dlptest/config
    /// </summary>
    [HttpGet("config")]
    public async Task<ActionResult<Dictionary<string, object>>> GetConfig()
    {
        try
        {
            // Try to get config from database first
            var config = await _dlpConfigService.GetAsync();
            var baseUrl = config.UseHttps
                ? $"https://{config.ManagerIp}:{config.ManagerPort}"
                : $"http://{config.ManagerIp}:{config.ManagerPort}";

            return Ok(new
            {
                config = new
                {
                    managerIP = config.ManagerIp,
                    managerPort = config.ManagerPort,
                    useHttps = config.UseHttps,
                    timeout = config.TimeoutSeconds,
                    baseUrl = baseUrl,
                    usernameConfigured = !string.IsNullOrEmpty(config.Username),
                    passwordConfigured = config.PasswordSet,
                    username = config.Username.Length > 0 ? config.Username.Substring(0, Math.Min(3, config.Username.Length)) + "***" : "not configured",
                    password = config.PasswordSet ? "***" : "not configured",
                    updatedAt = config.UpdatedAt,
                    source = "database"
                },
                note = "This endpoint shows configuration from database (UI settings). If not configured, falls back to appsettings.json."
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load config from database, falling back to appsettings.json");
            
            // Fallback to appsettings.json
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
                    password = password.Length > 0 ? "***" : "not configured",
                    source = "appsettings.json"
                },
                note = "This endpoint shows configuration from appsettings.json (fallback). Configure via UI (Settings → DLP API Configuration) to use database settings.",
                warning = "Database configuration not available. Using appsettings.json as fallback."
            });
        }
    }
}

