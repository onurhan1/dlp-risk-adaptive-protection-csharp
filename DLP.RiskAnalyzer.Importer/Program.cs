using System.Net.Http.Headers;
using System.Text;
using DLP.RiskAnalyzer.Analyzer.Data;
using DLP.RiskAnalyzer.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System.Globalization;

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

                    // Debug: Log Manager field for first incident
                    if (incidents[0].Source != null)
                    {
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.WriteLine($"  [DEBUG] Manager: '{incidents[0].Source.Manager ?? "NULL"}'");
                        Console.ResetColor();
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.WriteLine("  [DEBUG] Source field is NULL (Manager cannot be extracted)");
                        Console.ResetColor();
                    }

                    // Insert to DB
                    using var context = new AnalyzerDbContext(dbOptions);
                    
                    // Convert to DB Model
                    var dbIncidents = incidents.Select(i => MapToIncident(i)).ToList();

                    // UPSERT Logic: Update existing, Insert new
                    var ids = dbIncidents.Select(x => x.Id).ToList();
                    var timestamps = dbIncidents.Select(x => x.Timestamp).ToList();
                    
                    // Find existing incidents (composite key: Id + Timestamp)
                    var existingIncidents = await context.Incidents
                        .Where(x => ids.Contains(x.Id))
                        .ToListAsync();

                    int insertedCount = 0;
                    int updatedCount = 0;

                    foreach (var incident in dbIncidents)
                    {
                        var existing = existingIncidents.FirstOrDefault(e => e.Id == incident.Id && e.Timestamp == incident.Timestamp);
                        
                        if (existing != null)
                        {
                            // UPDATE existing record
                            existing.UserEmail = incident.UserEmail;
                            existing.Department = incident.Department;
                            existing.Severity = incident.Severity;
                            existing.DataType = incident.DataType;
                            existing.Policy = incident.Policy;
                            existing.RuleName = incident.RuleName;
                            existing.Channel = incident.Channel;
                            existing.Action = incident.Action;
                            existing.Destination = incident.Destination;
                            existing.FileName = incident.FileName;
                            existing.LoginName = incident.LoginName;
                            existing.EmailAddress = incident.EmailAddress;
                            existing.FullName = incident.FullName;
                            existing.Team = incident.Team;
                            existing.MaxMatches = incident.MaxMatches;
                            existing.ViolationTriggers = incident.ViolationTriggers;
                            existing.RiskScore = incident.RiskScore;
                            updatedCount++;
                        }
                        else
                        {
                            // INSERT new record
                            await context.Incidents.AddAsync(incident);
                            insertedCount++;
                        }
                    }

                    await context.SaveChangesAsync();
                    
                    if (insertedCount > 0 || updatedCount > 0)
                    {
                        Console.Write($"Inserted {insertedCount}, Updated {updatedCount}. ");
                        totalIncidentsImported += insertedCount;
                    }
                    else
                    {
                        Console.Write("No changes. ");
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
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"  Inner: {ex.InnerException.Message}");
                }
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
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[ERROR] Deserialization failed (Primary): {ex.Message}");
            Console.ResetColor();

            try
            {
                return JsonConvert.DeserializeObject<List<DLPIncident>>(content) ?? new List<DLPIncident>();
            }
            catch (Exception ex2)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[ERROR] Deserialization failed (Fallback): {ex2.Message}");
                Console.ResetColor();
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

            var calculatedRiskScore = riskAnalyzer.CalculateRiskScoreV2(maxMatches, apiModel.Channel, destinationScore, apiModel.Action);
            
            // Helper to truncate strings safely
            static string? Truncate(string? value, int maxLength = 500) =>
                string.IsNullOrEmpty(value) ? value : (value.Length <= maxLength ? value : value.Substring(0, maxLength));
                
            // Debug: Check ViolationTriggers before serialization
            if (apiModel.ViolationTriggers != null && apiModel.ViolationTriggers.Count > 0)
            {
                var vt = apiModel.ViolationTriggers[0];
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine($"  [DEBUG-MAP] VT Count: {apiModel.ViolationTriggers.Count}");
                Console.WriteLine($"  [DEBUG-MAP] VT[0] PolicyName: {vt.PolicyName}");
                Console.WriteLine($"  [DEBUG-MAP] VT[0] RuleName: {vt.RuleName}");
                if (string.IsNullOrEmpty(vt.RuleName))
                {
                     Console.WriteLine($"  [DEBUG-MAP] VT[0] Raw Properties: Snake={vt.RuleNameSnake}, Camel={vt.RuleNameCamel}, Pascal={vt.RuleNamePascal}");
                }
                Console.ResetColor();
            }
            
            if (apiModel.Source == null)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"  [DEBUG-MAP] Source is NULL for IncidentId: {apiModel.Id}. FullName/Team will be null.");
                Console.ResetColor();
            }
            else if (string.IsNullOrEmpty(apiModel.Source?.Manager))
            {
                Console.WriteLine($"  [DEBUG-MAP] Source exists but Manager is empty for IncidentId: {apiModel.Id}");
            }

                // Extract FullName and Team with Fallback logic
                string fullName = null;
                string team = null;

                if (!string.IsNullOrEmpty(apiModel.Source?.Manager))
                {
                    fullName = apiModel.Source.Manager.Split('/')[0].Trim();
                    if (apiModel.Source.Manager.Contains('/'))
                    {
                        var parts = apiModel.Source.Manager.Split('/')[1];
                        team = parts.Contains('-') ? parts.Split(new[]{'-'}, 2)[1].Trim() : parts.Trim();
                    }
                }
                
                // Fallbacks
                if (string.IsNullOrEmpty(fullName))
                {
                    if (!string.IsNullOrEmpty(apiModel.Source?.LoginName))
                        fullName = apiModel.Source.LoginName.Split('\\').Last();
                    else if (!string.IsNullOrEmpty(apiModel.Source?.EmailAddress))
                        fullName = apiModel.Source.EmailAddress.Split('@')[0];
                    else if (!string.IsNullOrEmpty(apiModel.Source?.HostName))
                        fullName = apiModel.Source.HostName;
                }

                if (string.IsNullOrEmpty(team))
                {
                    if (!string.IsNullOrEmpty(apiModel.Source?.Department))
                        team = apiModel.Source.Department;
                    else if (!string.IsNullOrEmpty(apiModel.Source?.BusinessUnit))
                        team = apiModel.Source.BusinessUnit;
                }

                return new Incident
                {
                    Id = apiModel.Id,
                    UserEmail = Truncate(apiModel.User?.Split('\\').Last() ?? apiModel.Source?.EmailAddress ?? apiModel.Source?.HostName, 255) ?? "unknown",
                    Department = Truncate(apiModel.Department, 255),
                    Severity = apiModel.Severity,
                    DataType = Truncate(apiModel.DataType, 255),
                    Timestamp = apiModel.Timestamp,
                    Policy = Truncate(apiModel.Policy, 500),
                    RuleName = Truncate(string.Join("; ", apiModel.ViolationTriggers?
                        .Select(vt => vt.RuleName)
                        .Where(rn => !string.IsNullOrEmpty(rn))
                        .Distinct() ?? Array.Empty<string>()), 255),
                    Channel = Truncate(apiModel.Channel, 255),
                    Action = Truncate(apiModel.Action, 100),
                    Destination = Truncate(apiModel.Destination, 500),
                    FileName = Truncate(apiModel.FileName, 500),
                    LoginName = Truncate(apiModel.LoginName?.Split('\\').Last() ?? apiModel.Source?.HostName, 255),
                    EmailAddress = Truncate(apiModel.EmailAddress, 255),
                    
                    FullName = Truncate(fullName, 255),
                    Team = Truncate(team, 255),
                MaxMatches = maxMatches,
                MaxMatches = maxMatches,
                ViolationTriggers = apiModel.ViolationTriggers != null 
                    ? JsonConvert.SerializeObject(apiModel.ViolationTriggers.Select(vt => new 
                    {
                        policy_name = vt.PolicyName,
                        rule_name = vt.RuleName,
                        classifiers = vt.Classifiers?.Select(c => new 
                        {
                            classifier_name = c.ClassifierName,
                            number_matches = c.NumberMatches
                        }).ToList()
                    }), new JsonSerializerSettings 
                    { 
                        Formatting = Formatting.None,
                        NullValueHandling = NullValueHandling.Ignore
                    }) 
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
    public DateTime Timestamp
    {
        get
        {
            // Try to parse incident_time first
            if (!string.IsNullOrEmpty(IncidentTimeString))
            {
                // Try multiple date formats
                var formats = new[] { 
                    "dd/MM/yyyy HH:mm:ss",
                    "MM/dd/yyyy HH:mm:ss",
                    "yyyy-MM-dd HH:mm:ss",
                    "dd-MM-yyyy HH:mm:ss"
                };
                
                foreach (var format in formats)
                {
                    if (DateTime.TryParseExact(IncidentTimeString, format, CultureInfo.InvariantCulture, 
                        DateTimeStyles.None, out var incidentTime))
                    {
                        return DateTime.SpecifyKind(incidentTime, DateTimeKind.Utc);
                    }
                }
                
                // Fallback to standard parse
                if (DateTime.TryParse(IncidentTimeString, CultureInfo.InvariantCulture, 
                    DateTimeStyles.None, out var parsedTime))
                {
                    return DateTime.SpecifyKind(parsedTime, DateTimeKind.Utc);
                }
            }
            
            return DateTime.UtcNow;
        }
    }
    [JsonProperty("policies")] public string Policy { get; set; }
    [JsonProperty("channel")] public string Channel { get; set; }
    [JsonProperty("data_type")] public string DataType { get; set; }
    [JsonProperty("action")] public string Action { get; set; }
    [JsonProperty("destination")] public string Destination { get; set; }
    [JsonProperty("file_name")] public string FileName { get; set; }
    [JsonProperty("violation_triggers")] public List<DLPViolationTrigger> ViolationTriggers { get; set; }
    public string LoginName => Source?.LoginName;
    public string EmailAddress => Source?.EmailAddress ?? (Source?.BusinessUnit?.Contains("@") == true ? Source.BusinessUnit : null);
}
public class DLPIncidentSource
{
    [JsonProperty("manager")] public string Manager { get; set; }
    [JsonProperty("department")] public string Department { get; set; }
    [JsonProperty("login_name")] public string LoginName { get; set; }
    [JsonProperty("host_name")] public string HostName { get; set; }
    [JsonProperty("email_address")] public string EmailAddress { get; set; }
    [JsonProperty("dn")] public string Dn { get; set; }
    [JsonProperty("business_unit")] public string BusinessUnit { get; set; }
}
public class DLPViolationTrigger
{
    // Support multiple formats for policy_name
    [JsonProperty("policy_name")] public string PolicyNameSnake { get; set; }
    [JsonProperty("PolicyName")] public string PolicyNamePascal { get; set; }
    
