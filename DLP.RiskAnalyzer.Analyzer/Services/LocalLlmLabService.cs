using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DLP.RiskAnalyzer.Analyzer.Data;
using DLP.RiskAnalyzer.Analyzer.Models;
using Microsoft.EntityFrameworkCore;

namespace DLP.RiskAnalyzer.Analyzer.Services;

/// <summary>
/// Controlled bridge between RADAR incidents and an Ollama-compatible local model.
/// The model receives a bounded, read-only snapshot rather than database credentials or SQL.
/// </summary>
public sealed class LocalLlmLabService : ILocalLlmLabService
{
    private const string EnabledKey = "local_llm_lab_enabled";
    private const string GenerateUrlKey = "local_llm_lab_generate_url";
    private const string ModelKey = "local_llm_lab_model";
    private const string TemperatureKey = "local_llm_lab_temperature";
    private const string MaxTokensKey = "local_llm_lab_max_tokens";

    private readonly AnalyzerDbContext _context;
    private readonly HttpClient _httpClient;
    private readonly IDirectorySettingsService _directorySettings;
    private readonly IEmailService _emailService;
    private readonly ILogger<LocalLlmLabService> _logger;

    public LocalLlmLabService(
        AnalyzerDbContext context,
        HttpClient httpClient,
        IDirectorySettingsService directorySettings,
        IEmailService emailService,
        ILogger<LocalLlmLabService> logger)
    {
        _context = context;
        _httpClient = httpClient;
        _directorySettings = directorySettings;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<LocalLlmLabSettings> GetSettingsAsync(CancellationToken ct)
    {
        var values = await _context.SystemSettings.AsNoTracking()
            .Where(setting => setting.Key.StartsWith("local_llm_lab_"))
            .ToDictionaryAsync(setting => setting.Key, setting => setting.Value, ct);

        return new LocalLlmLabSettings(
            bool.TryParse(values.GetValueOrDefault(EnabledKey), out var enabled) && enabled,
            values.GetValueOrDefault(GenerateUrlKey, "http://127.0.0.1:11434/api/generate"),
            values.GetValueOrDefault(ModelKey, "qwen3:8b"),
            double.TryParse(values.GetValueOrDefault(TemperatureKey), out var temperature) ? temperature : 0.2,
            int.TryParse(values.GetValueOrDefault(MaxTokensKey), out var maxTokens) ? maxTokens : 1800);
    }

    public async Task SaveSettingsAsync(LocalLlmLabSettings settings, CancellationToken ct)
    {
        ValidateSettings(settings);
        var entries = new Dictionary<string, string>
        {
            [EnabledKey] = settings.Enabled.ToString(),
            [GenerateUrlKey] = settings.GenerateUrl.Trim(),
            [ModelKey] = settings.Model.Trim(),
            [TemperatureKey] = settings.Temperature.ToString(System.Globalization.CultureInfo.InvariantCulture),
            [MaxTokensKey] = settings.MaxTokens.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };

        var existing = await _context.SystemSettings
            .Where(setting => entries.Keys.Contains(setting.Key))
            .ToDictionaryAsync(setting => setting.Key, ct);

        foreach (var (key, value) in entries)
        {
            if (existing.TryGetValue(key, out var setting)) setting.Value = value;
            else _context.SystemSettings.Add(new SystemSetting { Key = key, Value = value });
        }

        await _context.SaveChangesAsync(ct);
    }

    public async Task<LocalLlmHealthCheck> TestConnectionAsync(LocalLlmLabSettings settings, CancellationToken ct)
    {
        ValidateSettings(settings);
        try
        {
            var reply = await GenerateAsync(settings, "RADAR bağlantı testi. Yalnızca 'bağlantı başarılı' yaz.", 48, ct);
            return new LocalLlmHealthCheck(true, "healthy", "Yerel model yanıt verdi.", reply.Trim());
        }
        catch (LocalLlmConnectionException ex)
        {
            return new LocalLlmHealthCheck(false, ex.Code, ex.Detail, null, ex.Retryable, ex.SuggestedAction);
        }
    }

    public async Task<LocalLlmIncidentSnapshot> GetIncidentSnapshotAsync(LocalLlmSnapshotRequest request, CancellationToken ct)
    {
        var lookbackDays = Math.Clamp(request.LookbackDays, 1, 365);
        var sampleSize = Math.Clamp(request.SampleSize, 5, 100);
        var end = request.EndUtc?.ToUniversalTime() ?? DateTime.UtcNow;
        var start = request.StartUtc?.ToUniversalTime() ?? end.AddDays(-lookbackDays);
        if (start > end) throw new ArgumentException("Başlangıç tarihi bitiş tarihinden sonra olamaz.");
        if (end - start > TimeSpan.FromDays(366)) throw new ArgumentException("Kapsamlı analiz için en fazla 366 günlük tarih aralığı seçebilirsiniz.");
        var incidents = _context.Incidents.AsNoTracking().Where(incident => incident.Timestamp >= start && incident.Timestamp <= end);

        var total = await incidents.CountAsync(ct);
        var uniqueUsers = await incidents.Select(incident => incident.UserEmail).Distinct().CountAsync(ct);
        var maximumMatches = await incidents.MaxAsync(incident => (int?)incident.MaxMatches, ct) ?? 0;

        var actions = await CountByAsync(incidents.Select(incident => incident.Action), ct);
        var channels = await CountByAsync(incidents.Select(incident => incident.Channel), ct);
        var policies = await CountByAsync(incidents.Select(incident => incident.Policy ?? incident.RuleName), ct);

        // PostgreSQL'in çevirebildiği anonim projeksiyon ile sorguyu bitir; maskeleme
        // ve LocalLlm DTO üretimi EF sorgusunun dışında yapılır.
        var userRows = await incidents
            .GroupBy(incident => new { incident.UserEmail, incident.Department })
            .Select(group => new
            {
                User = group.Key.UserEmail,
                group.Key.Department,
                IncidentCount = group.Count(),
                MaximumSeverity = group.Max(incident => incident.Severity),
                MaximumMatches = group.Max(incident => incident.MaxMatches),
                AverageDataSensitivity = group.Average(incident => (double)incident.DataSensitivity),
                RepeatCount = group.Sum(incident => incident.RepeatCount),
            })
            .OrderByDescending(user => user.MaximumMatches)
            .ThenByDescending(user => user.IncidentCount)
            .Take(request.Comprehensive ? 200 : 30)
            .ToListAsync(ct);
        var users = userRows.Select(user => new LocalLlmUserProfile(
            request.MaskIdentifiers ? Mask(user.User) : user.User,
            user.Department,
            user.IncidentCount,
            user.MaximumSeverity,
            user.MaximumMatches,
            user.AverageDataSensitivity,
            user.RepeatCount)).ToList();

        var sampleRows = await incidents
            .OrderByDescending(incident => incident.MaxMatches)
            .ThenByDescending(incident => incident.Severity)
            .ThenByDescending(incident => incident.RepeatCount)
            .ThenByDescending(incident => incident.Timestamp)
            .Take(sampleSize)
            .Select(incident => new
            {
                incident.Timestamp,
                incident.UserEmail,
                incident.Department,
                incident.Action,
                incident.Severity,
                incident.MaxMatches,
                incident.DataSensitivity,
                incident.RepeatCount,
                incident.Policy,
                incident.RuleName,
                incident.Channel,
                incident.Destination,
            })
            .ToListAsync(ct);
        var samples = sampleRows.Select(incident => new LocalLlmIncidentSample(
            incident.Timestamp,
            request.MaskIdentifiers ? Mask(incident.UserEmail) : incident.UserEmail,
            incident.Department,
            incident.Action,
            incident.Severity,
            incident.MaxMatches,
            incident.DataSensitivity,
            incident.RepeatCount,
            incident.Policy,
            incident.RuleName,
            incident.Channel,
            request.MaskIdentifiers ? MaskDestination(incident.Destination) : incident.Destination)).ToList();

        return new LocalLlmIncidentSnapshot(
            start, end, total, uniqueUsers, maximumMatches, users.Count,
            actions, channels, policies, users, samples);
    }

    public async Task<IReadOnlyList<LocalLlmConversationSummary>> GetConversationsAsync(string ownerUsername, CancellationToken ct)
    {
        var rows = await _context.LocalLlmConversations.AsNoTracking()
            .Where(conversation => conversation.OwnerUsername == ownerUsername)
            .OrderByDescending(conversation => conversation.UpdatedAt)
            .Take(100)
            .Select(conversation => new
            {
                conversation.Id,
                conversation.Title,
                conversation.UpdatedAt,
                MessageCount = conversation.Messages.Count,
                Preview = conversation.Messages
                    .OrderByDescending(message => message.CreatedAt)
                    .Select(message => message.Content)
                    .FirstOrDefault(),
            })
            .ToListAsync(ct);

        return rows.Select(row => new LocalLlmConversationSummary(
            row.Id,
            row.Title,
            row.UpdatedAt,
            row.MessageCount,
            Truncate(row.Preview, 140))).ToList();
    }

    public async Task<LocalLlmConversationDetail> CreateConversationAsync(string ownerUsername, string? title, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var conversation = new LocalLlmConversation
        {
            OwnerUsername = ownerUsername,
            Title = NormalizeTitle(title),
            CreatedAt = now,
            UpdatedAt = now,
        };
        _context.LocalLlmConversations.Add(conversation);
        await _context.SaveChangesAsync(ct);
        return ToDetail(conversation, []);
    }

    public async Task<LocalLlmConversationDetail?> GetConversationAsync(Guid conversationId, string ownerUsername, CancellationToken ct)
    {
        var conversation = await _context.LocalLlmConversations.AsNoTracking()
            .Where(item => item.Id == conversationId && item.OwnerUsername == ownerUsername)
            .Select(item => new { item.Id, item.Title, item.CreatedAt, item.UpdatedAt })
            .FirstOrDefaultAsync(ct);
        if (conversation == null) return null;

        var messages = await _context.LocalLlmConversationMessages.AsNoTracking()
            .Where(message => message.ConversationId == conversationId)
            .OrderBy(message => message.CreatedAt)
            .Select(message => new LocalLlmChatMessage(message.Role, message.Content))
            .ToListAsync(ct);

        return new LocalLlmConversationDetail(
            conversation.Id, conversation.Title, conversation.CreatedAt, conversation.UpdatedAt, messages);
    }

    public async Task<bool> DeleteConversationAsync(Guid conversationId, string ownerUsername, CancellationToken ct)
    {
        var conversation = await _context.LocalLlmConversations
            .FirstOrDefaultAsync(item => item.Id == conversationId && item.OwnerUsername == ownerUsername, ct);
        if (conversation == null) return false;

        _context.LocalLlmConversations.Remove(conversation);
        await _context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<LocalLlmMailProposalDto>> GetMailProposalsAsync(Guid conversationId, string ownerUsername, CancellationToken ct)
    {
        await EnsureMailProposalStorageAsync(ct);
        var proposals = await _context.LocalLlmMailProposals.AsNoTracking()
            .Where(item => item.ConversationId == conversationId && item.OwnerUsername == ownerUsername)
            .OrderByDescending(item => item.CreatedAt)
            .ToListAsync(ct);
        return proposals.Select(ToProposalDto).ToList();
    }

    public async Task<LocalLlmMailProposalDto?> UpdateMailProposalAsync(Guid proposalId, string ownerUsername, LocalLlmMailProposalUpdateRequest request, CancellationToken ct)
    {
        await EnsureMailProposalStorageAsync(ct);
        var proposal = await _context.LocalLlmMailProposals.FirstOrDefaultAsync(item => item.Id == proposalId && item.OwnerUsername == ownerUsername, ct);
        if (proposal == null || proposal.Status is LocalLlmMailProposalStatus.Sent or LocalLlmMailProposalStatus.Rejected) return null;

        proposal.RecipientEmail = string.IsNullOrWhiteSpace(request.RecipientEmail) ? null : request.RecipientEmail.Trim();
        proposal.Subject = string.IsNullOrWhiteSpace(request.Subject) ? proposal.Subject : request.Subject.Trim();
        proposal.Body = string.IsNullOrWhiteSpace(request.Body) ? proposal.Body : request.Body.Trim();
        proposal.Status = string.IsNullOrWhiteSpace(proposal.RecipientEmail) ? LocalLlmMailProposalStatus.Unresolved : LocalLlmMailProposalStatus.Pending;
        proposal.ErrorMessage = null;
        proposal.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        return ToProposalDto(proposal);
    }

    public async Task<LocalLlmMailProposalDto?> ApproveMailProposalAsync(Guid proposalId, string ownerUsername, CancellationToken ct)
    {
        await EnsureMailProposalStorageAsync(ct);
        var proposal = await _context.LocalLlmMailProposals.FirstOrDefaultAsync(item => item.Id == proposalId && item.OwnerUsername == ownerUsername, ct);
        if (proposal == null || proposal.Status != LocalLlmMailProposalStatus.Pending || string.IsNullOrWhiteSpace(proposal.RecipientEmail)) return null;

        try
        {
            var sent = await _emailService.SendEmailAsync(proposal.RecipientEmail, proposal.Subject, proposal.Body, isHtml: false, toName: proposal.FullName);
            proposal.Status = sent ? LocalLlmMailProposalStatus.Sent : LocalLlmMailProposalStatus.Failed;
            proposal.SentAt = sent ? DateTime.UtcNow : null;
            proposal.ErrorMessage = sent ? null : "SMTP gönderimi başarısız oldu.";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Local LLM mail proposal send failed for {Recipient}", proposal.RecipientEmail);
            proposal.Status = LocalLlmMailProposalStatus.Failed;
            proposal.ErrorMessage = ex.Message;
        }

        proposal.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        return ToProposalDto(proposal);
    }

    public async Task<LocalLlmMailProposalDto?> RejectMailProposalAsync(Guid proposalId, string ownerUsername, CancellationToken ct)
    {
        await EnsureMailProposalStorageAsync(ct);
        var proposal = await _context.LocalLlmMailProposals.FirstOrDefaultAsync(item => item.Id == proposalId && item.OwnerUsername == ownerUsername, ct);
        if (proposal == null || proposal.Status != LocalLlmMailProposalStatus.Pending) return null;
        proposal.Status = LocalLlmMailProposalStatus.Rejected;
        proposal.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        return ToProposalDto(proposal);
    }

    public async Task<LocalLlmChatResult> ChatAsync(LocalLlmChatRequest request, string ownerUsername, CancellationToken ct)
    {
        await EnsureMailProposalStorageAsync(ct);
        if (string.IsNullOrWhiteSpace(request.Message))
            throw new ArgumentException("Mesaj boş olamaz.", nameof(request));

        var settings = await GetSettingsAsync(ct);
        if (!settings.Enabled)
            throw new InvalidOperationException("Yerel LLM Laboratuvarı ayarlardan etkinleştirilmemiş.");

        var conversation = request.ConversationId.HasValue
            ? await _context.LocalLlmConversations.FirstOrDefaultAsync(item =>
                item.Id == request.ConversationId.Value && item.OwnerUsername == ownerUsername, ct)
            : null;

        if (request.ConversationId.HasValue && conversation == null)
            throw new ArgumentException("Sohbet bulunamadı veya erişim yetkiniz yok.", nameof(request));

        if (conversation == null)
        {
            var now = DateTime.UtcNow;
            conversation = new LocalLlmConversation
            {
                OwnerUsername = ownerUsername,
                Title = TitleFromMessage(request.Message),
                CreatedAt = now,
                UpdatedAt = now,
            };
            _context.LocalLlmConversations.Add(conversation);
            await _context.SaveChangesAsync(ct);
        }

        var persistedHistory = await _context.LocalLlmConversationMessages.AsNoTracking()
            .Where(message => message.ConversationId == conversation.Id)
            .OrderBy(message => message.CreatedAt)
            .Select(message => new LocalLlmChatMessage(message.Role, message.Content))
            .ToListAsync(ct);

        var messageTime = DateTime.UtcNow;
        _context.LocalLlmConversationMessages.Add(new LocalLlmConversationMessage
        {
            ConversationId = conversation.Id,
            Role = "user",
            Content = request.Message.Trim(),
            CreatedAt = messageTime,
        });
        conversation.UpdatedAt = messageTime;
        if (conversation.Title == "Yeni sohbet") conversation.Title = TitleFromMessage(request.Message);
        await _context.SaveChangesAsync(ct);

        var snapshot = await GetIncidentSnapshotAsync(
            new LocalLlmSnapshotRequest(request.LookbackDays, request.SampleSize, request.MaskIdentifiers, request.StartUtc, request.EndUtc, request.Comprehensive), ct);
        var comprehensiveEvidence = request.Comprehensive
            ? await BuildComprehensiveEvidenceAsync(request, ct)
            : null;
        var prompt = BuildPrompt(request with { History = persistedHistory }, snapshot, comprehensiveEvidence);
        var reply = await GenerateAsync(settings, prompt, settings.MaxTokens, ct);

        var draftResult = RequestsMailDraft(request.Message)
            ? await PrepareMailDraftsAsync(conversation.Id, ownerUsername, request, settings, ct)
            : MailDraftPreparationResult.Empty;
        if (draftResult.Prepared > 0 || draftResult.Unresolved > 0)
        {
            reply += $"\n\nMail taslakları hazırlandı: {draftResult.Prepared} onay bekliyor" +
                (draftResult.Unresolved > 0 ? $", {draftResult.Unresolved} kullanıcı için LDAP/e-posta bilgisi bulunamadı." : ".") +
                " Taslakları bu sohbetin altından inceleyip düzenleyebilir, onaylayabilir veya reddedebilirsiniz.";
        }

        var replyTime = DateTime.UtcNow;
        _context.LocalLlmConversationMessages.Add(new LocalLlmConversationMessage
        {
            ConversationId = conversation.Id,
            Role = "assistant",
            Content = reply,
            CreatedAt = replyTime,
        });
        conversation.UpdatedAt = replyTime;
        await _context.SaveChangesAsync(ct);

        return new LocalLlmChatResult(conversation.Id, reply, snapshot, draftResult.Prepared, draftResult.Unresolved);
    }

    private async Task<string?> BuildComprehensiveEvidenceAsync(LocalLlmChatRequest request, CancellationToken ct)
    {
        var end = request.EndUtc?.ToUniversalTime() ?? DateTime.UtcNow;
        var start = request.StartUtc?.ToUniversalTime() ?? end.AddDays(-Math.Clamp(request.LookbackDays, 1, 365));
        var detailedUserLimit = Math.Clamp(request.DetailedUserLimit, 1, 50);
        var evidenceRowsPerUser = Math.Clamp(request.EvidenceRowsPerUser, 25, 500);
        var timeWindowHours = ExtractTimeWindowHours(request.Message);
        var incidents = _context.Incidents.AsNoTracking()
            .Where(incident => incident.Timestamp >= start && incident.Timestamp <= end && incident.UserEmail != null && incident.UserEmail != "");

        // Every incident contributes to the ranking. The selected candidates then receive
        // a complete, lossless count-based evidence history rather than a small event sample.
        var candidates = await incidents
            .GroupBy(incident => incident.UserEmail)
            .Select(group => new
            {
                User = group.Key!,
                IncidentCount = group.Count(),
                MaximumMatches = group.Max(incident => incident.MaxMatches),
                MaximumSeverity = group.Max(incident => incident.Severity),
                RepeatCount = group.Sum(incident => incident.RepeatCount),
            })
            .OrderByDescending(candidate => candidate.MaximumMatches)
            .ThenByDescending(candidate => candidate.MaximumSeverity)
            .ThenByDescending(candidate => candidate.IncidentCount)
            .Take(detailedUserLimit)
            .ToListAsync(ct);

        if (candidates.Count == 0) return null;

        var candidateUsers = candidates.Select(candidate => candidate.User).ToList();

        // This query intentionally reads every raw event for the selected candidates.
        // It powers temporal pattern detection; the model only receives a configurable
        // subset of timeline rows so a large period cannot exhaust its context window.
        var timelineSourceRows = await incidents
            .Where(incident => candidateUsers.Contains(incident.UserEmail!))
            .OrderBy(incident => incident.Timestamp)
            .Select(incident => new
            {
                User = incident.UserEmail!,
                incident.Timestamp,
                incident.Action,
                incident.Policy,
                incident.RuleName,
                incident.Channel,
                incident.Destination,
                incident.MaxMatches,
                incident.Severity,
                incident.DataSensitivity,
                incident.RepeatCount,
            })
            .ToListAsync(ct);
        var timelineRows = timelineSourceRows.Select(incident => new ComprehensiveTimelineEvent(
            incident.User,
            incident.Timestamp,
            incident.Action,
            incident.Policy,
            incident.RuleName,
            incident.Channel,
            incident.Destination,
            incident.MaxMatches,
            incident.Severity,
            incident.DataSensitivity,
            incident.RepeatCount)).ToList();
        var crossChannelPatterns = FindCrossChannelPatterns(timelineRows, timeWindowHours);

        var evidenceRows = await incidents
            .Where(incident => candidateUsers.Contains(incident.UserEmail!))
            .GroupBy(incident => new
            {
                incident.UserEmail,
                incident.Action,
                Policy = incident.Policy ?? incident.RuleName,
                incident.RuleName,
                incident.Channel,
            })
            .Select(group => new
            {
                User = group.Key.UserEmail!,
                group.Key.Action,
                group.Key.Policy,
                group.Key.RuleName,
                group.Key.Channel,
                IncidentCount = group.Count(),
                FirstSeen = group.Min(incident => incident.Timestamp),
                LastSeen = group.Max(incident => incident.Timestamp),
                MaximumMatches = group.Max(incident => incident.MaxMatches),
                MaximumSeverity = group.Max(incident => incident.Severity),
                MaximumSensitivity = group.Max(incident => incident.DataSensitivity),
                RepeatCount = group.Sum(incident => incident.RepeatCount),
            })
            .OrderBy(item => item.User)
            .ThenByDescending(item => item.MaximumMatches)
            .ThenByDescending(item => item.IncidentCount)
            .ToListAsync(ct);

        var text = new StringBuilder()
            .AppendLine("KAPSAMLI KULLANICI KANITI")
            .AppendLine($"Bu kanıt, {start:yyyy-MM-dd} - {end:yyyy-MM-dd} dönemindeki tüm olay kayıtlarından üretilmiştir.")
            .AppendLine($"Önceliklendirilmiş {candidates.Count} kullanıcı için olaylar aksiyon/politika/kural/kanal bazında eksiksiz sayısal olarak gruplanmıştır. Her satırdaki olay adedi, o gruba giren tüm kayıtları temsil eder.")
            .AppendLine($"Her kullanıcı için en fazla {evidenceRowsPerUser} ayrıntılı zaman çizelgesi satırı sunulur. Sunucudaki çapraz kanal taraması ise tüm ham olay kayıtlarında {timeWindowHours} saatlik pencereyle çalıştırılmıştır.");

        foreach (var candidate in candidates)
        {
            var user = request.MaskIdentifiers ? Mask(candidate.User) : candidate.User;
            text.AppendLine();
            text.AppendLine($"KULLANICI: {user}; toplam olay: {candidate.IncidentCount}; max match: {candidate.MaximumMatches}; en yüksek şiddet: {candidate.MaximumSeverity}; tekrar: {candidate.RepeatCount}.");

            foreach (var item in evidenceRows.Where(row => row.User == candidate.User))
            {
                text.AppendLine($"- olay: {item.IncidentCount}; aksiyon: {item.Action ?? "-"}; politika: {item.Policy ?? "-"}; " +
                    $"kural: {item.RuleName ?? "-"}; kanal: {item.Channel ?? "-"}; ilk: {item.FirstSeen:yyyy-MM-dd HH:mm}; son: {item.LastSeen:yyyy-MM-dd HH:mm}; " +
                    $"max match: {item.MaximumMatches}; şiddet: {item.MaximumSeverity}; hassasiyet: {item.MaximumSensitivity}; tekrar: {item.RepeatCount}.");
            }

            text.AppendLine("Ayrıntılı zaman çizelgesi:");
            foreach (var item in timelineRows.Where(row => row.User == candidate.User)
                         .OrderBy(row => row.Timestamp)
                         .Take(evidenceRowsPerUser))
            {
                text.AppendLine($"- {item.Timestamp:yyyy-MM-dd HH:mm}; aksiyon: {item.Action ?? "-"}; politika: {item.Policy ?? "-"}; " +
                    $"kural: {item.Rule ?? "-"}; kanal: {item.Channel ?? "-"}; hedef: {(request.MaskIdentifiers ? MaskDestination(item.Destination) : item.Destination) ?? "-"}; " +
                    $"max match: {item.MaxMatches}; şiddet: {item.Severity}; hassasiyet: {item.DataSensitivity}; tekrar: {item.RepeatCount}.");
            }

            var userPatterns = crossChannelPatterns.Where(pattern => pattern.User == candidate.User).ToList();
            if (userPatterns.Count > 0)
            {
                text.AppendLine($"{timeWindowHours} saat içinde aynı politika/kural için gözlenen farklı kanal geçişleri. Bunlar ilişki kanıtıdır; tek başına engel aşma veya aynı verinin aktarıldığını kanıtlamaz:");
                foreach (var pattern in userPatterns)
                {
                    var fromDestination = request.MaskIdentifiers ? MaskDestination(pattern.FromDestination) : pattern.FromDestination;
                    var toDestination = request.MaskIdentifiers ? MaskDestination(pattern.ToDestination) : pattern.ToDestination;
                    text.AppendLine($"- {pattern.OccurrenceCount} geçiş; veri kapsamı: {pattern.DataContext}; önce: {pattern.FromChannel} ({pattern.FromAction ?? "-"}, hedef: {fromDestination ?? "-"}); " +
                        $"sonra: {pattern.ToChannel} ({pattern.ToAction ?? "-"}, hedef: {toDestination ?? "-"}); ilk: {pattern.FirstSeen:yyyy-MM-dd HH:mm}; " +
                        $"son: {pattern.LastSeen:yyyy-MM-dd HH:mm}; max match: {pattern.MaximumMatches}.");
                }
            }
        }

        return text.ToString();
    }

    private static int ExtractTimeWindowHours(string message)
    {
        var match = System.Text.RegularExpressions.Regex.Match(message ?? string.Empty, @"\b(\d{1,2})\s*saat\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return match.Success && int.TryParse(match.Groups[1].Value, out var hours)
            ? Math.Clamp(hours, 1, 24)
            : 6;
    }

    private static IReadOnlyList<CrossChannelPattern> FindCrossChannelPatterns(
        IReadOnlyList<ComprehensiveTimelineEvent> events,
        int timeWindowHours)
    {
        var window = TimeSpan.FromHours(timeWindowHours);
        var patterns = new Dictionary<string, CrossChannelPattern>(StringComparer.Ordinal);

        foreach (var userEvents in events.GroupBy(item => item.User))
        {
            var ordered = userEvents.OrderBy(item => item.Timestamp).ToList();
            for (var currentIndex = 0; currentIndex < ordered.Count; currentIndex++)
            {
                var current = ordered[currentIndex];
                var dataContext = current.Policy ?? current.Rule;
                if (string.IsNullOrWhiteSpace(dataContext) || string.IsNullOrWhiteSpace(current.Channel)) continue;

                for (var previousIndex = currentIndex - 1; previousIndex >= 0; previousIndex--)
                {
                    var previous = ordered[previousIndex];
                    if (current.Timestamp - previous.Timestamp > window) break;
                    if (!string.Equals(dataContext, previous.Policy ?? previous.Rule, StringComparison.OrdinalIgnoreCase) ||
                        string.IsNullOrWhiteSpace(previous.Channel) ||
                        string.Equals(current.Channel, previous.Channel, StringComparison.OrdinalIgnoreCase)) continue;

                    var key = $"{current.User}\u001f{dataContext}\u001f{previous.Channel}\u001f{current.Channel}\u001f{previous.Action}\u001f{current.Action}\u001f{previous.Destination}\u001f{current.Destination}";
                    if (!patterns.TryGetValue(key, out var pattern))
                    {
                        pattern = new CrossChannelPattern(
                            current.User,
                            dataContext,
                            previous.Channel,
                            current.Channel,
                            previous.Action,
                            current.Action,
                            previous.Destination,
                            current.Destination,
                            0,
                            previous.Timestamp,
                            current.Timestamp,
                            0);
                    }

                    patterns[key] = pattern with
                    {
                        OccurrenceCount = pattern.OccurrenceCount + 1,
                        FirstSeen = pattern.FirstSeen < previous.Timestamp ? pattern.FirstSeen : previous.Timestamp,
                        LastSeen = pattern.LastSeen > current.Timestamp ? pattern.LastSeen : current.Timestamp,
                        MaximumMatches = Math.Max(pattern.MaximumMatches, Math.Max(current.MaxMatches, previous.MaxMatches)),
                    };

                    // The closest matching predecessor is the defensible event sequence.
                    // Pairing every earlier incident would inflate the apparent transition count.
                    break;
                }
            }
        }

        return patterns.Values
            .OrderByDescending(pattern => pattern.OccurrenceCount)
            .ThenByDescending(pattern => pattern.MaximumMatches)
            .ToList();
    }

    private async Task<MailDraftPreparationResult> PrepareMailDraftsAsync(Guid conversationId, string ownerUsername, LocalLlmChatRequest request, LocalLlmLabSettings settings, CancellationToken ct)
    {
        var promptHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.Message.Trim()))).ToLowerInvariant();
        var existing = await _context.LocalLlmMailProposals.AnyAsync(item => item.ConversationId == conversationId && item.SourcePromptHash == promptHash, ct);
        if (existing) return MailDraftPreparationResult.Empty;

        var end = request.EndUtc?.ToUniversalTime() ?? DateTime.UtcNow;
        var start = request.StartUtc?.ToUniversalTime() ?? end.AddDays(-Math.Clamp(request.LookbackDays, 1, 365));
        var take = ExtractRequestedUserCount(request.Message);
        var candidates = await _context.Incidents.AsNoTracking()
            .Where(incident => incident.Timestamp >= start && incident.Timestamp <= end && incident.UserEmail != null && incident.UserEmail != "")
            .GroupBy(incident => new { incident.UserEmail, incident.LoginName, incident.EmailAddress, incident.Department })
            .Select(group => new
            {
                User = group.Key.UserEmail,
                group.Key.LoginName,
                group.Key.EmailAddress,
                group.Key.Department,
                IncidentCount = group.Count(),
                MaximumMatches = group.Max(item => item.MaxMatches),
                MaximumSeverity = group.Max(item => item.Severity),
                LatestIncidentAt = group.Max(item => item.Timestamp),
                Policy = group.OrderByDescending(item => item.MaxMatches).Select(item => item.Policy ?? item.RuleName).FirstOrDefault(),
                Destination = group.OrderByDescending(item => item.MaxMatches).Select(item => item.Destination).FirstOrDefault(),
            })
            .OrderByDescending(item => item.MaximumMatches)
            .ThenByDescending(item => item.MaximumSeverity)
            .ThenByDescending(item => item.IncidentCount)
            .Take(take)
            .ToListAsync(ct);

        var aiTemplate = await TryGenerateMailTemplateAsync(settings, request.Message, ct);

        var prepared = 0;
        var unresolved = 0;
        foreach (var candidate in candidates)
        {
            var username = NormalizeDirectoryUsername(candidate.LoginName, candidate.User);
            var ldap = await _directorySettings.LookupLdapUserAsync(username, ct);
            var recipient = ldap.Success && !string.IsNullOrWhiteSpace(ldap.Email)
                ? ldap.Email.Trim()
                : FirstEmail(candidate.EmailAddress, candidate.User);
            var fullName = ldap.Success && !string.IsNullOrWhiteSpace(ldap.FullName) ? ldap.FullName.Trim() : username;
            var department = ldap.Success && !string.IsNullOrWhiteSpace(ldap.Department) ? ldap.Department : candidate.Department;
            var now = DateTime.UtcNow;
            var summary = JsonSerializer.Serialize(new
            {
                incident_start_utc = start,
                incident_end_utc = end,
                incident_count = candidate.IncidentCount,
                maximum_matches = candidate.MaximumMatches,
                maximum_severity = candidate.MaximumSeverity,
                latest_incident_at = candidate.LatestIncidentAt,
                policy = candidate.Policy,
                destination = candidate.Destination,
            });
            var proposal = new LocalLlmMailProposal
            {
                ConversationId = conversationId,
                OwnerUsername = ownerUsername,
                UserName = username,
                FullName = fullName,
                Department = department,
                RecipientEmail = recipient,
                Subject = "Veri Güvenliği Olay Kaydı İncelemeleri Kapsamında",
                Body = BuildMailDraft(aiTemplate, fullName, candidate.IncidentCount, candidate.MaximumMatches, candidate.Policy, candidate.Destination, candidate.LatestIncidentAt),
                IncidentSummaryJson = summary,
                Rationale = $"Son {Math.Clamp(request.LookbackDays, 1, 180)} gündeki yüksek maximum match, şiddet ve olay yoğunluğuna göre önceliklendirildi.",
                SourcePromptHash = promptHash,
                Status = string.IsNullOrWhiteSpace(recipient) ? LocalLlmMailProposalStatus.Unresolved : LocalLlmMailProposalStatus.Pending,
                CreatedAt = now,
                UpdatedAt = now,
            };
            _context.LocalLlmMailProposals.Add(proposal);
            if (proposal.Status == LocalLlmMailProposalStatus.Pending) prepared++;
            else unresolved++;
        }
        await _context.SaveChangesAsync(ct);
        return new MailDraftPreparationResult(prepared, unresolved);
    }

