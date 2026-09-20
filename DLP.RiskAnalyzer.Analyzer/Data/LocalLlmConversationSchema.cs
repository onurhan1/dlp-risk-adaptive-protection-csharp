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

        CREATE TABLE IF NOT EXISTS dlp.local_llm_mail_proposals (
            id UUID PRIMARY KEY,
            conversation_id UUID NOT NULL REFERENCES dlp.local_llm_conversations(id) ON DELETE CASCADE,
            owner_username VARCHAR(120) NOT NULL,
            user_name VARCHAR(255) NOT NULL,
            full_name VARCHAR(255),
            department VARCHAR(255),
            recipient_email VARCHAR(255),
            subject VARCHAR(500) NOT NULL,
            body TEXT NOT NULL,
            source_template_id INTEGER,
            source_template_name VARCHAR(255),
            template_origin VARCHAR(20) NOT NULL DEFAULT 'fallback',
            incident_summary_json TEXT NOT NULL DEFAULT '{}',
            rationale TEXT,
            source_prompt_hash VARCHAR(64) NOT NULL,
            status VARCHAR(20) NOT NULL DEFAULT 'pending',
            reviewed_by VARCHAR(120),
            reviewed_at TIMESTAMP,
            review_decision VARCHAR(20),
            created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
            updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
            sent_at TIMESTAMP,
            error_message TEXT
        );
        CREATE INDEX IF NOT EXISTS ix_local_llm_mail_proposals_conversation_status
            ON dlp.local_llm_mail_proposals (conversation_id, status, created_at DESC);
        CREATE INDEX IF NOT EXISTS ix_local_llm_mail_proposals_owner_status
            ON dlp.local_llm_mail_proposals (owner_username, status, updated_at DESC);

        ALTER TABLE dlp.local_llm_mail_proposals
            ADD COLUMN IF NOT EXISTS source_template_id INTEGER;
        ALTER TABLE dlp.local_llm_mail_proposals
            ADD COLUMN IF NOT EXISTS source_template_name VARCHAR(255);
        ALTER TABLE dlp.local_llm_mail_proposals
            ADD COLUMN IF NOT EXISTS template_origin VARCHAR(20) NOT NULL DEFAULT 'fallback';
        ALTER TABLE dlp.local_llm_mail_proposals
            ADD COLUMN IF NOT EXISTS reviewed_by VARCHAR(120);
        ALTER TABLE dlp.local_llm_mail_proposals
            ADD COLUMN IF NOT EXISTS reviewed_at TIMESTAMP;
        ALTER TABLE dlp.local_llm_mail_proposals
            ADD COLUMN IF NOT EXISTS review_decision VARCHAR(20);
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
