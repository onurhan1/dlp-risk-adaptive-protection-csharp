using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DLP.RiskAnalyzer.Collector.Services;

public class AnalyzerConfigClient
{
    private readonly HttpClient _httpClient;
    private readonly AnalyzerBridgeOptions _options;
    private readonly ILogger<AnalyzerConfigClient> _logger;

    public AnalyzerConfigClient(IOptions<AnalyzerBridgeOptions> options, ILogger<AnalyzerConfigClient> logger)
    {
        _options = options.Value;
        _logger = logger;
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_options.BaseUrl.TrimEnd('/')),
            Timeout = TimeSpan.FromSeconds(15)
        };

        _logger.LogInformation(
            "Analyzer config client initialized for {BaseUrl}; InternalSecret configured={Configured}, fingerprint={Fingerprint}",
            _httpClient.BaseAddress,
            !string.IsNullOrWhiteSpace(_options.InternalSecret),
            Fingerprint(_options.InternalSecret));
    }

    public async Task<DLPConfig?> FetchConfigAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.InternalSecret))
        {
            _logger.LogWarning("Analyzer internal secret is not configured. Skipping remote config fetch.");
            return null;
        }

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/settings/dlp/runtime");
            request.Headers.Add("X-Internal-Secret", _options.InternalSecret);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogInformation("DLP configuration is not yet set up on the Analyzer.");
                    return null;
                }

                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning(
                    "Failed to fetch DLP config from Analyzer API. BaseUrl={BaseUrl}, InternalSecretFingerprint={Fingerprint}, Status={Status}, Body={Body}",
                    _httpClient.BaseAddress,
                    Fingerprint(_options.InternalSecret),
                    response.StatusCode,
                    body);
                return null;
            }

            var payload = await response.Content.ReadFromJsonAsync<DlpRuntimeSettingsDto>(cancellationToken: cancellationToken);
            if (payload == null)
            {
                _logger.LogWarning("Analyzer API returned empty configuration payload.");
                return null;
            }

            return new DLPConfig
            {
                ManagerIP = payload.ManagerIp ?? "localhost",
                ManagerPort = payload.ManagerPort,
                Username = payload.Username ?? string.Empty,
                Password = payload.Password ?? string.Empty,
                UseHttps = payload.UseHttps,
                Timeout = payload.TimeoutSeconds
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while fetching DLP configuration from Analyzer API");
            return null;
        }
    }

    private static string Fingerprint(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "empty";
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash)[..10];
    }

    /// <summary>
    /// DTO matching the Analyzer's snake_case JSON output (SnakeCaseLower policy).
    /// </summary>
    private class DlpRuntimeSettingsDto
    {
        [JsonPropertyName("manager_ip")]
        public string? ManagerIp { get; set; }

        [JsonPropertyName("manager_port")]
        public int ManagerPort { get; set; }

        [JsonPropertyName("use_https")]
        public bool UseHttps { get; set; }

        [JsonPropertyName("timeout_seconds")]
        public int TimeoutSeconds { get; set; }

        [JsonPropertyName("username")]
        public string? Username { get; set; }

        [JsonPropertyName("password")]
        public string? Password { get; set; }
    }
}
