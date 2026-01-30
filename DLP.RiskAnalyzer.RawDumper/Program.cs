using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DLP.RiskAnalyzer.RawDumper;

public class Program
{
    private static HttpClient _httpClient = null!;
    private static string _managerIp = null!;
    private static int _managerPort;
    private static string _username = null!;
    private static string _password = null!;

    public static async Task Main(string[] args)
    {
        Console.WriteLine("===============================================");
        Console.WriteLine("   DLP SOURCE DATA EXPORTER");
        Console.WriteLine("===============================================");
        Console.WriteLine();
        Console.WriteLine("Usage: dotnet run [options]");
        Console.WriteLine("  --date <dd/MM/yyyy>    : Fetch data for a specific date");
        Console.WriteLine("  --start <dd/MM/yyyy>   : Start date for range");
        Console.WriteLine("  --end <dd/MM/yyyy>     : End date for range");
        Console.WriteLine("  --hours <N>            : Last N hours (default: 24)");
        Console.WriteLine();

        // Parse command line arguments
        DateTime startDate;
        DateTime endDate;
        
        var dateArg = GetArgValue(args, "--date");
        var startArg = GetArgValue(args, "--start");
        var endArg = GetArgValue(args, "--end");
        var hoursArg = GetArgValue(args, "--hours");

        if (!string.IsNullOrEmpty(dateArg))
        {
            if (!DateTime.TryParseExact(dateArg, new[] { "dd/MM/yyyy", "yyyy-MM-dd", "dd-MM-yyyy" }, 
                null, System.Globalization.DateTimeStyles.None, out var specificDate))
            {
                Console.WriteLine($"ERROR: Invalid date format '{dateArg}'. Use dd/MM/yyyy");
                return;
            }
            startDate = specificDate.Date;
            endDate = specificDate.Date.AddDays(1).AddSeconds(-1);
            Console.WriteLine($"Mode: Specific Date - {specificDate:dd/MM/yyyy}");
        }
        else if (!string.IsNullOrEmpty(startArg) && !string.IsNullOrEmpty(endArg))
        {
            if (!DateTime.TryParseExact(startArg, new[] { "dd/MM/yyyy", "yyyy-MM-dd", "dd-MM-yyyy" }, 
                null, System.Globalization.DateTimeStyles.None, out startDate))
            {
                Console.WriteLine($"ERROR: Invalid start date format '{startArg}'. Use dd/MM/yyyy");
                return;
            }
            if (!DateTime.TryParseExact(endArg, new[] { "dd/MM/yyyy", "yyyy-MM-dd", "dd-MM-yyyy" }, 
                null, System.Globalization.DateTimeStyles.None, out endDate))
            {
                Console.WriteLine($"ERROR: Invalid end date format '{endArg}'. Use dd/MM/yyyy");
                return;
            }
            startDate = startDate.Date;
            endDate = endDate.Date.AddDays(1).AddSeconds(-1);
            Console.WriteLine($"Mode: Date Range - {startDate:dd/MM/yyyy} to {endDate:dd/MM/yyyy}");
        }
        else
        {
            int hours = 24;
            if (!string.IsNullOrEmpty(hoursArg) && int.TryParse(hoursArg, out var parsedHours))
            {
                hours = parsedHours;
            }
            endDate = DateTime.Now;
            startDate = endDate.AddHours(-hours);
            Console.WriteLine($"Mode: Last {hours} hours");
        }

        Console.WriteLine($"Date Range: {startDate:dd/MM/yyyy HH:mm:ss} -> {endDate:dd/MM/yyyy HH:mm:ss}");
        Console.WriteLine();

        // Load Configuration
        var parentDir = Directory.GetParent(Directory.GetCurrentDirectory())?.FullName ?? "";
        var collectorConfigPath = Path.Combine(parentDir, "DLP.RiskAnalyzer.Collector", "appsettings.json");
        var localConfigPath = "appsettings.json";

        Console.WriteLine($"Loading DLP config from: {collectorConfigPath}");

        var config = new ConfigurationBuilder()
            .AddJsonFile(localConfigPath, optional: true, reloadOnChange: true)
            .AddJsonFile(collectorConfigPath, optional: true, reloadOnChange: true)
            .Build();

        _managerIp = config["DLP:ManagerIP"] ?? throw new Exception("DLP:ManagerIP not configured");
        _managerPort = config.GetValue<int>("DLP:ManagerPort", 8443);
        _username = config["DLP:Username"] ?? throw new Exception("DLP:Username not configured");
        _password = config["DLP:Password"] ?? throw new Exception("DLP:Password not configured");

        // Setup HttpClient
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
        };
        _httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri($"https://{_managerIp}:{_managerPort}/dlp/rest/v1/"),
            Timeout = TimeSpan.FromMinutes(5)
        };

        // Auth
        Console.WriteLine("Authenticating...");
        var token = await GetAccessTokenAsync();
        if (string.IsNullOrEmpty(token))
        {
            Console.WriteLine("Authentication Failed!");
            return;
        }
        Console.WriteLine("Authenticated.");

        // Fetch Incidents
        Console.WriteLine($"Fetching incidents...");
        var incidents = await FetchRawIncidentsAsync(token, startDate, endDate);
        Console.WriteLine($"Found {incidents.Count} incidents.");

        if (incidents.Count == 0)
        {
            Console.WriteLine("No incidents found for the specified date range.");
            return;
        }

        // Export to CSV
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var csvPath = $"source_dump_{timestamp}.csv";
        
        Console.WriteLine($"\nExporting to {csvPath}...");
        
        using var writer = new StreamWriter(csvPath, false, Encoding.UTF8);
        // CSV Header
        writer.WriteLine("incident_id,event_time,severity,action,channel,policies,destination,file_name,maximum_matches,manager,department,ip_address,login_name,host_name,email_address,dn,nt_domain");
        
        foreach (var inc in incidents)
        {
            var incidentId = inc["id"]?.ToString() ?? inc["incident_id"]?.ToString() ?? "";
            var eventTime = inc["event_time"]?.ToString() ?? inc["date_time"]?.ToString() ?? "";
            var severity = inc["severity"]?.ToString() ?? "";
            var action = inc["action"]?.ToString() ?? "";
            var channel = inc["channel"]?.ToString() ?? "";
            var policies = inc["policies"]?.ToString() ?? "";
            var destination = inc["destination"]?.ToString() ?? "";
            var fileName = inc["file_name"]?.ToString() ?? "";
            var maxMatches = inc["maximum_matches"]?.ToString() ?? "";
            
            var source = inc["source"];
            var manager = source?["manager"]?.ToString() ?? "";
            var department = source?["department"]?.ToString() ?? "";
            var ipAddress = source?["ip_address"]?.ToString() ?? "";
            var loginName = source?["login_name"]?.ToString() ?? "";
            var hostName = source?["host_name"]?.ToString() ?? "";
            var emailAddress = source?["email_address"]?.ToString() ?? "";
            var dn = source?["dn"]?.ToString() ?? "";
            var ntDomain = source?["nt_domain"]?.ToString() ?? "";
            
            string Escape(string val) => $"\"{val.Replace("\"", "\"\"")}\"";
            
            writer.WriteLine($"{Escape(incidentId)},{Escape(eventTime)},{Escape(severity)},{Escape(action)},{Escape(channel)},{Escape(policies)},{Escape(destination)},{Escape(fileName)},{Escape(maxMatches)},{Escape(manager)},{Escape(department)},{Escape(ipAddress)},{Escape(loginName)},{Escape(hostName)},{Escape(emailAddress)},{Escape(dn)},{Escape(ntDomain)}");
        }
        
        Console.WriteLine($"Exported {incidents.Count} records to {csvPath}");
        
        // Export first 3 incidents as full JSON for field discovery
        var jsonPath = $"raw_sample_{timestamp}.json";
        var sampleIncidents = incidents.Take(3).ToList();
        File.WriteAllText(jsonPath, JsonConvert.SerializeObject(sampleIncidents, Formatting.Indented));
        Console.WriteLine($"Saved {sampleIncidents.Count} sample incidents to {jsonPath} (for field discovery)");
        
        // List all source fields found
        Console.WriteLine("\n=== SOURCE OBJECT FIELDS FOUND ===");
        var allSourceFields = new HashSet<string>();
        foreach (var inc in incidents.Take(50))
        {
            var source = inc["source"] as JObject;
            if (source != null)
            {
                foreach (var prop in source.Properties())
                {
                    allSourceFields.Add(prop.Name);
                }
            }
        }
        Console.WriteLine($"Fields in source: {string.Join(", ", allSourceFields.OrderBy(x => x))}");
        
        // Summary
        Console.WriteLine("\n=== UNIQUE VALUES SUMMARY ===");
        
        var uniqueManagers = incidents.Select(i => i["source"]?["manager"]?.ToString()).Where(x => !string.IsNullOrEmpty(x)).Distinct().Take(20).ToList();
        var uniqueDepts = incidents.Select(i => i["source"]?["department"]?.ToString()).Where(x => !string.IsNullOrEmpty(x)).Distinct().Take(20).ToList();
        var uniqueChannels = incidents.Select(i => i["channel"]?.ToString()).Where(x => !string.IsNullOrEmpty(x)).Distinct().ToList();
        var uniqueActions = incidents.Select(i => i["action"]?.ToString()).Where(x => !string.IsNullOrEmpty(x)).Distinct().ToList();
        
        Console.WriteLine($"\nChannels ({uniqueChannels.Count}): {string.Join(", ", uniqueChannels)}");
        Console.WriteLine($"Actions ({uniqueActions.Count}): {string.Join(", ", uniqueActions)}");
        Console.WriteLine($"\nDepartments (first 20): {string.Join(", ", uniqueDepts)}");
        Console.WriteLine($"\nManagers (first 20): {string.Join(", ", uniqueManagers)}");
        
        Console.WriteLine("\nDone!");
    }

    private static string? GetArgValue(string[] args, string key)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i].Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }
        return null;
    }

    private static async Task<string?> GetAccessTokenAsync()
    {
        try 
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "auth/access-token");
            request.Headers.Add("username", _username);
            request.Headers.Add("password", _password);
            _httpClient.DefaultRequestHeaders.Authorization = null;

            var response = await _httpClient.SendAsync(request);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Auth Failed ({response.StatusCode}): {error}");
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            dynamic json = JsonConvert.DeserializeObject(content)!;
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
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var fromDateStr = start.ToString("dd/MM/yyyy HH:mm:ss");
        var toDateStr = end.ToString("dd/MM/yyyy HH:mm:ss");

        var requestBody = new
        {
            type = "INCIDENTS",
            from_date = fromDateStr,
            to_date = toDateStr,
            limit = 10000 
        };

        var jsonBody = JsonConvert.SerializeObject(requestBody);
        var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
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
