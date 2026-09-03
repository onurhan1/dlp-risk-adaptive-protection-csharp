using System.Collections.Concurrent;
using DLP.RiskAnalyzer.Analyzer.Models;
using DLP.RiskAnalyzer.Analyzer.Services;

namespace DLP.RiskAnalyzer.Analyzer.Helpers;

/// <summary>
/// Bir kullanici listesi icin LDAP profillerini tekillestirerek ve sinirli paralellikle toplar.
/// Onceden cagiricilar her kullaniciyi tek tek ve sirayla soruyordu; bu yuzden istek suresi
/// kullanici sayisiyla dogru orantili buyuyor ve buyuk listelerde sayfa acilmiyordu.
/// </summary>
public static class DirectoryProfileLoader
{
    /// <summary>Ayni anda acilabilecek LDAP baglanti sayisi.</summary>
    public const int DefaultConcurrency = 8;

    /// <summary>
    /// Verilen kullanici anahtarlari icin basarili LDAP profillerini doner.
    /// Bulunamayan veya hata alan kullanicilar sozlukte yer almaz.
    /// </summary>
    public static async Task<IReadOnlyDictionary<string, LdapUserLookupResult>> LoadAsync(
        IDirectorySettingsService directorySettings,
        IEnumerable<string?> userKeys,
        ILogger? logger = null,
        int concurrency = DefaultConcurrency,
        CancellationToken ct = default)
    {
        var distinctKeys = userKeys
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Select(key => key!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var profiles = new ConcurrentDictionary<string, LdapUserLookupResult>(StringComparer.OrdinalIgnoreCase);
        if (distinctKeys.Count == 0)
            return profiles;

        using var gate = new SemaphoreSlim(concurrency, concurrency);

        var tasks = distinctKeys.Select(async key =>
        {
            await gate.WaitAsync(ct);
            try
            {
                var lookup = await directorySettings.LookupLdapUserAsync(key, ct);
                if (lookup.Success)
                    profiles[key] = lookup;
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Directory lookup failed for {UserKey}", key);
            }
            finally
            {
                gate.Release();
            }
        });

        await Task.WhenAll(tasks);
        return profiles;
    }
}
