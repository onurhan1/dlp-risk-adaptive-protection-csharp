using System.Net.Http.Headers;
using System.Text;
using DLP.RiskAnalyzer.Analyzer.Data;
using DLP.RiskAnalyzer.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace DLP.RiskAnalyzer.Importer;

public class Program
{
    private static HttpClient _httpClient;
    private static string _managerIp;
    private static int _managerPort;
    private static string _username;
    private static string _password;
    private static string _dbConnection;
    private static int _batchSize = 1000;

    public static async Task Main(string[] args)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("===============================================");
        Console.WriteLine("   DLP RISK ANALYZER - HISTORICAL DATA IMPORTER");
        Console.WriteLine("===============================================");
        Console.ResetColor();

        // 1. Load Configuration
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .Build();

        _managerIp = config["DLP:ManagerIP"] ?? throw new Exception("DLP:ManagerIP not configured");
        _managerPort = config.GetValue<int>("DLP:ManagerPort", 8443);
        _username = config["DLP:Username"] ?? throw new Exception("DLP:Username not configured");
        _password = config["DLP:Password"] ?? throw new Exception("DLP:Password not configured");
        _dbConnection = config.GetConnectionString("DefaultConnection") ?? throw new Exception("ConnectionStrings:DefaultConnection not configured");

        Console.WriteLine($"Target API: https://{_managerIp}:{_managerPort}");
        Console.WriteLine($"Target DB : {_dbConnection.Split(';')[0]}..."); // Hide sensitive info

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

        // 3. Get Date Range
        Console.WriteLine();
        Console.Write("Enter Start Date (dd/MM/yyyy) [default: 01/01/2024]: ");
        var startInput = Console.ReadLine();
        var startDate = string.IsNullOrWhiteSpace(startInput) 
            ? new DateTime(2024, 1, 1) 
            : DateTime.ParseExact(startInput, "dd/MM/yyyy", null);

        Console.Write("Enter End Date (dd/MM/yyyy) [default: Today]: ");
        var endInput = Console.ReadLine();
        var endDate = string.IsNullOrWhiteSpace(endInput) 
            ? DateTime.Now 
            : DateTime.ParseExact(endInput, "dd/MM/yyyy", null).AddDays(1).AddSeconds(-1);

        Console.WriteLine($"Importing headers from {startDate} to {endDate}...");
        Console.WriteLine("-----------------------------------------------");

