using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DLP.RiskAnalyzer.Collector.Services;

/// <summary>
/// Service to send Collector service logs to Analyzer service
/// </summary>
public class CollectorLogService
{
    private readonly HttpClient _httpClient;
    private readonly AnalyzerBridgeOptions _options;
    private readonly ILogger<CollectorLogService> _logger;

    public CollectorLogService(IOptions<AnalyzerBridgeOptions> options, ILogger<CollectorLogService> logger)
    {
        _options = options.Value;
        _logger = logger;
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_options.BaseUrl.TrimEnd('/')),
            Timeout = TimeSpan.FromSeconds(10)
        };
    }

    public async Task LogCollectionAsync(
        string message,
        bool success,
        string? errorMessage = null,
        string? details = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.InternalSecret))
        {
            _logger.LogDebug("Analyzer internal secret is not configured. Skipping log submission.");
            return;
        }

        try
        {
            var request = new
            {
                Timestamp = DateTime.UtcNow,
                Message = message,
                Details = details,
                Success = success,
                ErrorMessage = errorMessage
            };

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/logs/application/collector");
            httpRequest.Headers.Add("X-Internal-Secret", _options.InternalSecret);
            httpRequest.Content = JsonContent.Create(request);

            var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Failed to send collector log to Analyzer API. Status: {Status}, Body: {Body}",
                    response.StatusCode, body);
            }
        }
        catch (Exception ex)
        {
            // Don't throw - logging failures shouldn't break the collector service
            _logger.LogWarning(ex, "Error while sending collector log to Analyzer API");
        }
    }
}