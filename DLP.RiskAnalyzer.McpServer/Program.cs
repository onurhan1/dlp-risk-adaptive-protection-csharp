using DLP.RiskAnalyzer.McpServer;

var builder = WebApplication.CreateBuilder(args);

// ── DLP API istemcisi ─────────────────────────────────────────────────────────
builder.Services.AddHttpClient<DlpApiClient>((sp, client) =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    client.BaseAddress = new Uri(config["DlpApi:BaseUrl"] ?? "http://localhost:5000");
    client.Timeout = TimeSpan.FromSeconds(30);
});

// ── Agent servisleri ──────────────────────────────────────────────────────────
builder.Services.AddSingleton<ToolRegistry>();
builder.Services.AddSingleton<AgentService>();

// ── MCP Server (n8n için) ─────────────────────────────────────────────────────
builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();

// ── CORS ──────────────────────────────────────────────────────────────────────
builder.Services.AddCors(opts =>
    opts.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.UseCors();
app.UseDefaultFiles();   // index.html otomatik serve edilir
app.UseStaticFiles();    // wwwroot/ klasörü

// ── MCP endpoint (n8n → /sse) ─────────────────────────────────────────────────
app.MapMcp();

// ── Chat API ──────────────────────────────────────────────────────────────────

// POST /api/chat — SSE stream ile yanıt döner
app.MapPost("/api/chat", async (ChatRequest req, AgentService agent, HttpResponse res) =>
{
    if (string.IsNullOrWhiteSpace(req.Message))
    {
        res.StatusCode = 400;
        await res.WriteAsJsonAsync(new { error = "Mesaj boş olamaz." });
        return;
    }

    var sessionId = req.SessionId ?? Guid.NewGuid().ToString();
    await agent.StreamAsync(sessionId, req.Message, res);
});

// DELETE /api/chat/{sessionId} — Konuşmayı sıfırla
app.MapDelete("/api/chat/{sessionId}", async (string sessionId, AgentService agent) =>
{
    await agent.ClearSessionAsync(sessionId);
    return Results.Ok(new { cleared = true });
});

// GET /health
app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    service = "DLP Risk Analyzer MCP Server",
    timestamp = DateTime.UtcNow
}));

app.Run();

// ── Request modeli ────────────────────────────────────────────────────────────
record ChatRequest(string Message, string? SessionId);
