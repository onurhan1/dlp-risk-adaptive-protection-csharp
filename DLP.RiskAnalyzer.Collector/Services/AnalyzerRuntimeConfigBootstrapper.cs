using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DLP.RiskAnalyzer.Collector.Services;

public static class AnalyzerRuntimeConfigBootstrapper
{
    public static async Task<Dictionary<string, string?>> FetchOverridesAsync(
        IConfiguration configuration,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var options = configuration.GetSection("Analyzer").Get<AnalyzerBridgeOptions>() ?? new AnalyzerBridgeOptions();
        if (string.IsNullOrWhiteSpace(options.InternalSecret))
        {
            logger.LogWarning("Analyzer internal secret is not configured. Redis runtime config bootstrap will use appsettings.json.");
            return new Dictionary<string, string?>();
        }

        try
        {
            using var client = new HttpClient
            {
                BaseAddress = new Uri(options.BaseUrl.TrimEnd('/')),
                Timeout = TimeSpan.FromSeconds(15)
            };

            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/settings/collector/runtime");
            request.Headers.Add("X-Internal-Secret", options.InternalSecret);

            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                logger.LogWarning(
                    "Failed to fetch collector runtime config from Analyzer. Status={Status}, Body={Body}. Redis will use appsettings.json.",
                    response.StatusCode,
                    body);
                return new Dictionary<string, string?>();
            }

            var payload = await response.Content.ReadFromJsonAsync<CollectorRuntimeSettings>(
                cancellationToken: cancellationToken);
            if (payload?.Redis == null)
            {
                logger.LogWarning("Analyzer returned empty collector runtime config. Redis will use appsettings.json.");
                return new Dictionary<string, string?>();
            }

            logger.LogInformation(
                "Collector runtime config loaded from Analyzer API: Redis={Host}:{Port}, PasswordSet={PasswordSet}",
                payload.Redis.Host,
                payload.Redis.Port,
                !string.IsNullOrWhiteSpace(payload.Redis.Password));

            return new Dictionary<string, string?>
            {
                ["Redis:Host"] = payload.Redis.Host,
                ["Redis:Port"] = payload.Redis.Port.ToString(),
                ["Redis:Password"] = payload.Redis.Password,
                ["Redis:StreamName"] = string.IsNullOrWhiteSpace(payload.Redis.StreamName)
                    ? "dlp:incidents"
                    : payload.Redis.StreamName
            };
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not bootstrap collector runtime config from Analyzer. Redis will use appsettings.json.");
            return new Dictionary<string, string?>();
        }
    }

    private sealed class CollectorRuntimeSettings
    {
        [JsonPropertyName("redis")]
        public RedisRuntimeSettings? Redis { get; set; }
    }

    private sealed class RedisRuntimeSettings
    {
        [JsonPropertyName("host")]
        public string Host { get; set; } = "localhost";

        [JsonPropertyName("port")]
        public int Port { get; set; } = 6379;

        [JsonPropertyName("password")]
        public string Password { get; set; } = string.Empty;

        [JsonPropertyName("stream_name")]
        public string StreamName { get; set; } = "dlp:incidents";
    }
}
