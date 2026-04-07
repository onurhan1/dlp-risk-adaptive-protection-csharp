using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DLP.RiskAnalyzer.Analyzer.Data;

namespace DLP.RiskAnalyzer.Analyzer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatBotController : ControllerBase
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ChatBotController> _logger;
    private readonly AnalyzerDbContext _db;

    public ChatBotController(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<ChatBotController> logger,
        AnalyzerDbContext db)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
        _db = db;
    }

    [HttpPost("chat")]
    public async Task<IActionResult> Chat([FromBody] ChatRequest request)
    {
        if (request?.Messages == null || request.Messages.Count == 0)
            return BadRequest("Messages list cannot be empty.");

        try
        {
            var reply = await RunAgentLoopAsync(request.Messages);
            return Ok(new { reply });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ChatBot agent döngüsünde hata");
            return StatusCode(500, new { error = "Bir hata oluştu.", message = ex.Message });
        }
    }

    // ── Agent döngüsü: LLM + tool calling ─────────────────────────────────────

    private async Task<string> RunAgentLoopAsync(List<ChatMessage> userHistory)
    {
        var subscriptionKey  = _configuration["AzureOpenAI:SubscriptionKey"] ?? "";
        var apiVersion       = _configuration["AzureOpenAI:ApiVersion"]      ?? "2024-12-01-preview";
        var deployment       = _configuration["AzureOpenAI:Deployment"]      ?? "gpt-5.1-ptu";
        var configEndpoint   = _configuration["AzureOpenAI:Endpoint"]        ?? "";

        // Tam URL'den base URL'yi çıkar (APIM proxy uyumlu)
        string requestUrl;
        if (configEndpoint.Contains("/deployments/"))
        {
            // Örn: https://apim-ai.azure-api.net/deneme/openai/deployments/gpt-5.1-ptu/chat/completions
            requestUrl = $"{configEndpoint}?api-version={apiVersion}";
        }
        else
        {
            requestUrl = $"{configEndpoint.TrimEnd('/')}/openai/deployments/{deployment}/chat/completions?api-version={apiVersion}";
        }

        // Mesaj geçmişini oluştur
        var messages = new List<object>
        {
            new
            {
                role = "system",
                content = """
                Sen Radarix adında profesyonel bir DLP (Data Loss Prevention) güvenlik analisti asistanısın.
                Şirketteki veri kaybı olaylarını analiz etmek ve güvenlik ekibine içgörü sağlamak için
                çeşitli araçlara erişimin var.
                - Kullanıcılar Türkçe veya İngilizce sorabilir, aynı dilde yanıtla.
                - Sayısal verileri tablo veya madde formatında sun.
                - Gerektiğinde tool çağır, veriyi al, sonra analiz et.
                - Kısa ve özlü yanıtlar ver; gereksiz uzatma.
                - Veriler kuruma ait gizli veriler olduğundan dışarıya sızdırma.
                """
            }
        };

        foreach (var m in userHistory)
            messages.Add(new { role = m.Role, content = m.Content });

        // Maksimum 8 tur (sonsuz döngü önlemi)
        for (int round = 0; round < 8; round++)
        {
            var payload = new
            {
                model = deployment,
                messages,
                max_completion_tokens = 4096,
                tools = ToolDefinitions,
                tool_choice = "auto"
            };

            using var req = new HttpRequestMessage(HttpMethod.Post, requestUrl);
            req.Headers.Add("api-key", subscriptionKey);
            req.Content = JsonContent.Create(payload);

            var resp = await _httpClient.SendAsync(req);
            if (!resp.IsSuccessStatusCode)
            {
                var err = await resp.Content.ReadAsStringAsync();
                _logger.LogError("Azure OpenAI hata {Status}: {Body}", resp.StatusCode, err);
                return $"Azure OpenAI API hatası: {resp.StatusCode}";
            }

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            var choice = doc.RootElement.GetProperty("choices")[0];
            var message = choice.GetProperty("message");
            var finishReason = choice.GetProperty("finish_reason").GetString();

            // Tool çağrısı yok → son yanıt
            if (finishReason != "tool_calls")
                return message.GetProperty("content").GetString() ?? "";

            // Tool çağrıları var — önce mesajı history'e ekle
            var assistantMsgJson = message.GetRawText();
            messages.Add(JsonSerializer.Deserialize<object>(assistantMsgJson)!);

            // Her tool'u çalıştır
            if (!message.TryGetProperty("tool_calls", out var toolCalls))
                break;

            foreach (var tc in toolCalls.EnumerateArray())
            {
                var toolCallId = tc.GetProperty("id").GetString()!;
                var funcName   = tc.GetProperty("function").GetProperty("name").GetString()!;
                var argsStr    = tc.GetProperty("function").GetProperty("arguments").GetString() ?? "{}";

                _logger.LogInformation("Tool çağrısı: {Name} args={Args}", funcName, argsStr);

                string toolResult;
                try
                {
                    using var argsDoc = JsonDocument.Parse(argsStr);
                    toolResult = await ExecuteToolAsync(funcName, argsDoc.RootElement);
                }
                catch (Exception ex)
                {
                    toolResult = JsonSerializer.Serialize(new { error = ex.Message });
                    _logger.LogWarning(ex, "Tool '{Name}' çalıştırılırken hata", funcName);
                }

                messages.Add(new
                {
                    role = "tool",
                    tool_call_id = toolCallId,
                    content = toolResult
                });
            }
        }

        return "Analizi tamamlayamadım. Lütfen soruyu farklı şekilde sormayı deneyin.";
    }

    // ── Tool executor ─────────────────────────────────────────────────────────

    private async Task<string> ExecuteToolAsync(string toolName, JsonElement args)
    {
        var today     = DateOnly.FromDateTime(DateTime.UtcNow);
        var startDate = GetDate(args, "start_date") ?? today.AddDays(-30);
        var endDate   = GetDate(args, "end_date")   ?? today;

        return toolName switch
        {
            "get_action_summary"   => await GetActionSummaryAsync(startDate, endDate),
            "get_high_risk_users"  => await GetHighRiskUsersAsync(GetDate(args, "date") ?? today),
            "get_department_summary" => await GetDepartmentSummaryAsync(startDate, endDate),
            "get_daily_summary"    => await GetDailySummaryAsync(GetInt(args, "days", 7)),
            "get_incidents"        => await GetIncidentsAsync(args, startDate, endDate),
            "get_channel_activity" => await GetChannelActivityAsync(startDate, endDate),
            "get_top_risky_users"  => await GetTopRiskyUsersAsync(GetDate(args, "date") ?? today, GetInt(args, "top_n", 10)),
            "get_user_risk_trend"  => await GetUserRiskTrendAsync(GetStr(args, "user_email"), startDate, endDate),
            "get_anomaly_detections" => await GetAnomalyDetectionsAsync(GetStr(args, "user_email"), startDate, endDate),
            "get_mercek_stats"     => await GetMercekStatsAsync(),
            _ => JsonSerializer.Serialize(new { error = $"Bilinmeyen tool: {toolName}" })
        };
    }

    // ── Tool implementasyonları (doğrudan DB sorgusu) ─────────────────────────

    private async Task<string> GetActionSummaryAsync(DateOnly start, DateOnly end)
    {
        var s = start.ToDateTime(TimeOnly.MinValue);
        var e = end.ToDateTime(TimeOnly.MaxValue);

        var summary = await _db.Incidents
            .Where(i => i.Timestamp >= s && i.Timestamp <= e)
            .GroupBy(i => i.Action ?? "UNKNOWN")
            .Select(g => new { action = g.Key, count = g.Count() })
            .ToListAsync();

        var total = summary.Sum(x => x.count);
        return JsonSerializer.Serialize(new
        {
            start_date  = start.ToString("yyyy-MM-dd"),
            end_date    = end.ToString("yyyy-MM-dd"),
            total,
            by_action   = summary
        });
    }

    private async Task<string> GetHighRiskUsersAsync(DateOnly date)
    {
        var dayStart = date.ToDateTime(TimeOnly.MinValue);
        var dayEnd   = date.ToDateTime(TimeOnly.MaxValue);

        var users = await _db.Incidents
            .Where(i => i.Timestamp >= dayStart && i.Timestamp <= dayEnd && (i.RiskScore ?? 0) >= 50)
            .GroupBy(i => new { i.UserEmail, i.Department, i.FullName })
            .Select(g => new
            {
                user_email = g.Key.UserEmail,
                full_name  = g.Key.FullName,
                department = g.Key.Department,
                max_risk_score = g.Max(x => x.RiskScore ?? 0),
                incident_count = g.Count()
            })
            .OrderByDescending(x => x.max_risk_score)
            .Take(20)
            .ToListAsync();

        return JsonSerializer.Serialize(new { date = date.ToString("yyyy-MM-dd"), high_risk_users = users, count = users.Count });
    }

    private async Task<string> GetDepartmentSummaryAsync(DateOnly start, DateOnly end)
    {
        var s = start.ToDateTime(TimeOnly.MinValue);
        var e = end.ToDateTime(TimeOnly.MaxValue);

        var data = await _db.Incidents
            .Where(i => i.Timestamp >= s && i.Timestamp <= e)
            .GroupBy(i => i.Department ?? "Unknown")
            .Select(g => new
            {
                department     = g.Key,
                total_incidents = g.Count(),
                high_risk_count = g.Count(i => (i.RiskScore ?? 0) >= 50),
                avg_risk_score = g.Average(i => (double)(i.RiskScore ?? 0)),
                unique_users   = g.Select(i => i.UserEmail).Distinct().Count()
            })
            .OrderByDescending(x => x.total_incidents)
            .ToListAsync();

        return JsonSerializer.Serialize(new { start_date = start.ToString("yyyy-MM-dd"), end_date = end.ToString("yyyy-MM-dd"), departments = data });
    }

    private async Task<string> GetDailySummaryAsync(int days)
    {
        var end   = DateOnly.FromDateTime(DateTime.UtcNow);
        var start = end.AddDays(-days);

        var data = await _db.Incidents
            .Where(i => i.Timestamp >= start.ToDateTime(TimeOnly.MinValue) &&
                        i.Timestamp <= end.ToDateTime(TimeOnly.MaxValue))
            .GroupBy(i => DateOnly.FromDateTime(i.Timestamp))
            .Select(g => new
            {
                date            = g.Key,
                total_incidents = g.Count(),
                high_risk_count = g.Count(i => (i.RiskScore ?? 0) >= 50),
                avg_risk_score  = g.Average(i => (double)(i.RiskScore ?? 0)),
                unique_users    = g.Select(i => i.UserEmail).Distinct().Count()
            })
            .OrderBy(x => x.date)
            .ToListAsync();

        return JsonSerializer.Serialize(new { days, daily_summaries = data });
    }

    private async Task<string> GetIncidentsAsync(JsonElement args, DateOnly start, DateOnly end)
    {
        var action   = GetStr(args, "action");
        var userEmail = GetStr(args, "user_email");
        var pageSize = GetInt(args, "page_size", 20);
        var page     = GetInt(args, "page", 1);

        var s = start.ToDateTime(TimeOnly.MinValue);
        var e = end.ToDateTime(TimeOnly.MaxValue);

        var query = _db.Incidents.Where(i => i.Timestamp >= s && i.Timestamp <= e);

        if (!string.IsNullOrWhiteSpace(action))
            query = query.Where(i => i.Action == action.ToUpper());

        if (!string.IsNullOrWhiteSpace(userEmail))
            query = query.Where(i => i.UserEmail != null && i.UserEmail.Contains(userEmail));

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(i => i.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(i => new
            {
                user_email  = i.UserEmail,
                full_name   = i.FullName,
                department  = i.Department,
                action      = i.Action,
                risk_score  = i.RiskScore,
                channel     = i.Channel,
                policy      = i.Policy,
                rule_name   = i.RuleName,
                timestamp   = i.Timestamp,
                destination = i.Destination
            })
            .ToListAsync();

        return JsonSerializer.Serialize(new { total, page, page_size = pageSize, items });
    }

    private async Task<string> GetChannelActivityAsync(DateOnly start, DateOnly end)
    {
        var s = start.ToDateTime(TimeOnly.MinValue);
        var e = end.ToDateTime(TimeOnly.MaxValue);

        var data = await _db.Incidents
            .Where(i => i.Timestamp >= s && i.Timestamp <= e)
            .GroupBy(i => i.Channel ?? "Unknown")
            .Select(g => new
            {
                channel         = g.Key,
                total_incidents = g.Count(),
                high_risk_count = g.Count(i => (i.RiskScore ?? 0) >= 50),
                blocked_count   = g.Count(i => i.Action == "BLOCK"),
                unique_users    = g.Select(i => i.UserEmail).Distinct().Count()
            })
            .OrderByDescending(x => x.total_incidents)
            .ToListAsync();

        return JsonSerializer.Serialize(new { start_date = start.ToString("yyyy-MM-dd"), end_date = end.ToString("yyyy-MM-dd"), channels = data });
    }

    private async Task<string> GetTopRiskyUsersAsync(DateOnly date, int topN)
    {
        var s = date.AddDays(-6).ToDateTime(TimeOnly.MinValue);
        var e = date.ToDateTime(TimeOnly.MaxValue);

        var data = await _db.Incidents
            .Where(i => i.Timestamp >= s && i.Timestamp <= e)
            .GroupBy(i => new { i.UserEmail, i.FullName, i.Department })
            .Select(g => new
            {
                user_email     = g.Key.UserEmail,
                full_name      = g.Key.FullName,
                department     = g.Key.Department,
                max_risk_score = g.Max(x => x.RiskScore ?? 0),
                total_incidents = g.Count(),
                blocked_count  = g.Count(i => i.Action == "BLOCK")
            })
            .OrderByDescending(x => x.max_risk_score)
            .Take(topN)
            .ToListAsync();

        return JsonSerializer.Serialize(new
        {
            as_of_date = date.ToString("yyyy-MM-dd"),
            top_n      = topN,
            users      = data
        });
    }

    private async Task<string> GetUserRiskTrendAsync(string? userEmail, DateOnly start, DateOnly end)
    {
        if (string.IsNullOrWhiteSpace(userEmail))
            return JsonSerializer.Serialize(new { error = "user_email parametresi gerekli." });

        var s = start.ToDateTime(TimeOnly.MinValue);
        var e = end.ToDateTime(TimeOnly.MaxValue);

        var data = await _db.Incidents
            .Where(i => i.UserEmail != null &&
                        i.UserEmail.Contains(userEmail) &&
                        i.Timestamp >= s && i.Timestamp <= e)
            .GroupBy(i => DateOnly.FromDateTime(i.Timestamp))
            .Select(g => new
            {
                date           = g.Key,
                incident_count = g.Count(),
                max_risk_score = g.Max(x => x.RiskScore ?? 0),
                actions        = g.GroupBy(i => i.Action).Select(ag => new { action = ag.Key, count = ag.Count() })
            })
            .OrderBy(x => x.date)
            .ToListAsync();

        return JsonSerializer.Serialize(new { user_email = userEmail, trend = data });
    }

    private async Task<string> GetAnomalyDetectionsAsync(string? userEmail, DateOnly start, DateOnly end)
    {
        var query = _db.AnomalyDetections
            .Where(a => a.Timestamp >= start.ToDateTime(TimeOnly.MinValue) &&
                        a.Timestamp <= end.ToDateTime(TimeOnly.MaxValue));

        if (!string.IsNullOrWhiteSpace(userEmail))
            query = query.Where(a => a.UserEmail != null && a.UserEmail.Contains(userEmail));

        var data = await query
            .OrderByDescending(a => a.Timestamp)
            .Take(50)
            .Select(a => new
            {
                user_email  = a.UserEmail,
                metric      = a.MetricType,
                value       = a.CurrentValue,
                mean        = a.BaselineMean,
                score       = a.AnomalyScore,
                severity    = a.Severity,
                detected_at = a.Timestamp
            })
            .ToListAsync();

        return JsonSerializer.Serialize(new { anomalies = data, count = data.Count });
    }

    private async Task<string> GetMercekStatsAsync()
    {
        var total   = await _db.MercekIncidents.CountAsync();
        var byUser  = await _db.MercekIncidents
            .GroupBy(m => m.UserName)
            .Select(g => new { user = g.Key, count = g.Count() })
            .OrderByDescending(x => x.count)
            .Take(10)
            .ToListAsync();

        return JsonSerializer.Serialize(new { total_mercek_incidents = total, top_users = byUser });
    }

    // ── Tool tanımları (OpenAI function calling formatı) ──────────────────────

    private static readonly object[] ToolDefinitions =
    [
        FuncTool("get_action_summary",
            "DLP olaylarının eylem özetini getirir: BLOCK, QUARANTINE, AUTHORIZED sayıları.",
            new
            {
                type = "object",
                properties = new
                {
                    start_date = new { type = "string", description = "Başlangıç tarihi YYYY-MM-DD (opsiyonel)" },
                    end_date   = new { type = "string", description = "Bitiş tarihi YYYY-MM-DD (opsiyonel)" }
                }
            }),

        FuncTool("get_high_risk_users",
            "Belirtilen tarihteki yüksek riskli kullanıcıları listeler (risk skoru >= 50).",
            new
            {
                type = "object",
                properties = new
                {
                    date = new { type = "string", description = "Tarih YYYY-MM-DD (opsiyonel, varsayılan: bugün)" }
                }
            }),

        FuncTool("get_department_summary",
            "Departman bazlı risk özetini getirir.",
            new
            {
                type = "object",
                properties = new
                {
                    start_date = new { type = "string", description = "Başlangıç tarihi YYYY-MM-DD" },
                    end_date   = new { type = "string", description = "Bitiş tarihi YYYY-MM-DD" }
                }
            }),

        FuncTool("get_daily_summary",
            "Günlük risk özetini getirir. Her gün için incident sayısı, yüksek riskli sayısı ve ortalama skor.",
            new
            {
                type = "object",
                properties = new
                {
                    days = new { type = "integer", description = "Son kaç gün (varsayılan: 7)" }
                }
            }),

        FuncTool("get_incidents",
            "DLP olaylarını listeler. Eylem ve kullanıcı filtreleri desteklenir.",
            new
            {
                type = "object",
                properties = new
                {
                    action      = new { type = "string", description = "Eylem filtresi: BLOCK | QUARANTINE | AUTHORIZED" },
                    user_email  = new { type = "string", description = "Kullanıcı e-posta filtresi (kısmi eşleşme)" },
                    start_date  = new { type = "string", description = "Başlangıç tarihi YYYY-MM-DD" },
                    end_date    = new { type = "string", description = "Bitiş tarihi YYYY-MM-DD" },
                    page        = new { type = "integer", description = "Sayfa no (varsayılan: 1)" },
                    page_size   = new { type = "integer", description = "Sayfa boyutu max 50 (varsayılan: 20)" }
                }
            }),

        FuncTool("get_channel_activity",
            "Kanal bazlı DLP aktivitesini getirir: Email, Cloud, USB, Print vb.",
            new
            {
                type = "object",
                properties = new
                {
                    start_date = new { type = "string", description = "Başlangıç tarihi YYYY-MM-DD" },
                    end_date   = new { type = "string", description = "Bitiş tarihi YYYY-MM-DD" }
                }
            }),

        FuncTool("get_top_risky_users",
            "En çok risk oluşturan kullanıcıları listeler (son 7 gün içinde).",
            new
            {
                type = "object",
                properties = new
                {
                    date  = new { type = "string",  description = "Bitiş tarihi YYYY-MM-DD (varsayılan: bugün)" },
                    top_n = new { type = "integer", description = "Kaç kullanıcı (varsayılan: 10)" }
                }
            }),

        FuncTool("get_user_risk_trend",
            "Belirli bir kullanıcının risk trendini getirir.",
            new
            {
                type = "object",
                properties = new
                {
                    user_email = new { type = "string", description = "Kullanıcı e-posta adresi (zorunlu)" },
                    start_date = new { type = "string", description = "Başlangıç tarihi YYYY-MM-DD" },
                    end_date   = new { type = "string", description = "Bitiş tarihi YYYY-MM-DD" }
                },
                required = new[] { "user_email" }
            }),

        FuncTool("get_anomaly_detections",
            "Anomali tespitlerini listeler.",
            new
            {
                type = "object",
                properties = new
                {
                    user_email = new { type = "string", description = "Kullanıcı filtresi (opsiyonel)" },
                    start_date = new { type = "string", description = "Başlangıç tarihi YYYY-MM-DD" },
                    end_date   = new { type = "string", description = "Bitiş tarihi YYYY-MM-DD" }
                }
            }),

        FuncTool("get_mercek_stats",
            "Mercek gelişmiş olay analizi istatistiklerini getirir.",
            new
            {
                type = "object",
                properties = new { }
            }),
    ];

    private static object FuncTool(string name, string description, object parameters) =>
        new
        {
            type = "function",
            function = new { name, description, parameters }
        };

    // ── Parametre yardımcıları ────────────────────────────────────────────────

    private static DateOnly? GetDate(JsonElement el, string key)
    {
        if (el.ValueKind == JsonValueKind.Object &&
            el.TryGetProperty(key, out var val) &&
            val.ValueKind == JsonValueKind.String &&
            DateOnly.TryParse(val.GetString(), out var d))
            return d;
        return null;
    }

    private static int GetInt(JsonElement el, string key, int defaultVal)
    {
        if (el.ValueKind == JsonValueKind.Object &&
            el.TryGetProperty(key, out var val))
        {
            if (val.ValueKind == JsonValueKind.Number && val.TryGetInt32(out int n)) return n;
            if (val.ValueKind == JsonValueKind.String && int.TryParse(val.GetString(), out int s)) return s;
        }
        return defaultVal;
    }

    private static string? GetStr(JsonElement el, string key)
    {
        if (el.ValueKind == JsonValueKind.Object &&
            el.TryGetProperty(key, out var val) &&
            val.ValueKind == JsonValueKind.String)
            return val.GetString();
        return null;
    }
}

// ── Request modelleri ─────────────────────────────────────────────────────────

public class ChatRequest
{
    public List<ChatMessage> Messages { get; set; } = [];
}

public class ChatMessage
{
    public string Role    { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}