        // 4. Authenticate
        var token = await GetAccessTokenAsync();
        if (string.IsNullOrEmpty(token))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Failed to authenticate!");
            return;
        }

        // 5. Setup DB Context
        var dbOptions = new DbContextOptionsBuilder<AnalyzerDbContext>()
            .UseNpgsql(_dbConnection)
            .Options;

        // 6. Loop chunks
        var currentStart = startDate;
        var chunkSizeHours = 4;
        var totalIncidentsImported = 0;

        while (currentStart < endDate)
        {
            var currentEnd = currentStart.AddHours(chunkSizeHours);
            if (currentEnd > endDate) currentEnd = endDate;

            Console.Write($"Fetching {currentStart:dd/MM HH:mm} - {currentEnd:dd/MM HH:mm}... ");

            try
            {
                var incidents = await FetchIncidentsAsync(token, currentStart, currentEnd);
                
                if (incidents.Count > 0)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write($"{incidents.Count} found. ");
                    Console.ResetColor();

                    // Insert to DB
                    using var context = new AnalyzerDbContext(dbOptions);
                    
                    // Convert to DB Model
                    var dbIncidents = incidents.Select(i => MapToIncident(i)).ToList();

                    // Check existing to prevent duplicates
                    var ids = dbIncidents.Select(x => x.Id).ToList();
                    var existingIds = await context.Incidents
                        .Where(x => ids.Contains(x.Id))
                        .Select(x => x.Id)
                        .ToListAsync();

                    var newIncidents = dbIncidents.Where(x => !existingIds.Contains(x.Id)).ToList();

                    if (newIncidents.Count > 0)
                    {
                        await context.Incidents.AddRangeAsync(newIncidents);
                        await context.SaveChangesAsync();
                        Console.Write($"Inserted {newIncidents.Count} new. ");
                        totalIncidentsImported += newIncidents.Count;
                    }
                    else
                    {
                        Console.Write("All skipped (duplicates). ");
                    }
                }
                else
                {
                    Console.Write("0 found. ");
                }
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error: {ex.Message}");
                Console.ResetColor();
                // Don't stop, try next chunk
            }

            currentStart = currentEnd;
            await Task.Delay(500); // Small pause
        }

        Console.WriteLine("-----------------------------------------------");
        Console.WriteLine($"DONE! Total imported: {totalIncidentsImported}");
    }

    private static async Task<string> GetAccessTokenAsync()
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "auth/access-token");
            request.Headers.Add("username", _username);
            request.Headers.Add("password", _password);

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            dynamic json = JsonConvert.DeserializeObject(content);
            return json.access_token ?? json.accessToken ?? json.token;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Auth Error: {ex.Message}");
            return null;
        }
    }

    private static async Task<List<DLPIncident>> FetchIncidentsAsync(string token, DateTime start, DateTime end)
    {
        var requestBody = new
        {
            type = "INCIDENTS",
            from_date = start.ToString("dd/MM/yyyy HH:mm:ss"),
            to_date = end.ToString("dd/MM/yyyy HH:mm:ss"),
            limit = 10000 
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "incidents/");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request);
        
        if (!response.IsSuccessStatusCode)
        {
            // If 404/420 means no incidents often
            return new List<DLPIncident>(); 
        }

        var content = await response.Content.ReadAsStringAsync();
        
        // Handle different response formats
        try 
        {
            var wrapper = JsonConvert.DeserializeObject<DLPIncidentResponse>(content);
            return wrapper.Incidents ?? new List<DLPIncident>();
        }
        catch
        {
            try
            {
                return JsonConvert.DeserializeObject<List<DLPIncident>>(content) ?? new List<DLPIncident>();
            }
            catch
            {
                return new List<DLPIncident>();
            }
        }
    }

    private static Incident MapToIncident(DLPIncident apiModel)
    {
        // Calculate max matches
        int maxMatches = 0;
        if (apiModel.ViolationTriggers != null && apiModel.ViolationTriggers.Count > 0)
        {
             maxMatches = apiModel.ViolationTriggers
                 .SelectMany(t => t.Classifiers ?? new List<DLPClassifier>())
                 .Max(c => (int?)c.NumberMatches) ?? 0;
        }

            // Calculate Risk Score
            var riskAnalyzer = new DLP.RiskAnalyzer.Shared.Services.RiskAnalyzer();
            
            // Determine Destination Score (using simplified logic or default)
            // Ideally we would query database for NdaDomains, but for speed in Importer we use a simplified check
            // or we could load domains into memory at startup if critical.
            // For now, using default logic:
            int destinationScore;
            
            // Basic domain check logic (simplified for importer)
            if (string.IsNullOrEmpty(apiModel.Destination))
            {
                 destinationScore = DLP.RiskAnalyzer.Shared.Constants.RiskConstants.DestinationScores.Unknown;
            }
            else if (apiModel.Destination.ToLower().Contains("printer") || apiModel.Channel?.ToLower().Contains("print") == true)
            {
                 destinationScore = DLP.RiskAnalyzer.Shared.Constants.RiskConstants.DestinationScores.Printer;
            }
            else
            {
                 // Default to NO NDA (Higher Risk) to be safe, or 5
                 destinationScore = DLP.RiskAnalyzer.Shared.Constants.RiskConstants.DestinationScores.NdaAbsent;
                 
                 // If you want to check for personal emails basic list:
                 var personalDomains = new[] { "gmail.com", "hotmail.com", "outlook.com", "yahoo.com", "icloud.com" };
                 foreach (var domain in personalDomains)
                 {
                     if (apiModel.Destination.Contains(domain, StringComparison.OrdinalIgnoreCase))
                     {
                         destinationScore = DLP.RiskAnalyzer.Shared.Constants.RiskConstants.DestinationScores.Personal;
                         break;
                     }
                 }
            }

            var calculatedRiskScore = riskAnalyzer.CalculateRiskScore(maxMatches, apiModel.Channel, destinationScore, apiModel.Action);
            
            return new Incident
            {
                Id = apiModel.Id,
                UserEmail = apiModel.User ?? "unknown",
                Department = apiModel.Department,
                Severity = apiModel.Severity,
                DataType = apiModel.DataType,
                Timestamp = apiModel.Timestamp,
                Policy = apiModel.Policy,
                Channel = apiModel.Channel,
                Action = apiModel.Action,
                Destination = apiModel.Destination,
                FileName = apiModel.FileName,
                LoginName = apiModel.LoginName,
                EmailAddress = apiModel.EmailAddress,
                // Extract FullName from Manager
                FullName = !string.IsNullOrEmpty(apiModel.Source?.Manager) 
                          ? apiModel.Source.Manager.Split('/')[0].Trim() 
                          : null,
                MaxMatches = maxMatches,
                ViolationTriggers = apiModel.ViolationTriggers != null 
                    ? JsonConvert.SerializeObject(apiModel.ViolationTriggers) 
                    : null,
                RiskScore = calculatedRiskScore
            };
    }
}

