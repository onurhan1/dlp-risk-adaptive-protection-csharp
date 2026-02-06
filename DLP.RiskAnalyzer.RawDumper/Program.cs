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
    private static string _connectionString = null!;

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
        Console.WriteLine("  --days <N>             : Last N days");
        Console.WriteLine("  --months <N>           : Last N months (e.g., --months 3 for last 3 months)");
        Console.WriteLine("  --id <incident_id>     : Fetch single incident by ID (full details)");
        Console.WriteLine("  --released             : Extract and save 'Released quarantined message' incidents to database");
        Console.WriteLine("  --released-json <path> : Extract from existing JSON file and save to database");
        Console.WriteLine();

        // Parse command line arguments
        DateTime startDate = DateTime.Now.AddHours(-24);
        DateTime endDate = DateTime.Now;
        
        var dateArg = GetArgValue(args, "--date");
        var startArg = GetArgValue(args, "--start");
        var endArg = GetArgValue(args, "--end");
        var hoursArg = GetArgValue(args, "--hours");
        var daysArg = GetArgValue(args, "--days");
        var monthsArg = GetArgValue(args, "--months");
        var idArg = GetArgValue(args, "--id");
        var releasedJsonArg = GetArgValue(args, "--released-json");
        bool releasedMode = args.Contains("--released") || !string.IsNullOrEmpty(releasedJsonArg);

        // Check if single incident mode
        bool singleIncidentMode = !string.IsNullOrEmpty(idArg);
        int singleIncidentId = 0;
        if (singleIncidentMode && !int.TryParse(idArg, out singleIncidentId))
        {
            Console.WriteLine($"ERROR: Invalid incident ID '{idArg}'");
            return;
        }

        if (!singleIncidentMode && !string.IsNullOrEmpty(dateArg))
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
        else if (!singleIncidentMode && !string.IsNullOrEmpty(startArg) && !string.IsNullOrEmpty(endArg))
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
        else if (!singleIncidentMode && !string.IsNullOrEmpty(monthsArg))
        {
            if (int.TryParse(monthsArg, out var months))
            {
                endDate = DateTime.Now;
                startDate = endDate.AddMonths(-months);
                Console.WriteLine($"Mode: Last {months} months");
            }
            else
            {
                Console.WriteLine($"ERROR: Invalid months value '{monthsArg}'");
                return;
            }
        }
        else if (!singleIncidentMode && !string.IsNullOrEmpty(daysArg))
        {
            if (int.TryParse(daysArg, out var days))
            {
                endDate = DateTime.Now;
                startDate = endDate.AddDays(-days);
                Console.WriteLine($"Mode: Last {days} days");
            }
            else
            {
                Console.WriteLine($"ERROR: Invalid days value '{daysArg}'");
                return;
            }
        }
        else if (!singleIncidentMode)
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
        var analyzerConfigPath = Path.Combine(parentDir, "DLP.RiskAnalyzer.Analyzer", "appsettings.json");
        var localConfigPath = "appsettings.json";

        Console.WriteLine($"Loading DLP config from: {collectorConfigPath}");
        Console.WriteLine($"Loading DB config from: {analyzerConfigPath}");

        var config = new ConfigurationBuilder()
            .AddJsonFile(localConfigPath, optional: true, reloadOnChange: true)
            .AddJsonFile(collectorConfigPath, optional: true, reloadOnChange: true)
            .AddJsonFile(analyzerConfigPath, optional: true, reloadOnChange: true)
            .Build();

        _connectionString = config.GetConnectionString("DefaultConnection") 
            ?? throw new Exception("ConnectionStrings:DefaultConnection not configured");

        // Released JSON mode - process existing JSON file
        if (!string.IsNullOrEmpty(releasedJsonArg))
        {
            await ProcessReleasedFromJsonFileAsync(releasedJsonArg);
            return;
        }

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

        // Released Mode - Extract and save "Released quarantined message" incidents to database
        if (releasedMode)
        {
            await ProcessReleasedIncidentsAsync(incidents);
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

    /// <summary>
    /// Process incidents from API and save "Released quarantined message" entries to database
    /// </summary>
    private static async Task ProcessReleasedIncidentsAsync(JArray incidents)
    {
        Console.WriteLine("\n=== RELEASED QUARANTINED MESSAGE EXTRACTION ===");
        
        var releasedEntries = ExtractReleasedIncidents(incidents);
        
        if (releasedEntries.Count == 0)
        {
            Console.WriteLine("No 'Released quarantined message' incidents found.");
            return;
        }

        Console.WriteLine($"Found {releasedEntries.Count} released incidents.");
        await SaveToDatabase(releasedEntries);
    }

    /// <summary>
    /// Process JSON file and save "Released quarantined message" entries to database
    /// </summary>
    private static async Task ProcessReleasedFromJsonFileAsync(string filePath)
    {
        Console.WriteLine("\n=== RELEASED QUARANTINED MESSAGE EXTRACTION FROM JSON FILE ===");
        Console.WriteLine($"Reading file: {filePath}");

        if (!File.Exists(filePath))
        {
            Console.WriteLine($"ERROR: File not found: {filePath}");
            return;
        }

        var jsonContent = await File.ReadAllTextAsync(filePath);
        JArray incidents;
        
        try
        {
            incidents = JArray.Parse(jsonContent);
            Console.WriteLine($"Loaded {incidents.Count} incidents from file.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR parsing JSON: {ex.Message}");
            return;
        }

        var releasedEntries = ExtractReleasedIncidents(incidents);
        
        if (releasedEntries.Count == 0)
        {
            Console.WriteLine("No 'Released quarantined message' incidents found.");
            return;
        }

        Console.WriteLine($"Found {releasedEntries.Count} released incidents.");
        await SaveToDatabase(releasedEntries);
    }

    /// <summary>
    /// Extract released incidents from JArray
    /// </summary>
    private static List<ReleasedIncidentRecord> ExtractReleasedIncidents(JArray incidents)
    {
        var releasedEntries = new List<ReleasedIncidentRecord>();

        foreach (var incident in incidents)
        {
            var history = incident["history"] as JArray;
            if (history == null) continue;

            // Find "Released quarantined message" entries in history
            foreach (var historyItem in history)
            {
                var taskName = historyItem["task_name"]?.ToString();
                if (taskName != "Released quarantined message") continue;

                var incidentId = incident["id"]?.Value<long>() ?? 0;
                var incidentTime = incident["incident_time"]?.ToString() ?? "";
                var action = incident["action"]?.ToString() ?? "";
                var adminName = historyItem["admin_name"]?.ToString();
                var comments = historyItem["comments"]?.ToString();
                var updateTime = historyItem["update_time"]?.ToString();

                // Parse dates
                DateTime? incidentTimestamp = null;
                if (DateTime.TryParseExact(incidentTime, new[] { "dd/MM/yyyy HH:mm:ss", "yyyy-MM-dd HH:mm:ss" },
                    null, System.Globalization.DateTimeStyles.None, out var parsedIncidentTime))
                {
                    incidentTimestamp = parsedIncidentTime;
                }

                DateTime? updateTimestamp = null;
                if (DateTime.TryParseExact(updateTime, new[] { "dd/MM/yyyy HH:mm:ss", "yyyy-MM-dd HH:mm:ss" },
                    null, System.Globalization.DateTimeStyles.None, out var parsedUpdateTime))
                {
                    updateTimestamp = parsedUpdateTime;
                }

                releasedEntries.Add(new ReleasedIncidentRecord
                {
                    IncidentId = incidentId,
                    IncidentTimestamp = incidentTimestamp ?? DateTime.MinValue,
                    Action = action,
                    TaskName = taskName,
                    AdminName = adminName,
                    Comments = comments,
                    UpdateTime = updateTimestamp
                });
            }
        }

        return releasedEntries;
    }

    /// <summary>
    /// Save released incidents to PostgreSQL database
    /// </summary>
    private static async Task SaveToDatabase(List<ReleasedIncidentRecord> entries)
    {
        Console.WriteLine($"\nSaving {entries.Count} records to database...");

        int inserted = 0;
        int skipped = 0;

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        foreach (var entry in entries)
        {
            try
            {
                // Check if already exists (upsert / conflict handling)
                var checkSql = @"SELECT COUNT(*) FROM released_incidents 
                                 WHERE incident_id = @incidentId AND update_time = @updateTime";
                
                await using var checkCmd = new NpgsqlCommand(checkSql, conn);
                checkCmd.Parameters.AddWithValue("incidentId", entry.IncidentId);
                checkCmd.Parameters.AddWithValue("updateTime", entry.UpdateTime ?? (object)DBNull.Value);
                
                var exists = Convert.ToInt32(await checkCmd.ExecuteScalarAsync()) > 0;
                
                if (exists)
                {
                    skipped++;
                    continue;
                }

                // Insert
                var insertSql = @"INSERT INTO released_incidents 
                                  (incident_id, incident_timestamp, action, task_name, admin_name, comments, update_time)
                                  VALUES (@incidentId, @incidentTimestamp, @action, @taskName, @adminName, @comments, @updateTime)";

                await using var cmd = new NpgsqlCommand(insertSql, conn);
                cmd.Parameters.AddWithValue("incidentId", entry.IncidentId);
                cmd.Parameters.AddWithValue("incidentTimestamp", entry.IncidentTimestamp);
                cmd.Parameters.AddWithValue("action", entry.Action);
                cmd.Parameters.AddWithValue("taskName", entry.TaskName);
                cmd.Parameters.AddWithValue("adminName", entry.AdminName ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("comments", entry.Comments ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("updateTime", entry.UpdateTime ?? (object)DBNull.Value);

                await cmd.ExecuteNonQueryAsync();
                inserted++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error inserting incident {entry.IncidentId}: {ex.Message}");
            }
        }

        Console.WriteLine($"\n✅ Database update complete:");
        Console.WriteLine($"   Inserted: {inserted}");
        Console.WriteLine($"   Skipped (already exists): {skipped}");
        Console.WriteLine($"   Total processed: {entries.Count}");

        // Show sample data
        Console.WriteLine("\n=== SAMPLE DATA ===");
        foreach (var entry in entries.Take(5))
        {
            Console.WriteLine($"  ID: {entry.IncidentId}, Admin: {entry.AdminName}, Comments: {entry.Comments?.Substring(0, Math.Min(50, entry.Comments?.Length ?? 0))}..., Time: {entry.UpdateTime}");
        }
    }

    /// <summary>
    /// Record class for released incidents
    /// </summary>
    private class ReleasedIncidentRecord
    {
        public long IncidentId { get; set; }
        public DateTime IncidentTimestamp { get; set; }
        public string Action { get; set; } = string.Empty;
        public string TaskName { get; set; } = string.Empty;
        public string? AdminName { get; set; }
        public string? Comments { get; set; }
        public DateTime? UpdateTime { get; set; }
    }
}