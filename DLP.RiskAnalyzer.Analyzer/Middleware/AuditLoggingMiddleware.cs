using DLP.RiskAnalyzer.Analyzer.Services;
using System.Diagnostics;
using System.Text;
using System.Security.Claims;

namespace DLP.RiskAnalyzer.Analyzer.Middleware;

public class AuditLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AuditLoggingMiddleware> _logger;

    public AuditLoggingMiddleware(RequestDelegate next, ILogger<AuditLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, AuditLogService auditLogService)
    {
        var stopwatch = Stopwatch.StartNew();
        var path = context.Request.Path.Value ?? "";
        var method = context.Request.Method;
        
        // Try to get user from authenticated context
        var userName = "Anonymous";
        var userRole = (string?)null;
        
        if (context.User?.Identity?.IsAuthenticated == true)
        {
            userName = context.User?.Identity?.Name ?? 
                      context.User?.Claims?.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value ??
                      context.User?.Claims?.FirstOrDefault(c => c.Type == "name")?.Value ??
                      "Anonymous";
            
            userRole = context.User?.Claims?.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value ??
                      context.User?.Claims?.FirstOrDefault(c => c.Type == "role")?.Value;
        }
        else
        {
            // Try to extract from Authorization header manually if authentication middleware hasn't processed it yet
            var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
            if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var token = authHeader.Substring("Bearer ".Length).Trim();
                    var tokenHandler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
                    var jwtToken = tokenHandler.ReadJwtToken(token);
                    
                    userName = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name || c.Type == "name")?.Value ??
                              jwtToken.Claims.FirstOrDefault(c => c.Type == "sub")?.Value ??
                              "Anonymous";
                    userRole = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role || c.Type == "role")?.Value;
                }
                catch
                {
                    // If token parsing fails, keep Anonymous
                }
            }
        }
        
        var ipAddress = context.Connection.RemoteIpAddress?.ToString();
        var userAgent = context.Request.Headers["User-Agent"].ToString();

        // Skip logging for health checks and swagger
        if (path.StartsWith("/health") || path.StartsWith("/swagger") || path == "/")
        {
            await _next(context);
            return;
        }

        string? requestBody = null;
        if (context.Request.ContentLength > 0 && context.Request.ContentLength < 10000)
        {
            context.Request.EnableBuffering();
            var buffer = new byte[context.Request.ContentLength ?? 0];
            await context.Request.Body.ReadAsync(buffer, 0, buffer.Length);
            requestBody = Encoding.UTF8.GetString(buffer);
            context.Request.Body.Position = 0;
        }

        var statusCode = 0;
        string? errorMessage = null;
        var success = true;

        try
        {
            await _next(context);
            statusCode = context.Response.StatusCode;
            success = statusCode >= 200 && statusCode < 400;
        }
        catch (Exception ex)
        {
            statusCode = 500;
            errorMessage = ex.Message;
            success = false;
            throw;
        }
        finally
        {
            stopwatch.Stop();

            var eventType = DetermineEventType(path, method);
            var resource = ExtractResource(path, method);

            var details = new Dictionary<string, object>
            {
                { "method", method },
                { "path", path }
            };

            if (!string.IsNullOrWhiteSpace(requestBody) && !IsSensitivePath(path))
            {
                try
                {
                    var jsonDoc = System.Text.Json.JsonDocument.Parse(requestBody);
                    details["requestBody"] = jsonDoc.RootElement;
                }
                catch
                {
                    details["requestBody"] = requestBody;
                }
            }

            await auditLogService.LogAsync(
                eventType: eventType,
                userName: userName,
                userRole: userRole,
                action: $"{method} {path}",
                resource: resource,
                details: System.Text.Json.JsonSerializer.Serialize(details),
                ipAddress: ipAddress,
                userAgent: userAgent,
                success: success,
                errorMessage: errorMessage,
                statusCode: statusCode,
                durationMs: stopwatch.ElapsedMilliseconds
            );
        }
    }

    private static string DetermineEventType(string path, string method)
    {
        if (path.StartsWith("/api/auth"))
        {
            return method == "POST" ? "Login" : "AuthCheck";
        }

        if (path.StartsWith("/api/users"))
        {
            return method switch
            {
                "POST" => "UserCreate",
                "PUT" => "UserUpdate",
                "DELETE" => "UserDelete",
                _ => "UserView"
            };
        }

        if (path.StartsWith("/api/incidents"))
        {
            return "IncidentView";
        }

        if (path.StartsWith("/api/settings"))
        {
            return "SettingsChange";
        }

        if (path.StartsWith("/api/logs"))
        {
            return "LogView";
        }

        return "ApiCall";
    }

    private static string? ExtractResource(string path, string method)
    {
        // Extract resource identifier from path
        // e.g., /api/users/123 -> "User:123"
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        
        if (parts.Length >= 3 && int.TryParse(parts[2], out var id))
        {
            var resourceType = parts[1].Replace("api/", "").TrimEnd('s');
            return $"{char.ToUpper(resourceType[0])}{resourceType.Substring(1)}:{id}";
        }

        return null;
    }

    private static bool IsSensitivePath(string path)
    {
        return path.Contains("/auth/login") ||
               path.Contains("/settings/dlp") ||
               path.Contains("/settings/email") ||
               path.Contains("/settings/ai");
    }
}