    private async Task<IReadOnlyList<LocalLlmCount>> CountByAsync(IQueryable<string?> source, CancellationToken ct)
    {
        var rows = await source
            .Where(value => value != null && value != "")
            .GroupBy(value => value!)
            .Select(group => new { Name = group.Key, Count = group.Count() })
            .OrderByDescending(item => item.Count)
            .Take(12)
            .ToListAsync(ct);

        return rows.Select(row => new LocalLlmCount(row.Name, row.Count)).ToList();
    }

    private static LocalLlmConversationDetail ToDetail(
        LocalLlmConversation conversation,
        IReadOnlyList<LocalLlmChatMessage> messages) =>
        new(conversation.Id, conversation.Title, conversation.CreatedAt, conversation.UpdatedAt, messages);

    private static LocalLlmMailProposalDto ToProposalDto(LocalLlmMailProposal proposal) =>
        new(proposal.Id, proposal.ConversationId, proposal.UserName, proposal.FullName, proposal.Department,
            proposal.RecipientEmail, proposal.Subject, proposal.Body, proposal.IncidentSummaryJson, proposal.Rationale,
            proposal.Status, proposal.CreatedAt, proposal.UpdatedAt, proposal.SentAt, proposal.ErrorMessage);

    private static bool RequestsMailDraft(string message)
    {
        var normalized = message.ToLowerInvariant();
        return normalized.Contains("mail") &&
            (normalized.Contains("taslak") || normalized.Contains("hazırla") || normalized.Contains("hazirla") || normalized.Contains("oluştur") || normalized.Contains("olustur"));
    }

