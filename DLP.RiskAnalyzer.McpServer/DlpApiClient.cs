using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DLP.RiskAnalyzer.McpServer;

/// <summary>
/// DLP API ile iletişimi yöneten istemci. JWT token'ı otomatik yönetir.
/// </summary>
public class DlpApiClient
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<DlpApiClient> _logger;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);

    private string? _token;
    private DateTime _tokenExpiry = DateTime.MinValue;

    public DlpApiClient(HttpClient http, IConfiguration config, ILogger<DlpApiClient> logger)
    {
        _http = http;
        _config = config;
        _logger = logger;
    }

    // ── Auth ─────────────────────────────────────────────────────────────────

    private async Task EnsureTokenAsync()
    {
        if (_token != null && DateTime.UtcNow < _tokenExpiry.AddMinutes(-5))
            return;

        await _tokenLock.WaitAsync();
        try
        {
            if (_token != null && DateTime.UtcNow < _tokenExpiry.AddMinutes(-5))
                return;

            var username = _config["DlpApi:Username"] ?? "admin";
            var password = _config["DlpApi:Password"] ?? "admin123";

            var payload = JsonConvert.SerializeObject(new { username, password });
            var content = new StringContent(payload, Encoding.UTF8, "application/json");

            var resp = await _http.PostAsync("/api/auth/login", content);
            resp.EnsureSuccessStatusCode();

            var body = await resp.Content.ReadAsStringAsync();
            var obj = JObject.Parse(body);

            _token = obj["token"]?.ToString()
                ?? throw new Exception("Login response did not contain a token.");

            _tokenExpiry = obj["expiresAt"] is { } expVal
                ? expVal.ToObject<DateTime>()
                : DateTime.UtcNow.AddHours(8);

            _logger.LogInformation("DLP API token refreshed, expires {Expiry}", _tokenExpiry);
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    // ── HTTP helpers ──────────────────────────────────────────────────────────

    public async Task<JToken> GetAsync(string path)
    {
        await EnsureTokenAsync();

        using var req = new HttpRequestMessage(HttpMethod.Get, path);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);

        var resp = await _http.SendAsync(req);
        resp.EnsureSuccessStatusCode();

        var body = await resp.Content.ReadAsStringAsync();
        return JToken.Parse(body);
    }

    public async Task<JToken> PostAsync(string path, object data)
    {
        await EnsureTokenAsync();

        var payload = JsonConvert.SerializeObject(data);
        using var req = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);

        var resp = await _http.SendAsync(req);
        resp.EnsureSuccessStatusCode();

        var body = await resp.Content.ReadAsStringAsync();
        return JToken.Parse(body);
    }

    // ── Convenience ───────────────────────────────────────────────────────────

    public async Task<string> GetJsonAsync(string path)
        => (await GetAsync(path)).ToString(Formatting.Indented);

    public async Task<string> PostJsonAsync(string path, object data)
        => (await PostAsync(path, data)).ToString(Formatting.Indented);

    /// <summary>
    /// Query string parametrelerini temizler (null/boş değerleri atar).
    /// </summary>
    public static string BuildQuery(string path, Dictionary<string, string?> @params)
    {
        var parts = @params
            .Where(kv => !string.IsNullOrWhiteSpace(kv.Value))
            .Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value!)}");

        var qs = string.Join("&", parts);
        return string.IsNullOrEmpty(qs) ? path : $"{path}?{qs}";
    }
}