    [JsonIgnore]
    public string PolicyName => PolicyNameSnake ?? PolicyNamePascal;

    // Support multiple formats for rule_name
    [JsonProperty("rule_name")] public string RuleNameSnake { get; set; }
    [JsonProperty("ruleName")] public string RuleNameCamel { get; set; }
    [JsonProperty("RuleName")] public string RuleNamePascal { get; set; }
    
    [JsonIgnore]
    public string RuleName => RuleNameSnake ?? RuleNameCamel ?? RuleNamePascal;

    [JsonProperty("classifiers")] 
    public List<DLPClassifier> Classifiers { get; set; }
}
public class DLPClassifier
{
    // Support multiple formats for classifier_name
    [JsonProperty("classifier_name")] public string ClassifierNameSnake { get; set; }
    [JsonProperty("ClassifierName")] public string ClassifierNamePascal { get; set; }
    
    [JsonIgnore]
    public string ClassifierName => ClassifierNameSnake ?? ClassifierNamePascal;

    [JsonProperty("number_matches")] public int NumberMatchesSnake { get; set; }
    [JsonProperty("NumberMatches")] public int NumberMatchesPascal { get; set; }
    [JsonProperty("numberMatches")] public int NumberMatchesCamel { get; set; }

    [JsonIgnore]
    public int NumberMatches => NumberMatchesSnake != 0 ? NumberMatchesSnake : (NumberMatchesPascal != 0 ? NumberMatchesPascal : NumberMatchesCamel);
}
