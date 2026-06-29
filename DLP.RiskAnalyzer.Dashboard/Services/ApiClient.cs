using System.Net.Http;

namespace DLP.RiskAnalyzer.Dashboard.Services;

public static class ApiClient
{
    private static readonly HttpClient _instance = new HttpClient();
    private static bool _initialized;

    public static HttpClient Instance => _instance;

    public static void Initialize(string baseAddress)
    {
        if (!_initialized && !string.IsNullOrEmpty(baseAddress))
        {
            _instance.BaseAddress = new Uri(baseAddress);
            _initialized = true;
        }
    }
}
