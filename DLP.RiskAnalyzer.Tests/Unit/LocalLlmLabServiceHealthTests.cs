using System.Net;
using System.Text;
using DLP.RiskAnalyzer.Analyzer.Data;
using DLP.RiskAnalyzer.Analyzer.Models;
using DLP.RiskAnalyzer.Analyzer.Services;
using DLP.RiskAnalyzer.Shared.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DLP.RiskAnalyzer.Tests.Unit;

public class LocalLlmLabServiceHealthTests
{
    private static readonly LocalLlmLabSettings Settings = new(true, "http://127.0.0.1:11434/api/generate", "qwen3:8b", 0.2, 300);

    [Fact]
    public async Task TestConnectionAsync_OllamaResponse_ReturnsHealthyResult()
    {
        string? requestBody = null;
        var service = CreateService(request =>
        {
            requestBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"response\":\"bağlantı başarılı\"}", Encoding.UTF8, "application/json")
            };
        });

        var result = await service.TestConnectionAsync(Settings, CancellationToken.None);

        result.Healthy.Should().BeTrue();
        result.Status.Should().Be("healthy");
        result.Reply.Should().Be("bağlantı başarılı");
        requestBody.Should().Contain("\"think\":false");
        requestBody.Should().Contain("\"num_predict\":256");
    }

    [Fact]
    public async Task TestConnectionAsync_ThinkingBudgetExhausted_ReturnsSpecificGuidance()
    {
        var service = CreateService(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"response\":\"\",\"thinking\":\"analiz ediyorum\",\"done_reason\":\"length\"}", Encoding.UTF8, "application/json")
        });

        var result = await service.TestConnectionAsync(Settings, CancellationToken.None);

        result.Healthy.Should().BeFalse();
        result.Status.Should().Be("thinking_budget_exhausted");
        result.SuggestedAction.Should().Contain("think=false");
    }

    [Fact]
    public async Task TestConnectionAsync_NotFound_ReturnsModelOrEndpointGuidance()
    {
        var service = CreateService(_ => new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("model not found")
        });

        var result = await service.TestConnectionAsync(Settings, CancellationToken.None);

        result.Healthy.Should().BeFalse();
        result.Status.Should().Be("model_or_endpoint_not_found");
        result.Retryable.Should().BeFalse();
        result.SuggestedAction.Should().Contain("ollama list");
    }

    [Fact]
    public async Task TestConnectionAsync_UnreachableServer_RetriesOnceAndReturnsActionableResult()
    {
        var calls = 0;
        var service = CreateService(_ =>
        {
            calls++;
            throw new HttpRequestException("connection refused");
        });

        var result = await service.TestConnectionAsync(Settings, CancellationToken.None);

        calls.Should().Be(2);
        result.Healthy.Should().BeFalse();
        result.Status.Should().Be("unreachable");
        result.Retryable.Should().BeTrue();
        result.SuggestedAction.Should().Contain("güvenlik duvarını");
    }

    [Fact]
    public async Task TestConnectionAsync_InvalidJson_ReturnsNonRetryableProtocolGuidance()
    {
        var service = CreateService(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("not-json", Encoding.UTF8, "application/json")
        });

        var result = await service.TestConnectionAsync(Settings, CancellationToken.None);

        result.Healthy.Should().BeFalse();
        result.Status.Should().Be("invalid_response");
        result.Retryable.Should().BeFalse();
    }

    [Fact]
    public async Task ChatAsync_CasualGreeting_DoesNotRequireEnabledModelOrIncidentSnapshot()
    {
        var service = CreateService(_ => throw new InvalidOperationException("The model must not be called for a casual greeting."));

        var result = await service.ChatAsync(new LocalLlmChatRequest("Naber?", null), "test-user", CancellationToken.None);

        result.Reply.Should().Contain("İyiyim");
        result.Snapshot.TotalIncidents.Should().Be(0);
        result.Snapshot.Users.Should().BeEmpty();
    }

    [Fact]
    public async Task ChatAsync_ComprehensiveRequest_EnforcesBoundedEvidencePrompt()
    {
        string? capturedPrompt = null;
        var service = CreateService(request =>
        {
            capturedPrompt = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"response\":\"incelendi\"}", Encoding.UTF8, "application/json")
            };
        }, out var context);
        context.SystemSettings.AddRange(
            new SystemSetting { Key = "local_llm_lab_enabled", Value = "true" },
            new SystemSetting { Key = "local_llm_lab_generate_url", Value = Settings.GenerateUrl },
            new SystemSetting { Key = "local_llm_lab_model", Value = Settings.Model },
            new SystemSetting { Key = "local_llm_lab_temperature", Value = "0.2" },
            new SystemSetting { Key = "local_llm_lab_max_tokens", Value = "300" });
        var now = DateTime.UtcNow;
        for (var user = 0; user < 10; user++)
        for (var item = 0; item < 60; item++)
            context.Incidents.Add(new Incident
            {
                UserEmail = $"user{user}@kuveytturk.com.tr",
                Timestamp = now.AddMinutes(-(user * 60 + item)),
                Policy = $"Policy-{item}",
                RuleName = $"Rule-{item}",
                Channel = item % 2 == 0 ? "EMAIL" : "HTTPS",
                Destination = $"target{item}@example.com",
                Action = "BLOCK",
                MaxMatches = item + 1,
                Severity = item % 5,
                DataSensitivity = item % 4,
                RepeatCount = item % 3
            });
        await context.SaveChangesAsync();

        await service.ChatAsync(new LocalLlmChatRequest("Son 7 gün risk analizi yap", null,
            LookbackDays: 7, Comprehensive: true, DetailedUserLimit: 50, EvidenceRowsPerUser: 500), "test-user", CancellationToken.None);

        capturedPrompt.Should().NotBeNull();
        capturedPrompt!.Length.Should().BeLessThan(50_000);
        capturedPrompt.Should().Contain("KAPSAMLI KULLANICI KANITI");
    }

    [Fact]
    public async Task ChatAsync_MailDraft_UsesCompatibleSavedTemplateWithoutCallingModelAgain()
    {
        var calls = 0;
        var service = CreateService(_ =>
        {
            calls++;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"response\":\"incelendi\"}", Encoding.UTF8, "application/json")
            };
        }, out var context);
        EnableLocalModel(context);
        var now = DateTime.UtcNow;
        context.MailTemplates.Add(new MailTemplate
        {
            Name = "GitHub olay bildirimi",
            Subject = "GitHub incelemesi - {{tam_ad}}",
            Body = "Merhaba {{tam_ad}}, {{destination}} hedefine ilişkin {{olay_sayisi}} olay kaydı için açıklama rica ederiz.",
            CreatedAt = now,
            UpdatedAt = now
        });
        context.Incidents.Add(new Incident
        {
            UserEmail = "deniz@kuveytturk.com.tr",
            EmailAddress = "deniz@kuveytturk.com.tr",
            Timestamp = now.AddHours(-1),
            Policy = "Kritik veri",
            Destination = "business.github.com",
            MaxMatches = 9,
            Severity = 4
        });
        await context.SaveChangesAsync();

        var result = await service.ChatAsync(new LocalLlmChatRequest("Son 7 gün için 1 kullanıcı mail taslağı hazırla", null, LookbackDays: 7), "test-user", CancellationToken.None);

        result.MailDraftsPrepared.Should().Be(1);
        calls.Should().Be(1);
        var proposal = context.LocalLlmMailProposals.Single();
        proposal.TemplateOrigin.Should().Be(LocalLlmMailTemplateOrigin.Saved);
        proposal.SourceTemplateName.Should().Be("GitHub olay bildirimi");
        proposal.Subject.Should().Contain("deniz");
        proposal.Body.Should().Contain("business.github.com");
    }

    [Fact]
    public async Task ChatAsync_MailDraft_OffersNewTemplateWhenNoSavedTemplateFits()
    {
        var calls = 0;
        var service = CreateService(_ =>
        {
            calls++;
            var response = calls == 1
                ? "{\"response\":\"incelendi\"}"
                : "{\"response\":\"{\\\"subject\\\":\\\"Yeni inceleme\\\",\\\"body\\\":\\\"Merhaba {{tam_ad}}, olay kaydı bağlamını ve ilgili iş gerekçesini paylaşmanızı rica ederiz. Bu taslak insan onayı sonrasında değerlendirilecektir.\\\"}\"}";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(response, Encoding.UTF8, "application/json")
            };
        }, out var context);
        EnableLocalModel(context);
        var now = DateTime.UtcNow;
        context.Incidents.Add(new Incident
        {
            UserEmail = "ayse@kuveytturk.com.tr",
            EmailAddress = "ayse@kuveytturk.com.tr",
            Timestamp = now.AddHours(-1),
            Policy = "Özel politika",
            Destination = "unknown.example",
            MaxMatches = 6,
            Severity = 3
        });
        await context.SaveChangesAsync();

        var result = await service.ChatAsync(new LocalLlmChatRequest("Son 7 gün için 1 kullanıcı mail taslağı hazırla", null, LookbackDays: 7), "test-user", CancellationToken.None);

        result.MailDraftsPrepared.Should().Be(1);
        calls.Should().Be(2);
        var proposal = context.LocalLlmMailProposals.Single();
        proposal.TemplateOrigin.Should().Be(LocalLlmMailTemplateOrigin.Suggested);
        proposal.SourceTemplateId.Should().BeNull();
        proposal.SourceTemplateName.Should().Be("Yeni LLM şablon önerisi");
        proposal.Subject.Should().Be("Yeni inceleme");
        proposal.Body.Should().Contain("ayse");
    }

    [Fact]
    public async Task RejectMailProposalAsync_RecordsReviewerAndDecision()
    {
        var service = CreateService(_ => throw new InvalidOperationException("The model must not be called."), out var context);
        var proposal = new LocalLlmMailProposal
        {
            ConversationId = Guid.NewGuid(),
            OwnerUsername = "test-user",
            UserName = "deniz",
            RecipientEmail = "deniz@kuveytturk.com.tr",
            Subject = "İnceleme",
            Body = "Taslak",
            SourcePromptHash = "test",
            Status = LocalLlmMailProposalStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.LocalLlmMailProposals.Add(proposal);
        await context.SaveChangesAsync();

        var result = await service.RejectMailProposalAsync(proposal.Id, "test-user", CancellationToken.None);

        result.Should().NotBeNull();
        result!.Status.Should().Be(LocalLlmMailProposalStatus.Rejected);
        result.ReviewedBy.Should().Be("test-user");
        result.ReviewDecision.Should().Be("rejected");
        result.ReviewedAt.Should().NotBeNull();
    }

    private static LocalLlmLabService CreateService(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) =>
        CreateService(responseFactory, out _);

    private static LocalLlmLabService CreateService(Func<HttpRequestMessage, HttpResponseMessage> responseFactory, out AnalyzerDbContext context)
    {
        var options = new DbContextOptionsBuilder<AnalyzerDbContext>()
            .UseInMemoryDatabase($"local-llm-health-{Guid.NewGuid():N}")
            .Options;
        var httpClient = new HttpClient(new StubHttpMessageHandler(responseFactory));
        context = new AnalyzerDbContext(options);
        var directory = new Mock<IDirectorySettingsService>();
        directory.Setup(service => service.LookupLdapUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string username, CancellationToken _) => new LdapUserLookupResult
            {
                Success = false,
                Username = username,
                Message = "LDAP test dışı"
            });
        return new LocalLlmLabService(
            context,
            httpClient,
            directory.Object,
            new Mock<IEmailService>().Object,
            NullLogger<LocalLlmLabService>.Instance);
    }

    private static void EnableLocalModel(AnalyzerDbContext context)
    {
        context.SystemSettings.AddRange(
            new SystemSetting { Key = "local_llm_lab_enabled", Value = "true" },
            new SystemSetting { Key = "local_llm_lab_generate_url", Value = Settings.GenerateUrl },
            new SystemSetting { Key = "local_llm_lab_model", Value = Settings.Model },
            new SystemSetting { Key = "local_llm_lab_temperature", Value = "0.2" },
            new SystemSetting { Key = "local_llm_lab_max_tokens", Value = "300" });
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(responseFactory(request));
    }
}
