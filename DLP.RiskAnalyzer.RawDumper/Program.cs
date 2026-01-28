using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Npgsql;

namespace DLP.RiskAnalyzer.RawDumper;

public class Program
{
    private static HttpClient _httpClient = null!;
    private static string _managerIp = null!;
    private static int _managerPort;
    private static string _username = null!;
    private static string _password = null!;
    private static string _dbConnection = null!;

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

    private static Task<string?> GetAccessTokenAsync()
    {
        var authString = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_username}:{_password}"));
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authString);

        // Return completed task with result to satisfy async/task requirement without compiler warning
        return Task.FromResult<string?>("ready");
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
