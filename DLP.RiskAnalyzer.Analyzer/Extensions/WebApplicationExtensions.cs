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

        // Ensure schemas (dlp/log/auth) exist and relocate tables to them.
        // Done at runtime (idempotent, data-preserving) so no EF migration is required.
        await EnsureSchemasAndMoveTablesAsync(app, logger);
        await EnsureInvestigationQuerySchemaAsync(app, logger);

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
    /// Ensure the dlp/log/auth schemas exist and move existing tables into them.
    /// Idempotent and data-preserving (ALTER TABLE ... SET SCHEMA), runs on every startup,
    /// so the schema layout is applied automatically without an EF migration. Tables are
    /// moved from whichever schema they currently live in (public or dlp) to their target.
    /// </summary>
    private static async Task EnsureSchemasAndMoveTablesAsync(WebApplication app, ILogger logger)
    {
        // dlp = application data, log = log tables, auth = users + all configuration
        var dlpTables = new[]
        {
            "incidents", "daily_summaries", "user_risk_trends", "department_summaries",
            "ai_behavioral_analyses", "anomaly_detections", "nda_domains", "user_daily_risk_scores",
            "domain_feature_definitions", "domain_feature_values", "ai_explanations", "released_incidents",
            "merceks", "policy_rule_exceptions", "permanent_exceptions", "exception_removals",
            "isolation_forest_scores", "mail_templates"
        };

        var sql = new System.Text.StringBuilder();
        sql.AppendLine("CREATE SCHEMA IF NOT EXISTS dlp;");
        sql.AppendLine("CREATE SCHEMA IF NOT EXISTS log;");
        sql.AppendLine("CREATE SCHEMA IF NOT EXISTS auth;");

        // Reconcile duplicate tables: runtime code (seed / settings) may have created empty
        // copies of users/system_settings in 'auth' while the real data still lives in 'public'.
        // Merge public rows into the auth table (no data loss), then rename the public copy aside
        // as a backup so the subsequent SET SCHEMA is a harmless no-op. Idempotent (to_regclass).
        sql.AppendLine(@"
DO $$
BEGIN
  IF to_regclass('public.users') IS NOT NULL AND to_regclass('auth.users') IS NOT NULL THEN
    INSERT INTO auth.users (username, email, role, password_hash, password_salt, is_active, created_at)
    SELECT username, email, role, password_hash, password_salt, is_active, created_at
    FROM public.users s
    WHERE NOT EXISTS (SELECT 1 FROM auth.users d WHERE lower(d.username) = lower(s.username));
    ALTER TABLE public.users RENAME TO users_backup_premigration;
  END IF;
END $$;");

        sql.AppendLine(@"
DO $$
BEGIN
  IF to_regclass('public.system_settings') IS NOT NULL AND to_regclass('auth.system_settings') IS NOT NULL THEN
    INSERT INTO auth.system_settings (key, value, updated_at)
    SELECT key, value, updated_at FROM public.system_settings s
    WHERE NOT EXISTS (SELECT 1 FROM auth.system_settings d WHERE d.key = s.key);
    ALTER TABLE public.system_settings RENAME TO system_settings_backup_premigration;
  END IF;
END $$;");

        // Log tables -> log
        foreach (var t in new[] { "audit_logs", "user_activity_logs" })
        {
            sql.AppendLine($"ALTER TABLE IF EXISTS public.{t} SET SCHEMA log;");
            sql.AppendLine($"ALTER TABLE IF EXISTS dlp.{t} SET SCHEMA log;");
        }

        // Users + configuration -> auth
        foreach (var t in new[] { "users", "system_settings" })
        {
            sql.AppendLine($"ALTER TABLE IF EXISTS public.{t} SET SCHEMA auth;");
            sql.AppendLine($"ALTER TABLE IF EXISTS dlp.{t} SET SCHEMA auth;");
        }

        // Everything else -> dlp (only from public; if already in dlp the IF EXISTS no-ops)
        foreach (var t in dlpTables)
        {
            sql.AppendLine($"ALTER TABLE IF EXISTS public.{t} SET SCHEMA dlp;");
        }

        // isolation_forest_scores columns added after the table shipped. Ensured here — after the
        // relocation above, so the table is guaranteed to be in dlp — because the EF migrations
        // that add them are skipped whenever Database:AutoMigrate is false, and because a server
        // that already failed those migrations never records them as applied. Missing columns are
        // not a degraded chart: every read of the entity fails outright with
        // "column i.explanation does not exist" and the AI risk model page shows no score at all.
        // Idempotent (ADD COLUMN IF NOT EXISTS), so this is a no-op once the columns are there.
        //
        // The explanation default is the empty JSON object, spelled chr(123)||chr(125) rather than
        // written out: ExecuteSqlRaw runs the statement through string.Format to substitute its
        // positional parameters, so a literal curly brace in the text would throw a FormatException
        // before the SQL ever reaches PostgreSQL — taking the schema relocation above down with it.
        sql.AppendLine(@"
DO $$
BEGIN
  IF to_regclass('dlp.policy_rule_exceptions') IS NOT NULL THEN
    ALTER TABLE dlp.policy_rule_exceptions
      ADD COLUMN IF NOT EXISTS enabled character varying(10) NOT NULL DEFAULT 'true';
  END IF;
END $$;");

        sql.AppendLine(@"
DO $$
BEGIN
  IF to_regclass('dlp.isolation_forest_scores') IS NOT NULL THEN
    ALTER TABLE dlp.isolation_forest_scores
      ADD COLUMN IF NOT EXISTS baseline_incident_count INTEGER NOT NULL DEFAULT 0;
    ALTER TABLE dlp.isolation_forest_scores
      ADD COLUMN IF NOT EXISTS explanation_version INTEGER NOT NULL DEFAULT 1;
    ALTER TABLE dlp.isolation_forest_scores
      ADD COLUMN IF NOT EXISTS explanation TEXT NOT NULL DEFAULT (chr(123) || chr(125));
  END IF;
END $$;");

        try
        {
            using var scope = app.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AnalyzerDbContext>();
            await context.Database.ExecuteSqlRawAsync(sql.ToString());
            logger.LogInformation("=== SCHEMAS ENSURED (dlp/log/auth) AND TABLES RELOCATED ===");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to ensure schemas / relocate tables: {Message}", ex.Message);
        }
    }

    private static async Task EnsureInvestigationQuerySchemaAsync(WebApplication app, ILogger logger)
    {
        using var scope = app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AnalyzerDbContext>();
        await InvestigationQuerySchema.EnsureAsync(context, logger);
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
