using System.Net;
using System.Text;
using DLP.RiskAnalyzer.Analyzer.Data;
using DLP.RiskAnalyzer.Analyzer.Services;
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
        var service = CreateService(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"response\":\"bağlantı başarılı\"}", Encoding.UTF8, "application/json")
        });

        var result = await service.TestConnectionAsync(Settings, CancellationToken.None);

        result.Healthy.Should().BeTrue();
        result.Status.Should().Be("healthy");
        result.Reply.Should().Be("bağlantı başarılı");
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

    private static LocalLlmLabService CreateService(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
    {
        var options = new DbContextOptionsBuilder<AnalyzerDbContext>()
            .UseInMemoryDatabase($"local-llm-health-{Guid.NewGuid():N}")
            .Options;
        var httpClient = new HttpClient(new StubHttpMessageHandler(responseFactory));
        return new LocalLlmLabService(
            new AnalyzerDbContext(options),
            httpClient,
            new Mock<IDirectorySettingsService>().Object,
            new Mock<IEmailService>().Object,
            NullLogger<LocalLlmLabService>.Instance);
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(responseFactory(request));
    }
}
