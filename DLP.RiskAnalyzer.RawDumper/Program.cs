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
        Console.WriteLine("  --id <incident_id>     : Fetch single incident by ID (full details)");
        Console.WriteLine("  --ids <id1,id2,...>    : Fetch multiple incidents by IDs (bulk mode)");
        Console.WriteLine();

        // Parse command line arguments
        DateTime startDate = DateTime.Now.AddHours(-24);
        DateTime endDate = DateTime.Now;
        
        var dateArg = GetArgValue(args, "--date");
        var startArg = GetArgValue(args, "--start");
        var endArg = GetArgValue(args, "--end");
        var hoursArg = GetArgValue(args, "--hours");
        var idArg = GetArgValue(args, "--id");
        var idsArg = GetArgValue(args, "--ids");

        // Check if single incident mode
        bool singleIncidentMode = !string.IsNullOrEmpty(idArg);
        int singleIncidentId = 0;
        if (singleIncidentMode && !int.TryParse(idArg, out singleIncidentId))
        {
            Console.WriteLine($"ERROR: Invalid incident ID '{idArg}'");
            return;
        }

        // Check if bulk incidents mode
        bool bulkIncidentMode = !string.IsNullOrEmpty(idsArg);
        List<int> bulkIncidentIds = new List<int>();
        if (bulkIncidentMode)
        {
            foreach (var idStr in idsArg!.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                if (int.TryParse(idStr.Trim(), out var parsedId))
                {
                    bulkIncidentIds.Add(parsedId);
                }
                else
                {
                    Console.WriteLine($"WARNING: Invalid incident ID '{idStr}' - skipping");
                }
            }
            if (bulkIncidentIds.Count == 0)
            {
                Console.WriteLine("ERROR: No valid incident IDs provided");
                return;
            }
            Console.WriteLine($"Mode: Bulk IDs - {bulkIncidentIds.Count} incidents");
        }

        if (!singleIncidentMode && !bulkIncidentMode && !string.IsNullOrEmpty(dateArg))
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
        else if (!singleIncidentMode && !bulkIncidentMode && !string.IsNullOrEmpty(startArg) && !string.IsNullOrEmpty(endArg))
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
        else if (!singleIncidentMode && !bulkIncidentMode)
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

        if (!singleIncidentMode && !bulkIncidentMode)
        {
            Console.WriteLine($"Date Range: {startDate:dd/MM/yyyy HH:mm:ss} -> {endDate:dd/MM/yyyy HH:mm:ss}");
            Console.WriteLine();
        }

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

        // Bulk Incidents Mode - fetch multiple incidents by IDs and dump all data
        if (bulkIncidentMode)
        {
            Console.WriteLine($"\n📦 Fetching {bulkIncidentIds.Count} incidents by IDs...");
            var incidents = await FetchIncidentsByIdsAsync(token, bulkIncidentIds);
            
            if (incidents.Count == 0)
            {
                Console.WriteLine("No incidents found.");
                return;
            }
            
            Console.WriteLine($"✅ Retrieved {incidents.Count} incidents");
            
            var ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            
            // Save full JSON
            var jsonPath = $"bulk_incidents_{ts}.json";
            File.WriteAllText(jsonPath, JsonConvert.SerializeObject(incidents, Formatting.Indented));
            Console.WriteLine($"📄 Full JSON saved to: {jsonPath}");
            
            // Save CSV for source fields
            var csvPath = $"bulk_source_data_{ts}.csv";
            using (var writer = new StreamWriter(csvPath, false, Encoding.UTF8))
            {
                // Extended CSV header with all source fields
                writer.WriteLine("incident_id,event_time,severity,action,channel,policies,destination,file_name,maximum_matches,manager,department,ip_address,login_name,host_name,email_address,dn,nt_domain,business_unit,risk_level,full_cn");
                
                foreach (var inc in incidents)
                {
                    var incidentId = inc["id"]?.ToString() ?? "";
                    var eventTime = inc["event_time"]?.ToString() ?? "";
                    var severity = inc["severity"]?.ToString() ?? "";
                    var action = inc["action"]?.ToString() ?? "";
                    var channel = inc["channel"]?.ToString() ?? "";
                    var policies = inc["policies"]?.ToString() ?? "";
                    var destination = inc["destination"]?.ToString() ?? "";
                    var fileName = inc["file_name"]?.ToString() ?? "";
                    var maxMatches = inc["maximum_matches"]?.ToString() ?? "";
                    var riskLevel = inc["risk_level"]?.ToString() ?? "";
                    var businessUnit = inc["business_unit"]?.ToString() ?? "";
                    
                    var source = inc["source"];
                    var manager = source?["manager"]?.ToString() ?? "";
                    var department = source?["department"]?.ToString() ?? "";
                    var ipAddress = source?["ip_address"]?.ToString() ?? "";
                    var loginName = source?["login_name"]?.ToString() ?? "";
                    var hostName = source?["host_name"]?.ToString() ?? "";
                    var emailAddress = source?["email_address"]?.ToString() ?? "";
                    var dn = source?["dn"]?.ToString() ?? "";
                    var ntDomain = source?["nt_domain"]?.ToString() ?? "";
                    
                    // Extract CN (Common Name) from DN if present
                    var fullCn = "";
                    if (!string.IsNullOrEmpty(dn) && dn.Contains("CN="))
                    {
                        var cnStart = dn.IndexOf("CN=") + 3;
                        var cnEnd = dn.IndexOf(",", cnStart);
                        if (cnEnd > cnStart) fullCn = dn.Substring(cnStart, cnEnd - cnStart);
                        else fullCn = dn.Substring(cnStart);
                    }
                    
                    string Escape(string val) => $"\"{val.Replace("\"", "\"\"")}\"";
                    
                    writer.WriteLine($"{Escape(incidentId)},{Escape(eventTime)},{Escape(severity)},{Escape(action)},{Escape(channel)},{Escape(policies)},{Escape(destination)},{Escape(fileName)},{Escape(maxMatches)},{Escape(manager)},{Escape(department)},{Escape(ipAddress)},{Escape(loginName)},{Escape(hostName)},{Escape(emailAddress)},{Escape(dn)},{Escape(ntDomain)},{Escape(businessUnit)},{Escape(riskLevel)},{Escape(fullCn)}");
                }
            }
            Console.WriteLine($"📊 CSV saved to: {csvPath}");
            
            // Print all source fields found
            Console.WriteLine("\n=== ALL SOURCE FIELDS FOUND ===");
            var allSourceFields = new HashSet<string>();
            foreach (var inc in incidents)
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
            Console.WriteLine($"Fields: {string.Join(", ", allSourceFields.OrderBy(x => x))}");
            
            // Print detailed source data for each incident
            Console.WriteLine("\n=== DETAILED SOURCE DATA ===");
            foreach (var inc in incidents)
            {
                var incId = inc["id"]?.ToString() ?? "?";
                var source = inc["source"] as JObject;
                Console.WriteLine($"\n--- Incident {incId} ---");
                if (source != null)
                {
                    foreach (var prop in source.Properties())
                    {
                        var val = prop.Value?.ToString() ?? "(null)";
                        if (val.Length > 80) val = val.Substring(0, 80) + "...";
                        Console.WriteLine($"  {prop.Name}: {val}");
                    }
                }
                else
                {
                    Console.WriteLine("  (no source object)");
                }
            }
            
            Console.WriteLine("\n✅ Done!");
            return;
        }

        // Single Incident Mode - fetch by ID and dump full JSON
        if (singleIncidentMode)
        {
            Console.WriteLine($"\nFetching incident ID: {singleIncidentId} ...");
            var incident = await FetchIncidentByIdAsync(token, singleIncidentId);
            
            if (incident == null)
            {
                Console.WriteLine("Incident not found.");
                return;
            }
            
            var ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var outPath = $"incident_{singleIncidentId}_{ts}.json";
            File.WriteAllText(outPath, JsonConvert.SerializeObject(incident, Formatting.Indented));
            Console.WriteLine($"\n✅ Full incident saved to: {outPath}");
            
            // Print all fields
            Console.WriteLine("\n=== ALL ROOT FIELDS ===");
            if (incident is JObject incObj)
            {
                foreach (var prop in incObj.Properties())
                {
                    var val = prop.Value?.ToString() ?? "(null)";
                    if (val.Length > 100) val = val.Substring(0, 100) + "...";
                    Console.WriteLine($"  {prop.Name}: {val}");
                }
            }
            
            // Print all source fields
            Console.WriteLine("\n=== ALL SOURCE FIELDS ===");
            var source = incident["source"] as JObject;
            if (source != null)
            {
                foreach (var prop in source.Properties())
                {
                    Console.WriteLine($"  {prop.Name}: {prop.Value}");
                }
            }
            else
            {
                Console.WriteLine("  (no source object)");
            }
            
            Console.WriteLine("\nDone!");
            return;
        }

        // Fetch Incidents by date range
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

    private static async Task<JToken?> FetchIncidentByIdAsync(string token, int incidentId)
    {
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var requestBody = new
        {
            type = "INCIDENTS",
            ids = new[] { incidentId }
        };

        var jsonBody = JsonConvert.SerializeObject(requestBody);
        Console.WriteLine($"Request: {jsonBody}");
        
        var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync("incidents", content);
        
        Console.WriteLine($"Response Status: {response.StatusCode}");
        
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Error: {error}");
            return null;
        }

        var responseString = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"Raw Response Length: {responseString.Length} chars");
        
        if (string.IsNullOrWhiteSpace(responseString)) return null;

        try 
        {
            var jsonObj = JObject.Parse(responseString);
            
            // incidents array içindeki ilk elemanı dön
            if (jsonObj["incidents"] is JArray arr && arr.Count > 0)
            {
                return arr[0];
            }
            
            return jsonObj;
        }
        catch
        {
            // Array olarak dene
            var arr = JArray.Parse(responseString);
            return arr.Count > 0 ? arr[0] : null;
        }
    }

    private static async Task<List<JToken>> FetchIncidentsByIdsAsync(string token, List<int> incidentIds)
    {
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var requestBody = new
        {
            type = "INCIDENTS",
            ids = incidentIds
        };

        var jsonBody = JsonConvert.SerializeObject(requestBody);
        Console.WriteLine($"Request: {jsonBody}");
        
        var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync("incidents", content);
        
        Console.WriteLine($"Response Status: {response.StatusCode}");
        
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Error: {error}");
            return new List<JToken>();
        }

        var responseString = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"Raw Response Length: {responseString.Length} chars");
        
        if (string.IsNullOrWhiteSpace(responseString)) return new List<JToken>();

        try 
        {
            var jsonObj = JObject.Parse(responseString);
            
            if (jsonObj["incidents"] is JArray arr)
            {
                return arr.ToList();
            }
            
            return new List<JToken>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Parse Error: {ex.Message}");
            return new List<JToken>();
        }
    }
}