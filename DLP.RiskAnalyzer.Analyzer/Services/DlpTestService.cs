using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DLP.RiskAnalyzer.Analyzer.Services;

public class DlpTestService : IDlpTestService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<DlpTestService> _logger;
    private readonly IDlpConfigurationService _dlpConfigService;

    public DlpTestService(
        IConfiguration configuration,
        ILogger<DlpTestService> logger,
        IDlpConfigurationService dlpConfigService)
    {
        _configuration = configuration;
        _logger = logger;
        _dlpConfigService = dlpConfigService;
    }

    private Task<HttpClient> CreateHttpClientAsync(DLP.RiskAnalyzer.Analyzer.Models.DlpApiSensitiveSettingsResponse config)
    {
        try
        {
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
            };
            
            var baseUrl = config.UseHttps 
                ? $"https://{config.ManagerIp}:{config.ManagerPort}/"
                : $"http://{config.ManagerIp}:{config.ManagerPort}/";
                
            return Task.FromResult(new HttpClient(handler)
            {
                BaseAddress = new Uri(baseUrl),
                Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds)
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load DLP config, falling back to appsettings");
            var dlpIp = _configuration["DLP:ManagerIP"] ?? "localhost";
            var dlpPort = _configuration.GetValue<int>("DLP:ManagerPort", 8443);
            var useHttps = _configuration.GetValue<bool>("DLP:UseHttps", true);
            var timeout = _configuration.GetValue<int>("DLP:Timeout", 30);
            var handler = new HttpClientHandler { ServerCertificateCustomValidationCallback = (m, c, ch, e) => true };
            var baseUrl = useHttps ? $"https://{dlpIp}:{dlpPort}/" : $"http://{dlpIp}:{dlpPort}/";
            return Task.FromResult(new HttpClient(handler)
            {
                BaseAddress = new Uri(baseUrl),
                Timeout = TimeSpan.FromSeconds(timeout)
            });
        }
    }

    private async Task<DlpTestResult> GetTokenAsync(HttpClient client, string username, string password)
    {
        var authRequest = new HttpRequestMessage(HttpMethod.Post, "/dlp/rest/v1/auth/access-token");
        authRequest.Headers.Add("username", username);
        authRequest.Headers.Add("password", password);

        var authResponse = await client.SendAsync(authRequest);
        if (!authResponse.IsSuccessStatusCode)
        {
            var errorContent = await authResponse.Content.ReadAsStringAsync();
            return new DlpTestResult((int)authResponse.StatusCode, new { success = false, message = "Authentication failed", error = errorContent });
        }

        var authContent = await authResponse.Content.ReadAsStringAsync();
        var tokenObj = JsonSerializer.Deserialize<Dictionary<string, object>>(authContent);
        
        var accessToken = tokenObj?.ContainsKey("access_token") == true ? tokenObj["access_token"].ToString()
                        : tokenObj?.ContainsKey("accessToken") == true ? tokenObj["accessToken"].ToString()
                        : tokenObj?.ContainsKey("token") == true ? tokenObj["token"].ToString() : null;

        if (string.IsNullOrEmpty(accessToken))
        {
             return new DlpTestResult(500, new { success = false, message = "No access token received" });
        }
        return new DlpTestResult(200, accessToken);
    }

    private async Task<DlpTestResult> ExecuteWithAuthAsync(
        string endpoint, 
        HttpMethod method, 
        object? requestBody = null, 
        Func<string, object>? successParser = null,
        Func<HttpResponseMessage, string, object>? errorBuilder = null,
        bool includeGetBody = true)
    {
        HttpClient? httpClient = null;
        try
        {
            var config = await _dlpConfigService.GetSensitiveConfigAsync();
            if (string.IsNullOrEmpty(config.Username) || string.IsNullOrEmpty(config.Password))
                return new DlpTestResult(400, new { success = false, message = "DLP Username or Password not configured." });

            httpClient = await CreateHttpClientAsync(config);
            var tokenResult = await GetTokenAsync(httpClient, config.Username, config.Password);
            if (tokenResult.StatusCode != 200) return tokenResult;
            var token = tokenResult.Content as string;

            var requestUri = endpoint.StartsWith("//dlp/", StringComparison.OrdinalIgnoreCase)
                ? new Uri($"{httpClient.BaseAddress!.GetLeftPart(UriPartial.Authority)}{endpoint}")
                : new Uri(endpoint, UriKind.Relative);

            var request = new HttpRequestMessage(method, requestUri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            
            if (method == HttpMethod.Get && includeGetBody)
            {
                request.Headers.TryAddWithoutValidation("Content-Type", "application/json");
                request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
                request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json"); 
            }
            else if (requestBody != null)
            {
                request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            }

            var response = await httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                var errorResult = errorBuilder != null 
                    ? errorBuilder(response, responseContent) 
                    : new { success = false, message = "DLP request failed", statusCode = (int)response.StatusCode, requestUri = request.RequestUri?.ToString(), error = responseContent };
                return new DlpTestResult((int)response.StatusCode, errorResult);
            }

            object parsedContent = responseContent;
            try { parsedContent = JsonSerializer.Deserialize<Dictionary<string, object>>(responseContent) ?? (object)responseContent; } catch { }

            var finalSuccess = successParser != null ? successParser(responseContent) : new
            {
                success = true,
                message = "Request successful",
                data = parsedContent,
                rawResponse = responseContent,
                requestUri = request.RequestUri?.ToString(),
                config = new { baseUrl = httpClient.BaseAddress?.ToString(), source = "database" }
            };

            return new DlpTestResult(200, finalSuccess);
        }
        catch (TaskCanceledException ex) { return new DlpTestResult(408, new { success = false, message = "DLP API Timeout", error = ex.Message }); }
        catch (Exception ex) { return new DlpTestResult(500, new { success = false, message = "Error executing DLP Request", error = ex.Message }); }
        finally { httpClient?.Dispose(); }
    }

    public async Task<DlpTestResult> TestAuthenticationAsync()
    {
        HttpClient? httpClient = null;
        try
        {
            var config = await _dlpConfigService.GetSensitiveConfigAsync();
            if (string.IsNullOrEmpty(config.Username) || string.IsNullOrEmpty(config.Password))
                return new DlpTestResult(400, new { success = false, message = "DLP Username or Password not configured." });

            httpClient = await CreateHttpClientAsync(config);
            var request = new HttpRequestMessage(HttpMethod.Post, "/dlp/rest/v1/auth/access-token");
            request.Headers.Add("username", config.Username);
            request.Headers.Add("password", config.Password);

            var response = await httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return new DlpTestResult((int)response.StatusCode, new { success = false, message = "Authentication failed", error = responseContent });

            var parsed = JsonSerializer.Deserialize<Dictionary<string, object>>(responseContent);
            return new DlpTestResult(200, new { success = true, message = "DLP API authentication successful", rawResponse = parsed });
        }
        catch (Exception ex) { return new DlpTestResult(500, new { success = false, error = ex.Message }); }
        finally { httpClient?.Dispose(); }
    }

    public async Task<DlpTestResult> TestConnectionAsync()
    {
        HttpClient? httpClient = null;
        try
        {
            var config = await _dlpConfigService.GetSensitiveConfigAsync();
            httpClient = await CreateHttpClientAsync(config);
            var response = await httpClient.GetAsync("/");
            return new DlpTestResult((int)response.StatusCode, new { success = true, message = "Connection successful", statusCode = (int)response.StatusCode });
        }
        catch (Exception ex) { return new DlpTestResult(500, new { success = false, error = ex.Message }); }
        finally { httpClient?.Dispose(); }
    }

    public async Task<DlpTestResult> TestFetchIncidentsAsync(int hours = 24)
    {
        var endTime = DateTime.UtcNow;
        var startTime = endTime.AddHours(-hours);
        var fromDate = startTime.ToString("dd/MM/yyyy HH:mm:ss");
        var toDate = endTime.ToString("dd/MM/yyyy HH:mm:ss");

        var body = new { type = "INCIDENTS", from_date = fromDate, to_date = toDate };
        return await ExecuteWithAuthAsync("/dlp/rest/v1/incidents/", HttpMethod.Post, body);
    }

    public async Task<DlpTestResult> GetPolicyRulesAsync(string policyName)
    {
        return await ExecuteWithAuthAsync($"/dlp/rest/v1/policy/rules?policyName={Uri.EscapeDataString(policyName ?? "")}", HttpMethod.Get);
    }

    public async Task<DlpTestResult> GetEnabledPolicyNamesAsync(string type)
    {
        return await ExecuteWithAuthAsync($"/dlp/rest/v1/policy/enabled-names?type={Uri.EscapeDataString(type ?? "")}", HttpMethod.Get);
    }

    public async Task<DlpTestResult> GetAllPolicyRulesExceptionsAsync(string type)
    {
        return await ExecuteWithAuthAsync(
            $"/dlp/rest/v1/policy/rules/exceptions/all?type={Uri.EscapeDataString(type ?? "")}", 
            HttpMethod.Get, null,
            successParser: responseContent =>
            {
                var availableRuleNames = new List<string>();
                var rulesList = new List<object>();
                try
                {
                    var doc = JsonDocument.Parse(responseContent);
                    if (doc.RootElement.TryGetProperty("exception_rules", out var exceptionRules) && exceptionRules.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var rule in exceptionRules.EnumerateArray())
                        {
                            if (rule.TryGetProperty("rule_name", out var rn)) availableRuleNames.Add(rn.GetString() ?? "");
                            rulesList.Add(new
                            {
                                policy_name = rule.TryGetProperty("policy_name", out var pn) ? pn.GetString() : "",
                                rule_name = rn.GetString(),
                                exception_count = rule.TryGetProperty("exception_rule_names", out var ern) && ern.ValueKind == JsonValueKind.Array ? ern.GetArrayLength() : 0
                            });
                        }
                    }
                }
                catch { }

                return new { success = true, message = "Exceptions fetched", availableRuleNames = availableRuleNames.Distinct().OrderBy(x=>x).ToList(), rules = rulesList, data = JsonSerializer.Deserialize<object>(responseContent) };
            });
    }

    public async Task<DlpTestResult> GetPolicyRulesExceptionsAsync(string type, string ruleName)
    {
        return await ExecuteWithAuthAsync(
            $"//dlp/rest/v1/policy/rules/exceptions?type={Uri.EscapeDataString(type ?? "DLP")}&ruleName={Uri.EscapeDataString(ruleName ?? "")}",
            HttpMethod.Get,
            includeGetBody: false);
    }

    public async Task<DlpTestResult> DebugPolicyRulesExceptionsAsync(string type, string ruleName)
    {
        HttpClient? httpClient = null;
        try
        {
            var config = await _dlpConfigService.GetSensitiveConfigAsync();
            if (string.IsNullOrEmpty(config.Username) || string.IsNullOrEmpty(config.Password))
                return new DlpTestResult(400, new { success = false, message = "DLP Username or Password not configured." });

            httpClient = await CreateHttpClientAsync(config);
            var tokenResult = await GetTokenAsync(httpClient, config.Username, config.Password);
            if (tokenResult.StatusCode != 200) return tokenResult;
            var token = tokenResult.Content as string ?? string.Empty;

            var encodedType = Uri.EscapeDataString(type ?? "DLP");
            var encodedRule = Uri.EscapeDataString(ruleName ?? string.Empty);
            var formEncodedRule = WebUtility.UrlEncode(ruleName ?? string.Empty);
            var attempts = new[]
            {
                new { Name = "documented-double-slash-no-body", Endpoint = $"//dlp/rest/v1/policy/rules/exceptions?type={encodedType}&ruleName={encodedRule}", IncludeBody = false },
                new { Name = "documented-double-slash-with-body", Endpoint = $"//dlp/rest/v1/policy/rules/exceptions?type={encodedType}&ruleName={encodedRule}", IncludeBody = true },
                new { Name = "single-slash-no-body", Endpoint = $"/dlp/rest/v1/policy/rules/exceptions?type={encodedType}&ruleName={encodedRule}", IncludeBody = false },
                new { Name = "rule-first-double-slash-no-body", Endpoint = $"//dlp/rest/v1/policy/rules/exceptions?ruleName={encodedRule}&type={encodedType}", IncludeBody = false },
                new { Name = "plus-encoded-double-slash-no-body", Endpoint = $"//dlp/rest/v1/policy/rules/exceptions?type={encodedType}&ruleName={formEncodedRule}", IncludeBody = false }
            };

            var results = new List<object>();
            foreach (var attempt in attempts)
            {
                var requestUri = attempt.Endpoint.StartsWith("//dlp/", StringComparison.OrdinalIgnoreCase)
                    ? new Uri($"{httpClient.BaseAddress!.GetLeftPart(UriPartial.Authority)}{attempt.Endpoint}")
                    : new Uri(attempt.Endpoint, UriKind.Relative);

                using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                if (attempt.IncludeBody)
                {
                    request.Headers.TryAddWithoutValidation("Content-Type", "application/json");
                    request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
                    request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                }

                using var response = await httpClient.SendAsync(request);
                var body = await response.Content.ReadAsStringAsync();
                results.Add(new
                {
                    attempt = attempt.Name,
                    requestUri = request.RequestUri?.ToString(),
                    statusCode = (int)response.StatusCode,
                    reasonPhrase = response.ReasonPhrase,
                    success = response.IsSuccessStatusCode,
                    responsePreview = body.Length > 1000 ? body[..1000] : body
                });

                if (response.IsSuccessStatusCode)
                    break;
            }

            var anySuccess = results.Any(r =>
                r.GetType().GetProperty("success")?.GetValue(r) is true);

            return new DlpTestResult(anySuccess ? 200 : 400, new
            {
                success = anySuccess,
                message = anySuccess
                    ? "At least one Forcepoint detail request variant succeeded."
                    : "All Forcepoint detail request variants failed.",
                ruleName,
                attempts = results
            });
        }
        catch (TaskCanceledException ex) { return new DlpTestResult(408, new { success = false, message = "DLP API Timeout", error = ex.Message }); }
        catch (Exception ex) { return new DlpTestResult(500, new { success = false, message = "Error executing DLP debug request", error = ex.Message }); }
        finally { httpClient?.Dispose(); }
    }

    public async Task<DlpTestResult> GetConfigAsync()
    {
        try
        {
            var config = await _dlpConfigService.GetSensitiveConfigAsync();
            return new DlpTestResult(200, new { success = true, usernameConfigured = !string.IsNullOrEmpty(config.Username) });
        }
        catch (Exception ex)
        {
            return new DlpTestResult(500, new { success = false, error = ex.Message });
        }
    }
}
