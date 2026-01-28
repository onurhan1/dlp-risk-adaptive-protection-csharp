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

        // 1. Load Configuration (Combine from Collector and Analyzer)
        var parentDir = Directory.GetParent(Directory.GetCurrentDirectory())?.FullName ?? "";
        
        var collectorConfigPath = Path.Combine(parentDir, "DLP.RiskAnalyzer.Collector", "appsettings.json");
        var analyzerConfigPath = Path.Combine(parentDir, "DLP.RiskAnalyzer.Analyzer", "appsettings.json");
        var localConfigPath = "appsettings.json";

        Console.WriteLine($"Loading DLP config from: {collectorConfigPath}");
        Console.WriteLine($"Loading DB config from: {analyzerConfigPath}");

        var config = new ConfigurationBuilder()
            .AddJsonFile(localConfigPath, optional: true, reloadOnChange: true)
            .AddJsonFile(analyzerConfigPath, optional: true, reloadOnChange: true) // Loads DB connection
            .AddJsonFile(collectorConfigPath, optional: true, reloadOnChange: true) // Loads DLP settings (wins if same keys)
            .Build();

        _managerIp = config["DLP:ManagerIP"] ?? throw new Exception("DLP:ManagerIP not configured");
        _managerPort = config.GetValue<int>("DLP:ManagerPort", 8443);
        _username = config["DLP:Username"] ?? throw new Exception("DLP:Username not configured");
        _password = config["DLP:Password"] ?? throw new Exception("DLP:Password not configured");
        
        // Connection string usually in Analyzer appsettings
        _dbConnection = config.GetConnectionString("DefaultConnection");
        if (string.IsNullOrEmpty(_dbConnection))
        {
             // Fallback: try to read directly from Analyzer config if the merge didn't work as expected 
             // (though it should). Or maybe it's in a different section?
             // In Analyzer appsettings it is under "ConnectionStrings": { "DefaultConnection": ... }
             // Let's print what we found if missing.
             throw new Exception($"ConnectionStrings:DefaultConnection not configured. Loaded keys: {string.Join(", ", config.GetChildren().Select(k => k.Key))}");
        }

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
        try 
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "auth/access-token");
            request.Headers.Add("username", _username);
            request.Headers.Add("password", _password);

            // Removing default auth headers if any
            _httpClient.DefaultRequestHeaders.Authorization = null;

            Console.WriteLine("Requesting Access Token...");
            var response = await _httpClient.SendAsync(request);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Auth Failed ({response.StatusCode}): {error}");
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            dynamic json = JsonConvert.DeserializeObject(content)!;
            // Handle snake_case or camelCase token field
            string token = json.access_token ?? json.accessToken ?? json.token;
            
            return token;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Auth Exception: {ex.Message}");
            return null;
        }
    }

    private static async Task<JArray> FetchRawIncidentsAsync(string token, DateTime start, DateTime end)
    {
        // Use Bearer Token for this request
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var requestBody = new
        {
            type = "INCIDENTS", // Required field based on Importer logic
            from_date = start.ToString("dd/MM/yyyy HH:mm:ss"), // Forcepoint format
            to_date = end.ToString("dd/MM/yyyy HH:mm:ss"),     // Forcepoint format
            limit = 10000 
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
        catch (Exception ex)
        { 
            Console.WriteLine($"Parse Error: {ex.Message}");
            return new JArray(); 
        }
    }
}
