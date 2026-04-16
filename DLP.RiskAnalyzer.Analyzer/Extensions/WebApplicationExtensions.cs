using DLP.RiskAnalyzer.Analyzer.Data;
using DLP.RiskAnalyzer.Analyzer.Services;
using Microsoft.EntityFrameworkCore;

namespace DLP.RiskAnalyzer.Analyzer.Extensions;

/// <summary>
/// Extension methods for configuring the WebApplication middleware pipeline.
/// </summary>
public static class WebApplicationExtensions
{
    /// <summary>
    /// Apply pending database migrations and seed default admin user.
    /// </summary>
    public static async Task ApplyMigrationsAndSeedAsync(this WebApplication app, IConfiguration configuration)
    {
        var logger = app.Services.GetRequiredService<ILogger<Program>>();

        var autoMigrate = configuration.GetValue<bool>("Database:AutoMigrate", true);

        if (autoMigrate)
        {
            logger.LogInformation("=== AUTOMATIC MIGRATION ENABLED ===");
            using var scope = app.Services.CreateScope();
            var services = scope.ServiceProvider;
            try
            {
                var context = services.GetRequiredService<AnalyzerDbContext>();

                logger.LogInformation("Checking database connection...");
                var canConnect = await context.Database.CanConnectAsync();
                if (!canConnect)
                {
                    logger.LogError("Cannot connect to database. Please check connection string and ensure PostgreSQL is running.");
                    throw new InvalidOperationException("Cannot connect to database. Check connection string and PostgreSQL service.");
                }
                logger.LogInformation("Database connection successful.");

                var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
                if (pendingMigrations.Any())
                {
                    logger.LogInformation("Found {Count} pending migrations: {Migrations}",
                        pendingMigrations.Count(),
                        string.Join(", ", pendingMigrations));
                }
                else
                {
                    logger.LogInformation("No pending migrations. Database is up to date.");
                }

                logger.LogInformation("Applying database migrations automatically...");
                await context.Database.MigrateAsync();
                logger.LogInformation("=== Database migrations applied successfully ===");
            }
            catch (Npgsql.NpgsqlException ex)
            {
                logger.LogError(ex, "PostgreSQL connection error during migration: {Message}. Please check: 1) PostgreSQL is running, 2) Connection string is correct, 3) Database 'dlp_analyzer' exists.", ex.Message);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while applying database migrations: {Message}", ex.Message);
            }
        }
        else
        {
            logger.LogInformation("=== AUTOMATIC MIGRATION DISABLED ===");
            logger.LogInformation("Automatic database migration is disabled. Migrations must be applied manually using 'dotnet ef database update'.");
        }

        // Seed default admin user
        logger.LogInformation("=== SEEDING DEFAULT ADMIN USER ===");
        try
        {
            using var scope = app.Services.CreateScope();
            var userService = scope.ServiceProvider.GetRequiredService<IUserService>();
            var seedLogger = scope.ServiceProvider.GetRequiredService<ILogger<Controllers.UsersController>>();
            await userService.SeedDefaultAdminAsync(configuration, seedLogger);
            logger.LogInformation("=== DEFAULT ADMIN USER SEED COMPLETED ===");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to seed default admin user: {Message}", ex.Message);
        }
    }

    /// <summary>
    /// Configure the HTTP request pipeline middleware (security headers, swagger, etc.)
    /// </summary>
    public static WebApplication ConfigurePipeline(this WebApplication app)
    {
        // Forward proxy headers
        app.UseForwardedHeaders(new Microsoft.AspNetCore.Builder.ForwardedHeadersOptions
        {
            ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor
                             | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto
        });

        app.UseRouting();
        app.UseCors();
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();

        // Global Exception Handling
        app.UseMiddleware<Middleware.ExceptionHandlingMiddleware>();

        // Audit logging middleware (after UseAuthentication to get user info)
        app.UseMiddleware<Middleware.AuditLoggingMiddleware>();

        // Security headers
        app.Use(async (context, next) =>
        {
            context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
            context.Response.Headers.Append("X-Frame-Options", "DENY");
            context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
            context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");

            if (!app.Environment.IsDevelopment())
            {
                context.Response.Headers.Append("Content-Security-Policy",
                    "default-src 'self'; script-src 'self' 'unsafe-inline' 'unsafe-eval'; style-src 'self' 'unsafe-inline';");
            }

            await next();
        });

        // Swagger (Development only)
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "DLP Risk Analyzer API v1");
                c.RoutePrefix = "swagger";
            });
        }

        return app;
    }

    /// <summary>
    /// Configure URL binding for network access
    /// </summary>
    public static WebApplication ConfigureUrls(this WebApplication app)
    {
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        const string defaultUrl = "http://0.0.0.0:5001";

        app.Urls.Clear();
        app.Urls.Add(defaultUrl);
        logger.LogInformation("API configured to listen on {Url} for network access", defaultUrl);

        foreach (var url in app.Urls)
        {
            logger.LogInformation("  Listening: {Url}", url);
            logger.LogInformation("  Swagger UI: {Url}/swagger", url);
            logger.LogInformation("  Health Check: {Url}/health", url);
        }

        return app;
    }
}
