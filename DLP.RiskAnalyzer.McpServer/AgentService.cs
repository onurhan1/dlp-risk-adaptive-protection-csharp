using Azure.AI.OpenAI;
using OpenAI.Chat;
using System.ClientModel;
using System.Text.Json;

namespace DLP.RiskAnalyzer.McpServer;

/// <summary>
/// Conversation session'larını tutar ve LLM + tool calling döngüsünü yönetir.
/// </summary>
public class AgentService
{
    private readonly ToolRegistry _tools;
    private readonly ChatClient _chat;
    private readonly string _systemPrompt;
    private readonly ILogger<AgentService> _logger;

    // Session bazlı konuşma geçmişi (production'da Redis/DB kullanılabilir)
    private readonly Dictionary<string, List<ChatMessage>> _sessions = new();
    private readonly SemaphoreSlim _sessionsLock = new(1, 1);

    public AgentService(
        ToolRegistry tools,
        IConfiguration config,
        ILogger<AgentService> logger)
    {
        _tools = tools;
        _logger = logger;
        _systemPrompt = config["LLM:SystemPrompt"] ?? "Sen bir DLP güvenlik analisti asistanısın.";

        var endpoint  = config["LLM:Endpoint"]        ?? throw new InvalidOperationException("LLM:Endpoint ayarlanmamış.");
        var apiKey    = config["LLM:ApiKey"]          ?? throw new InvalidOperationException("LLM:ApiKey ayarlanmamış.");
        var deploy    = config["LLM:DeploymentName"]  ?? "gpt-4o";

        var azureClient = new AzureOpenAIClient(new Uri(endpoint), new ApiKeyCredential(apiKey));
        _chat = azureClient.GetChatClient(deploy);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Mesajı gönderir ve yanıtı SSE stream olarak yazar.
    /// </summary>
    public async Task StreamAsync(string sessionId, string userMessage, HttpResponse response)
    {
        response.ContentType = "text/event-stream";
        response.Headers.CacheControl = "no-cache";
        response.Headers.Connection   = "keep-alive";

        var history = await GetOrCreateSessionAsync(sessionId);
        history.Add(new UserChatMessage(userMessage));

        try
        {
            var fullReply = await RunAgentLoopAsync(history, response);
            history.Add(new AssistantChatMessage(fullReply));
            await SaveSessionAsync(sessionId, history);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Agent döngüsünde hata oluştu. Session: {Session}", sessionId);
            await WriteSseAsync(response, $"\n\n❌ Hata: {ex.Message}");
        }
        finally
        {
            await WriteSseAsync(response, "[DONE]", eventName: "done");
        }
    }

    public async Task ClearSessionAsync(string sessionId)
    {
        await _sessionsLock.WaitAsync();
        try { _sessions.Remove(sessionId); }
        finally { _sessionsLock.Release(); }
    }

    // ── Agent döngüsü ─────────────────────────────────────────────────────────

    private async Task<string> RunAgentLoopAsync(List<ChatMessage> history, HttpResponse response)
    {
        var options = new ChatCompletionOptions
        {
            Temperature = 0.3f,
            MaxOutputTokenCount = 4096,
        };
        foreach (var tool in _tools.GetChatTools())
            options.Tools.Add(tool);

        var fullReply = new System.Text.StringBuilder();

        // Maksimum 10 tool çağrısı turu (sonsuz döngü önlemi)
        for (int round = 0; round < 10; round++)
        {
            var messages = BuildMessages(history);
            var streaming = _chat.CompleteChatStreamingAsync(messages, options);

            var toolCalls = new List<StreamingChatToolCallUpdate>();
            string? finishReason = null;

            await foreach (var chunk in streaming)
            {
                finishReason = chunk.FinishReason?.ToString();

                foreach (var delta in chunk.ContentUpdate)
                {
                    if (!string.IsNullOrEmpty(delta.Text))
                    {
                        fullReply.Append(delta.Text);
                        await WriteSseAsync(response, delta.Text);
                    }
                }

                foreach (var tc in chunk.ToolCallUpdates)
                    toolCalls.Add(tc);
            }

            // Tool çağrısı yoksa bitti
            if (finishReason != "tool_calls" || toolCalls.Count == 0)
                break;

            // Tool'ları grupla (aynı index → aynı çağrı)
            var grouped = toolCalls
                .GroupBy(tc => tc.Index)
                .Select(g => new
                {
                    Id   = g.First().ToolCallId,
                    Name = g.Select(x => x.FunctionName).FirstOrDefault(x => x != null) ?? "",
                    Args = string.Concat(g.Select(x => x.FunctionArgumentsUpdate?.ToString() ?? ""))
                })
                .ToList();

            // Assistant mesajı olarak ekle
            var assistantToolMsg = new AssistantChatMessage(grouped.Select(g =>
                ChatToolCall.CreateFunctionToolCall(g.Id, g.Name, BinaryData.FromString(g.Args))
            ).ToList());
            history.Add(assistantToolMsg);

            // Her tool'u çalıştır
            foreach (var call in grouped)
            {
                await WriteSseAsync(response, $"\n\n🔧 `{call.Name}` çalıştırılıyor...\n", eventName: "tool");
                _logger.LogInformation("Tool çağrısı: {Name} args={Args}", call.Name, call.Args);

                string toolResult;
                try
                {
                    var argsEl = string.IsNullOrWhiteSpace(call.Args)
                        ? JsonDocument.Parse("{}").RootElement
                        : JsonDocument.Parse(call.Args).RootElement;

                    toolResult = await _tools.InvokeAsync(call.Name, argsEl);
                }
                catch (Exception ex)
                {
                    toolResult = $"{{\"error\": \"{ex.Message}\"}}";
                    _logger.LogWarning(ex, "Tool '{Name}' çalıştırılırken hata", call.Name);
                }

                history.Add(new ToolChatMessage(call.Id, toolResult));
            }
        }

        return fullReply.ToString();
    }

    // ── Session yönetimi ──────────────────────────────────────────────────────

    private async Task<List<ChatMessage>> GetOrCreateSessionAsync(string sessionId)
    {
        await _sessionsLock.WaitAsync();
        try
        {
            if (!_sessions.TryGetValue(sessionId, out var hist))
            {
                hist = [];
                _sessions[sessionId] = hist;
            }
            return hist;
        }
        finally { _sessionsLock.Release(); }
    }

    private async Task SaveSessionAsync(string sessionId, List<ChatMessage> history)
    {
        // Konuşma çok uzarsa eski mesajları kırp (sistem mesajı + son 40 mesaj)
        const int maxMessages = 40;
        if (history.Count > maxMessages)
            history.RemoveRange(0, history.Count - maxMessages);

        await _sessionsLock.WaitAsync();
        try { _sessions[sessionId] = history; }
        finally { _sessionsLock.Release(); }
    }

    private List<ChatMessage> BuildMessages(List<ChatMessage> history)
    {
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(_systemPrompt)
        };
        messages.AddRange(history);
        return messages;
    }

    // ── SSE yardımcısı ────────────────────────────────────────────────────────

    private static async Task WriteSseAsync(HttpResponse res, string data, string? eventName = null)
    {
        if (eventName != null)
            await res.WriteAsync($"event: {eventName}\n");

        // Çok satırlı veriyi düzgün encode et
        foreach (var line in data.Split('\n'))
            await res.WriteAsync($"data: {line}\n");

        await res.WriteAsync("\n");
        await res.Body.FlushAsync();
    }
}