    private static int ExtractRequestedUserCount(string message)
    {
        var match = System.Text.RegularExpressions.Regex.Match(message, @"\b([1-9]|1\d|20)\s+(?:kullanıcı|kullanici|kişi|kisi)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return match.Success && int.TryParse(match.Groups[1].Value, out var count) ? count : 5;
    }

    private static string NormalizeDirectoryUsername(string? loginName, string user)
    {
        var value = string.IsNullOrWhiteSpace(loginName) ? user : loginName;
        var slash = value.LastIndexOf('\\');
        if (slash >= 0) value = value[(slash + 1)..];
        var at = value.IndexOf('@');
        if (at > 0) value = value[..at];
        return value.Trim();
    }

    private static string? FirstEmail(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value) && value.Contains('@', StringComparison.Ordinal))?.Trim();

    private async Task<string?> TryGenerateMailTemplateAsync(LocalLlmLabSettings settings, string command, CancellationToken ct)
    {
        const string prompt = """
RADAR için Türkçe, kısa ve profesyonel bir olay kaydı inceleme e-posta taslağı yaz.
Bu metin yalnızca insan onayından sonra gönderilecek bir taslaktır; kesin hüküm, suçlama veya gönderildi ifadesi kullanma.
Şu yer tutucuları aynen kullan: {{tam_ad}}, {{olay_sayisi}}, {{max_match}}, {{olay_tarihi}}, {{politika}}, {{destination}}.
Konu satırı yazma, HTML veya Markdown kullanma. Yalnızca mail gövdesini yaz.
Kullanıcı komutu:
""";

        try
        {
            var generated = await GenerateAsync(settings, $"{prompt}\n{command}", 700, ct);
            var body = generated.Trim().Replace("```text", string.Empty, StringComparison.OrdinalIgnoreCase).Replace("```", string.Empty).Trim();
            return body.Length >= 60 && body.Contains("{{tam_ad}}", StringComparison.OrdinalIgnoreCase) ? body : null;
        }
        catch (Exception ex)
        {
            _logger.LogInformation(ex, "Local LLM mail template generation failed; using the controlled fallback draft");
            return null;
        }
    }

