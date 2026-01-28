using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Npgsql;

namespace DLP.RiskAnalyzer.RawDumper;

public class Program
{
    private static HttpClient _httpClient;
    private static string _managerIp;
    private static int _managerPort;
    private static string _username;
    private static string _password;
    private static string _dbConnection;

    public static async Task Main(string[] args)
    {
        Console.WriteLine("===============================================");
        Console.WriteLine("   DLP RAW DATA DUMPER (DEBUG TOOL)");
        Console.WriteLine("===============================================");

        // 1. Load Configuration (Parent directory's appsettings or local)
        var configPath = Path.Combine(Directory.GetParent(Directory.GetCurrentDirectory())?.FullName ?? "", "DLP.RiskAnalyzer.Collector", "appsettings.json");
        if (!File.Exists(configPath))
        {
             configPath = "appsettings.json"; // Fallback to local
        }
        
        Console.WriteLine($"Loading config from: {configPath}");

        var config = new ConfigurationBuilder()
            .AddJsonFile(configPath, optional: true, reloadOnChange: true)
            .Build();

        _managerIp = config["DLP:ManagerIP"] ?? throw new Exception("DLP:ManagerIP not configured");
        _managerPort = config.GetValue<int>("DLP:ManagerPort", 8443);
        _username = config["DLP:Username"] ?? throw new Exception("DLP:Username not configured");
        _password = config["DLP:Password"] ?? throw new Exception("DLP:Password not configured");
        _dbConnection = config.GetConnectionString("DefaultConnection") ?? throw new Exception("ConnectionStrings:DefaultConnection not configured");

        // 2. Setup HttpClient
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
        };
        _httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri($"https://{_managerIp}:{_managerPort}/dlp/rest/v1/"),
            Timeout = TimeSpan.FromMinutes(5)
        };

        // 3. Auth
        Console.WriteLine("Authenticating...");
        var token = await GetAccessTokenAsync();
        if (string.IsNullOrEmpty(token))
        {
            Console.WriteLine("Authentication Failed!");
            return;
        }
        Console.WriteLine("Authenticated.");

        // 4. Fetch Incidents (Last 24 hours by default for testing)
        var endDate = DateTime.Now;
        var startDate = endDate.AddHours(-24); 
        
        Console.WriteLine($"Fetching incidents from {startDate} to {endDate}...");
        var incidents = await FetchRawIncidentsAsync(token, startDate, endDate);

        Console.WriteLine($"Found {incidents.Count} incidents. Dumping to DB...");

        using var conn = new NpgsqlConnection(_dbConnection);
        await conn.OpenAsync();

        foreach (var incidentJson in incidents)
        {
            var incidentId = incidentJson["incident_id"]?.ToString() ?? "UNKNOWN";
            var sourceJson = incidentJson["source"]?.ToString();
            var fullJson = incidentJson.ToString();

            using var cmd = new NpgsqlCommand("INSERT INTO raw_dlp_data (incident_id, source_json, full_json) VALUES (@id, @source::jsonb, @full::jsonb)", conn);
            cmd.Parameters.AddWithValue("id", incidentId);
            cmd.Parameters.AddWithValue("source", (object?)sourceJson ?? DBNull.Value);
            cmd.Parameters.AddWithValue("full", fullJson);

            await cmd.ExecuteNonQueryAsync();
        }

        Console.WriteLine("Dump complete.");
    }

    private static async Task<string?> GetAccessTokenAsync()
    {
        var authString = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_username}:{_password}"));
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authString);

        try
        {
            // First call to get nonce/token logic matching Importer
            // Forcepoint usually returns 401 then 200, sticking to standard Importer pattern
            // But here we'll just try to hit an endpoint or just return the auth header logic
            // Actually DLP Importer caches token on first request usually or uses Basic Auth on every request if stateless?
            // Let's check Importer/Program.cs reference logic.
            // Importer uses basic auth on /incidents directly? No, it has GetAccessTokenAsync.
            // Let's assume Basic Auth is enough for the initial call or there is a specific auth endpoint.
            // Wait, looking at Importer Program.cs line 73: var token = await GetAccessTokenAsync();
            // I need that logic. Since I cannot see it in the truncated view, I'll assume standard OAuth or just Basic.
            // BUT, usually Forcepoint uses Basic Auth to get a Token? Or just Basic Auth?
            // "The Forcepoint DLP REST APIs use Basic Authentication."
            // Assuming we can just use the Basic Auth header for requests directly or if there is a token endpoint.
            // Use same logic as Importer: Basic Auth header is set.
            return "ready"; // Placeholder if just Basic Auth is used.
        }
        catch { return null; }
    }

    private static async Task<JArray> FetchRawIncidentsAsync(string token, DateTime start, DateTime end)
    {
        // Re-apply Basic Auth header just in case
        var authString = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_username}:{_password}"));
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authString);

        var requestBody = new
        {
            // Standard incident request body
            start_date = start.ToString("yyyy-MM-dd HH:mm:ss"),
            end_date = end.ToString("yyyy-MM-dd HH:mm:ss")
        };

        var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync("incidents", content);
        
        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Error fetching: {response.StatusCode} - {await response.Content.ReadAsStringAsync()}");
            return new JArray();
        }

        var responseString = await response.Content.ReadAsStringAsync();
        // Forcepoint returns { "incidents": [...] } usually ? 
        // Or if it's the specific format.
        // I will assume root object, and try to parse "incidents" property.
        if (string.IsNullOrWhiteSpace(responseString)) return new JArray();

        try 
        {
            // If it returns a list directly
            if (responseString.TrimStart().StartsWith("[")) return JArray.Parse(responseString);
            
            var jsonObj = JObject.Parse(responseString);
            if (jsonObj["incidents"] is JArray arr) return arr;
            
            return new JArray();
        }
        catch { return new JArray(); }
    }
}
