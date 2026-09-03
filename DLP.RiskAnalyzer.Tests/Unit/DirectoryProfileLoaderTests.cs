using DLP.RiskAnalyzer.Analyzer.Helpers;
using DLP.RiskAnalyzer.Analyzer.Models;
using DLP.RiskAnalyzer.Analyzer.Services;
using FluentAssertions;
using Moq;

namespace DLP.RiskAnalyzer.Tests.Unit;

/// <summary>
/// Unit tests for DirectoryProfileLoader.
/// The loader replaced a per-user sequential LDAP loop that made the incident list and
/// weekly-flag endpoints scale linearly with user count. These tests pin the three
/// properties that fix depends on: de-duplication, bounded concurrency, and failure isolation.
/// </summary>
public class DirectoryProfileLoaderTests
{
    private static LdapUserLookupResult Success(string username) => new()
    {
        Success = true,
        Username = username,
        FullName = "Ad Soyad",
        Email = username,
        Department = "IT"
    };

    private static LdapUserLookupResult Failure(string username) => new()
    {
        Success = false,
        Username = username,
        Message = "LDAP kullanicisi bulunamadi"
    };

    [Fact]
    public async Task LoadAsync_DuplicateAndEmptyKeys_QueriesEachUserOnce()
    {
        var directory = new Mock<IDirectorySettingsService>();
        directory
            .Setup(d => d.LookupLdapUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string user, CancellationToken _) => Success(user));

        var result = await DirectoryProfileLoader.LoadAsync(
            directory.Object,
            new[] { "a@kt.com.tr", "A@KT.COM.TR", "b@kt.com.tr", null, "", "   " });

        result.Should().HaveCount(2);
        directory.Verify(
            d => d.LookupLdapUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task LoadAsync_UnsuccessfulOrThrowingLookup_IsSkippedWithoutFailingTheBatch()
    {
        var directory = new Mock<IDirectorySettingsService>();
        directory
            .Setup(d => d.LookupLdapUserAsync("ok@kt.com.tr", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Success("ok@kt.com.tr"));
        directory
            .Setup(d => d.LookupLdapUserAsync("missing@kt.com.tr", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Failure("missing@kt.com.tr"));
        directory
            .Setup(d => d.LookupLdapUserAsync("broken@kt.com.tr", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("LDAP baglantisi koptu"));

        var result = await DirectoryProfileLoader.LoadAsync(
            directory.Object,
            new[] { "ok@kt.com.tr", "missing@kt.com.tr", "broken@kt.com.tr" });

        result.Should().ContainKey("ok@kt.com.tr");
        result.Should().NotContainKey("missing@kt.com.tr");
        result.Should().NotContainKey("broken@kt.com.tr");
    }

    [Fact]
    public async Task LoadAsync_ManyUsers_NeverExceedsConfiguredConcurrency()
    {
        const int concurrency = 4;
        var inFlight = 0;
        var observedPeak = 0;
        var peakLock = new object();

        var directory = new Mock<IDirectorySettingsService>();
        directory
            .Setup(d => d.LookupLdapUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async (string user, CancellationToken _) =>
            {
                var current = Interlocked.Increment(ref inFlight);
                lock (peakLock)
                {
                    if (current > observedPeak) observedPeak = current;
                }

                await Task.Delay(5);
                Interlocked.Decrement(ref inFlight);
                return Success(user);
            });

        var users = Enumerable.Range(0, 50).Select(i => $"user{i}@kt.com.tr").ToArray();

        var result = await DirectoryProfileLoader.LoadAsync(
            directory.Object, users, logger: null, concurrency: concurrency);

        result.Should().HaveCount(50);
        observedPeak.Should().BeLessThanOrEqualTo(concurrency);
        observedPeak.Should().BeGreaterThan(1, "aramalar paralel calismali");
    }

    [Fact]
    public async Task LoadAsync_NoUsableKeys_ReturnsEmptyWithoutQueryingDirectory()
    {
        var directory = new Mock<IDirectorySettingsService>();

        var result = await DirectoryProfileLoader.LoadAsync(directory.Object, new string?[] { null, "", "  " });

        result.Should().BeEmpty();
        directory.Verify(
            d => d.LookupLdapUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