    private static string BuildMailDraft(string? aiTemplate, string fullName, int incidentCount, int maximumMatches, string? policy, string? destination, DateTime latestIncidentAt)
    {
        if (!string.IsNullOrWhiteSpace(aiTemplate))
        {
            return aiTemplate
                .Replace("{{tam_ad}}", fullName, StringComparison.OrdinalIgnoreCase)
                .Replace("{{olay_sayisi}}", incidentCount.ToString(), StringComparison.OrdinalIgnoreCase)
                .Replace("{{max_match}}", maximumMatches.ToString(), StringComparison.OrdinalIgnoreCase)
                .Replace("{{olay_tarihi}}", latestIncidentAt.ToLocalTime().ToString("dd.MM.yyyy"), StringComparison.OrdinalIgnoreCase)
                .Replace("{{politika}}", policy ?? "-", StringComparison.OrdinalIgnoreCase)
                .Replace("{{destination}}", destination ?? "-", StringComparison.OrdinalIgnoreCase);
        }

        return $"""
Merhaba {fullName},

Veri Güvenliği süreçleri kapsamında yapılan kontrollerde hesabınızla ilişkili olay kayıtları için inceleme ihtiyacı oluşmuştur.

İncelenen dönem içinde {incidentCount} olay kaydı tespit edilmiştir. En yüksek eşleşme değeri {maximumMatches}, son olay kaydı tarihi {latestIncidentAt.ToLocalTime():dd.MM.yyyy} olarak görülmektedir.
Politika/Kural: {policy ?? "-"}
Hedef: {destination ?? "-"}

İlgili işlemi ve iş gereksinimini açıklamanızı rica ederiz.

Saygılarımızla,
Veri Güvenliği Yönetimi
""";
    }