// Models needed for deserialization (simplified copies from Collector)
public class DLPIncidentResponse { public List<DLPIncident> Incidents { get; set; } }
public class DLPIncident
{
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("severity")] public string SeverityString { get; set; }
    public int Severity => SeverityString?.ToUpper() switch { "LOW"=>1, "MEDIUM"=>2, "HIGH"=>3, "CRITICAL"=>4, _=>0 };
    [JsonProperty("source")] public DLPIncidentSource Source { get; set; }
    public string User => Source?.LoginName;
    public string Department => Source?.Department;
    [JsonProperty("incident_time")] public string IncidentTimeString { get; set; }
    public DateTime Timestamp => DateTime.TryParse(IncidentTimeString, out var dt) ? dt : DateTime.UtcNow;
    [JsonProperty("policies")] public string Policy { get; set; }
    [JsonProperty("channel")] public string Channel { get; set; }
    [JsonProperty("data_type")] public string DataType { get; set; }
    [JsonProperty("action")] public string Action { get; set; }
    [JsonProperty("destination")] public string Destination { get; set; }
    [JsonProperty("file_name")] public string FileName { get; set; }
    [JsonProperty("violation_triggers")] public List<DLPViolationTrigger> ViolationTriggers { get; set; }
    public string LoginName => Source?.LoginName;
    public string EmailAddress => Source?.BusinessUnit?.Contains("@") == true ? Source.BusinessUnit : null;
}
public class DLPIncidentSource
{
    [JsonProperty("manager")] public string Manager { get; set; }
    [JsonProperty("department")] public string Department { get; set; }
    [JsonProperty("login_name")] public string LoginName { get; set; }
    [JsonProperty("business_unit")] public string BusinessUnit { get; set; }
}
public class DLPViolationTrigger
{
    [JsonProperty("policy_name")] public string PolicyName { get; set; }
    [JsonProperty("rule_name")] public string RuleName { get; set; }
    [JsonProperty("classifiers")] public List<DLPClassifier> Classifiers { get; set; }
}
public class DLPClassifier
{
    [JsonProperty("classifier_name")] public string ClassifierName { get; set; }
    [JsonProperty("number_matches")] public int NumberMatches { get; set; }
}
