using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DLP.RiskAnalyzer.Analyzer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatBotController : ControllerBase
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ChatBotController> _logger;

    public ChatBotController(HttpClient httpClient, IConfiguration configuration, ILogger<ChatBotController> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost("chat")]
    public async Task<IActionResult> Chat([FromBody] ChatRequest request)
    {
        if (request?.Messages == null || request.Messages.Count == 0)
        {
            return BadRequest("Messages list cannot be empty.");
        }

        try
        {
            var subscriptionKey = _configuration["AzureOpenAI:SubscriptionKey"] ?? "<your-subscription-key>";
            var apiVersion = _configuration["AzureOpenAI:ApiVersion"] ?? "2024-12-01-preview";
            var modelName = _configuration["AzureOpenAI:ModelName"] ?? "gpt-5.1";
            var deployment = _configuration["AzureOpenAI:Deployment"] ?? "gpt-5.1-ptu";
            
            // Eğer config'te tam URL tanımlanmışsa onu kullan, yoksa birleştir
            var endpoint = _configuration["AzureOpenAI:Endpoint"];
            if (string.IsNullOrEmpty(endpoint)) 
            {
                endpoint = $"https://apim-ai-management.azure-api.net/deneme/openai/deployments/{deployment}/chat/completions";
            }
            
            var requestUrl = $"{endpoint}?api-version={apiVersion}";

            // Frontend'den gelen mesajların önüne bir system mesajı ekle (eğer yoksa)
            var formattedMessages = new List<object>
            {
                new { role = "system", content = "Sen profesyonel bir Siber Güvenlik (DLP) analiz ve raporlama asistanısın. Kısa, öz ve net cevaplar verirsin. Olayları ve DLP ihlallerini profesyonel bir dille açıklarsın." }
            };

            // Kullanıcıdan gelen sohbet geçmişini (role ve content yapısında) ekle
            foreach(var msg in request.Messages) 
            {
                formattedMessages.Add(new { role = msg.Role, content = msg.Content });
            }

            var payload = new
            {
                messages = formattedMessages,
                max_completion_tokens = 4096,
                model = deployment
            };

            var requestMessage = new HttpRequestMessage(HttpMethod.Post, requestUrl);
            requestMessage.Headers.Add("api-key", subscriptionKey);
            requestMessage.Content = JsonContent.Create(payload);

            var response = await _httpClient.SendAsync(requestMessage);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Azure OpenAI Error: {StatusCode} {Error}", response.StatusCode, errorContent);
                return StatusCode((int)response.StatusCode, new { error = "Azure API request failed.", details = errorContent });
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(jsonResponse);
            var reply = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();

            return Ok(new { reply = reply });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing ChatBot AI response");
            return StatusCode(500, new { error = "An internal error occurred", message = ex.Message });
        }
    }
}

public class ChatRequest
{
    public List<ChatMessage> Messages { get; set; } = new();
}

public class ChatMessage
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}