    private sealed record ComprehensiveTimelineEvent(
        string User,
        DateTime Timestamp,
        string? Action,
        string? Policy,
        string? Rule,
        string? Channel,
        string? Destination,
        int MaxMatches,
        int Severity,
        int DataSensitivity,
        int RepeatCount);

    private sealed record CrossChannelPattern(
        string User,
        string DataContext,
        string FromChannel,
        string ToChannel,
        string? FromAction,
        string? ToAction,
        string? FromDestination,
        string? ToDestination,
        int OccurrenceCount,
        DateTime FirstSeen,
        DateTime LastSeen,
        int MaximumMatches);

    private sealed record MailDraftPreparationResult(int Prepared, int Unresolved)
    {
        public static MailDraftPreparationResult Empty { get; } = new(0, 0);
    }

    private static string NormalizeTitle(string? title)
    {
        var normalized = string.IsNullOrWhiteSpace(title) ? "Yeni sohbet" : title.Trim();
        return normalized[..Math.Min(normalized.Length, 200)];
    }

    private static string TitleFromMessage(string message)
    {
        var firstLine = message.Trim().ReplaceLineEndings(" ");
        return NormalizeTitle(firstLine[..Math.Min(firstLine.Length, 80)]);
    }

    private static string? Truncate(string? value, int length) =>
        string.IsNullOrWhiteSpace(value) ? value : value.Length <= length ? value : $"{value[..length]}...";

