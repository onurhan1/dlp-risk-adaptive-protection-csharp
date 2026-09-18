using Microsoft.EntityFrameworkCore;

namespace DLP.RiskAnalyzer.Analyzer.Data;

/// <summary>Creates Local LLM chat storage without relying on the legacy EF snapshot.</summary>
public static class LocalLlmConversationSchema
{
    private const string Ddl = @"
        CREATE SCHEMA IF NOT EXISTS dlp;

        CREATE TABLE IF NOT EXISTS dlp.local_llm_conversations (
            id UUID PRIMARY KEY,
            owner_username VARCHAR(120) NOT NULL,
            title VARCHAR(200) NOT NULL DEFAULT 'Yeni sohbet',
            created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
            updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
        );
        CREATE INDEX IF NOT EXISTS ix_local_llm_conversations_owner_updated
            ON dlp.local_llm_conversations (owner_username, updated_at DESC);

        CREATE TABLE IF NOT EXISTS dlp.local_llm_conversation_messages (
            id BIGSERIAL PRIMARY KEY,
            conversation_id UUID NOT NULL REFERENCES dlp.local_llm_conversations(id) ON DELETE CASCADE,
            role VARCHAR(20) NOT NULL,
            content TEXT NOT NULL,
            created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
        );
        CREATE INDEX IF NOT EXISTS ix_local_llm_conversation_messages_conversation_created
            ON dlp.local_llm_conversation_messages (conversation_id, created_at);
    ";

    public static async Task EnsureAsync(AnalyzerDbContext context, ILogger logger, CancellationToken ct = default)
    {
        try
        {
            await context.Database.ExecuteSqlRawAsync(Ddl, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not ensure Local LLM conversation tables");
        }
    }
}