    private async Task<string> GenerateAsync(LocalLlmLabSettings settings, string prompt, int maxTokens, CancellationToken ct)
    {
        const int maxAttempts = 2;
        LocalLlmConnectionException? lastFailure = null;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                using var request = CreateGenerateRequest(settings, prompt, maxTokens);
                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
                var body = await response.Content.ReadAsStringAsync(ct);
                if (!response.IsSuccessStatusCode)
                {
                    var failure = FromHttpFailure(response.StatusCode, body);
                    _logger.LogWarning("Local LLM returned {StatusCode} ({Code})", response.StatusCode, failure.Code);
                    if (attempt < maxAttempts && failure.Retryable)
                    {
                        lastFailure = failure;
                        await Task.Delay(TimeSpan.FromMilliseconds(350 * attempt), ct);
                        continue;
                    }
                    throw failure;
                }

                try
                {
                    using var document = JsonDocument.Parse(body);
                    var reply = document.RootElement.TryGetProperty("response", out var responseText)
                        ? responseText.GetString()?.Trim()
                        : null;
                    if (!string.IsNullOrWhiteSpace(reply)) return reply;
                    throw new LocalLlmConnectionException("empty_response", "Yerel model boş veya beklenen formatta olmayan bir yanıt verdi.",
                        "Modelin Ollama uyumlu /api/generate uç noktasını kullandığını ve model loglarını kontrol edin.", true, StatusCodes.Status502BadGateway);
                }
                catch (JsonException ex)
                {
                    throw new LocalLlmConnectionException("invalid_response", "Yerel model geçerli JSON döndürmedi.",
                        "Generate URL'nin Ollama uyumlu /api/generate uç noktasını gösterdiğini kontrol edin.", false, StatusCodes.Status502BadGateway, ex);
                }
            }
            catch (LocalLlmConnectionException ex) when (attempt < maxAttempts && ex.Retryable)
            {
                lastFailure = ex;
                await Task.Delay(TimeSpan.FromMilliseconds(350 * attempt), ct);
            }
            catch (HttpRequestException ex)
            {
                lastFailure = new LocalLlmConnectionException("unreachable", "Yerel LLM sunucusuna ulaşılamadı.",
                    "Sunucunun çalıştığını, Generate URL'nin Analyzer makinesinden erişildiğini ve güvenlik duvarını kontrol edin.", true, StatusCodes.Status503ServiceUnavailable, ex);
                if (attempt < maxAttempts)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(350 * attempt), ct);
                    continue;
                }
            }
            catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
            {
                lastFailure = new LocalLlmConnectionException("timeout", "Yerel model zaman aşımına uğradı.",
                    "Daha küçük kapsam seçin, modelin yüklü olduğundan emin olun veya yerel model kapasitesini kontrol edin.", true, StatusCodes.Status504GatewayTimeout, ex);
                if (attempt < maxAttempts)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(350 * attempt), ct);
                    continue;
                }
            }
        }

        throw lastFailure ?? new LocalLlmConnectionException("unknown", "Yerel LLM yanıtı alınamadı.",
            "Bağlantı testini çalıştırıp yerel model ayarlarını kontrol edin.", true, StatusCodes.Status502BadGateway);
    }

    private static HttpRequestMessage CreateGenerateRequest(LocalLlmLabSettings settings, string prompt, int maxTokens) =>
        new(HttpMethod.Post, settings.GenerateUrl)
        {
            Content = JsonContent.Create(new
            {
                model = settings.Model,
                prompt,
                stream = false,
                options = new { temperature = settings.Temperature, num_predict = Math.Clamp(maxTokens, 64, 4096) },
            }),
        };

    private static LocalLlmConnectionException FromHttpFailure(System.Net.HttpStatusCode statusCode, string responseBody)
    {
        _ = responseBody; // Do not expose remote response bodies to the UI or logs.
        var status = (int)statusCode;
        if (status == StatusCodes.Status404NotFound)
            return new LocalLlmConnectionException("model_or_endpoint_not_found", "Yerel model veya generate uç noktası bulunamadı.",
                "Model adını `ollama list` ile doğrulayın; URL genellikle http://sunucu:11434/api/generate olur.", false, StatusCodes.Status502BadGateway);
        if (status is StatusCodes.Status429TooManyRequests or StatusCodes.Status502BadGateway or StatusCodes.Status503ServiceUnavailable or StatusCodes.Status504GatewayTimeout)
            return new LocalLlmConnectionException("model_busy", "Yerel model isteği geçici olarak işleyemedi.",
                "Kısa süre sonra tekrar deneyin; eşzamanlı istekleri ve model kaynak kullanımını kontrol edin.", true, StatusCodes.Status503ServiceUnavailable);

        return new LocalLlmConnectionException("model_rejected_request", $"Yerel LLM isteği başarısız oldu ({status}).",
            "Generate URL, model adı ve yerel model günlüklerini kontrol edin.", false, StatusCodes.Status502BadGateway);
    }

    private static string BuildPrompt(LocalLlmChatRequest request, LocalLlmIncidentSnapshot snapshot, string? comprehensiveEvidence = null)
    {
        var history = (request.History ?? [])
            .Where(message => message.Role is "user" or "assistant" && !string.IsNullOrWhiteSpace(message.Content))
            .TakeLast(10)
            .Select(message => $"{(message.Role == "user" ? "Kullanıcı" : "Asistan")}: {message.Content.Trim()[..Math.Min(message.Content.Trim().Length, 3000)]}");
        var context = FormatSnapshot(snapshot);

        return $$"""
Sen RADAR içindeki yerel DLP risk modelleme laboratuvarının analist asistanısın.
Kullanıcıyla doğal, kısa ve yardımcı bir Türkçe sohbet yürüt. Selamlaşmaya selamlaşarak karşılık ver;
her mesajda risk algoritması üretme. Yalnızca kullanıcı risk puanlama, sınıflandırma veya incident analizi isterse
bu konulara geç. Sana yalnızca salt-okunur incident özeti verildi; bu veride olmayan olguları kesinmiş gibi iddia etme.
RADAR'ın mevcut risk_score alanı bu bağlama bilinçli olarak dahil edilmemiştir; onu kullanma veya varsayma.
Bir risk puanlama algoritması istendiğinde 0-100 aralığı, ölçülebilir faktörler, ağırlık toplamı, eşikler,
doğrulama adımları ve sınırlılıkları öner. Kullanıcı sınıflandırmasını yalnızca ham olay özellikleri,
yoğunluk, tekrar, max match, şiddet, veri hassasiyeti, aksiyon ve dağılımlardan türet.
Kullanıcı dar kapsamlı bir davranış veya zaman penceresi isterse, kapsamlı kanıttaki zaman çizelgesini ve çapraz kanal desenlerini özellikle test et.
Doğrudan kanıt bulunmuyorsa bunu açıkça belirt; eksik kanıtı varsayımla tamamlama.
Farklı kanal veya hedefe ait iki olay, tek başına aynı verinin tekrar gönderildiğini, engelin aşıldığını veya nedensel bir akışı kanıtlamaz.
Bu tür bir çıkarımı yalnızca kanıt satırında önceki/sonraki olay, aksiyon ve hedef birlikte destekliyorsa yap; aksi halde "ilişkili olabilir" olarak ifade et.
Üretim kuralı değiştirme, kullanıcıya işlem uygulama veya SQL üretme.
Yanıtlarını Türkçe, denetlenebilir ve kısa başlıklarla yaz. Kullanıcı açıkça JSON istemedikçe JSON,
JSON şeması, kod bloğu veya yalnızca yapılandırılmış veri döndürme.

INCIDENT BAĞLAMI (yalnızca ilgili olduğunda kullan):
{{context}}

{{(string.IsNullOrWhiteSpace(comprehensiveEvidence) ? string.Empty : comprehensiveEvidence)}}

{{string.Join("\n", history)}}
Kullanıcı: {{request.Message.Trim()}}
Asistan:
""";
    }

    private static string FormatSnapshot(LocalLlmIncidentSnapshot snapshot)
    {
        var text = new StringBuilder()
            .AppendLine($"Dönem: {snapshot.StartUtc:yyyy-MM-dd} - {snapshot.EndUtc:yyyy-MM-dd} (UTC)")
            .AppendLine($"Toplam olay: {snapshot.TotalIncidents}; benzersiz kullanıcı: {snapshot.UniqueUsers}; en yüksek maximum match: {snapshot.MaximumMatches}.")
            .AppendLine($"Model bağlamına alınan öncelikli kullanıcı profili: {snapshot.ProfileCount}.")
            .AppendLine($"Aksiyon dağılımı: {FormatCounts(snapshot.Actions)}")
            .AppendLine($"Kanal dağılımı: {FormatCounts(snapshot.Channels)}")
            .AppendLine($"Politika dağılımı: {FormatCounts(snapshot.Policies)}")
            .AppendLine("Öne çıkan kullanıcı profilleri:");

        foreach (var user in snapshot.Users)
        {
            text.AppendLine($"- Kullanıcı: {user.User}; birim: {user.Department ?? "-"}; olay: {user.IncidentCount}; " +
                $"max match: {user.MaximumMatches}; en yüksek şiddet: {user.MaximumSeverity}; tekrar: {user.RepeatCount}; " +
                $"ortalama veri hassasiyeti: {user.AverageDataSensitivity:F1}.");
        }

        text.AppendLine("Örnek olaylar:");
        foreach (var incident in snapshot.Samples)
        {
            text.AppendLine($"- {incident.Timestamp:yyyy-MM-dd HH:mm}; kullanıcı: {incident.User}; aksiyon: {incident.Action ?? "-"}; " +
                $"max match: {incident.MaxMatches}; şiddet: {incident.Severity}; tekrar: {incident.RepeatCount}; " +
                $"politika: {incident.Policy ?? "-"}; kural: {incident.Rule ?? "-"}; kanal: {incident.Channel ?? "-"}; hedef: {incident.Destination ?? "-"}.");
        }

        return text.ToString();
    }

    private static string FormatCounts(IReadOnlyList<LocalLlmCount> counts) =>
        counts.Count == 0 ? "veri yok" : string.Join(", ", counts.Select(item => $"{item.Name}: {item.Count}"));

    private Task EnsureMailProposalStorageAsync(CancellationToken ct) =>
        LocalLlmConversationSchema.EnsureAsync(_context, _logger, ct);

    private static void ValidateSettings(LocalLlmLabSettings settings)
    {
        if (!Uri.TryCreate(settings.GenerateUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            throw new ArgumentException("Geçerli bir http/https generate URL girin.");
        if (string.IsNullOrWhiteSpace(settings.Model)) throw new ArgumentException("Model adı zorunludur.");
        if (settings.Temperature is < 0 or > 2) throw new ArgumentException("Sıcaklık 0 ile 2 arasında olmalıdır.");
        if (settings.MaxTokens is < 64 or > 4096) throw new ArgumentException("Maksimum çıktı 64 ile 4096 arasında olmalıdır.");
    }

    private static string Mask(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "-";
        var at = value.IndexOf('@');
        if (at > 0) return $"{value[..1]}***{value[at..]}";
        return value.Length <= 3 ? "***" : $"{value[..2]}***";
    }

    private static string? MaskDestination(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return value;
        var at = value.LastIndexOf('@');
        return at > 0 ? $"***{value[at..]}" : value;
    }
}
