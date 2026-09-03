using System.Net.Security;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using DLP.RiskAnalyzer.Analyzer.Data;
using DLP.RiskAnalyzer.Analyzer.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace DLP.RiskAnalyzer.Analyzer.Services;

public class DirectorySettingsService : IDirectorySettingsService
{
    static DirectorySettingsService()
    {
        // IMAP messages still commonly use legacy Turkish MIME charsets such as ISO-8859-9.
        // .NET does not enable these code pages by default on all deployments.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    private const string ImapPrefix = "imap_";
    private const string ImapEnabledKey = "imap_enabled";
    private const string ImapHostKey = "imap_host";
    private const string ImapPortKey = "imap_port";
    private const string ImapSslKey = "imap_enable_ssl";
    private const string ImapUsernameKey = "imap_username";
    private const string ImapPasswordKey = "imap_password_protected";
    private const string ImapFolderKey = "imap_folder";
    private const string ImapUnreadOnlyKey = "imap_unread_only";
    private const string ImapLookbackDaysKey = "imap_lookback_days";
    private const string ImapMaxMessagesKey = "imap_max_messages";
    private const int ImapMessagePreviewBytes = 200_000;
    private const int ImapFullMessageMaxBytes = 25 * 1024 * 1024;

    private const string LdapPrefix = "ldap_";
    private const string LdapEnabledKey = "ldap_enabled";
    private const string LdapUseLdapsKey = "ldap_use_ldaps";
    private const string LdapHostKey = "ldap_host";
    private const string LdapPortKey = "ldap_port";
    private const string LdapDomainKey = "ldap_domain";
    private const string LdapSearchBaseKey = "ldap_search_base";
    private const string LdapServiceAccountKey = "ldap_service_account";
    private const string LdapServicePasswordKey = "ldap_service_password_protected";
    private static readonly TimeSpan LdapResponseTimeout = TimeSpan.FromSeconds(30);

    // LDAP arama sonuclari cache'lenir: incident/haftalik sorgu listeleri ayni kullanicilari
    // her istekte tekrar tekrar sorguluyordu ve her sorgu yeni bir TCP + bind + search demekti.
    private const string LdapSettingsCacheKey = "directory:ldap:settings";
    private const string LdapServicePasswordCacheKey = "directory:ldap:service-password";
    private const string LdapUserCacheKeyPrefix = "directory:ldap:user:";
    private static readonly TimeSpan LdapSettingsCacheTtl = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan LdapUserHitCacheTtl = TimeSpan.FromMinutes(60);
    private static readonly TimeSpan LdapUserMissCacheTtl = TimeSpan.FromMinutes(10);

    // LDAP ayarlari degistiginde onbellekteki tum kullanici kayitlarini gecersiz kilar.
    private static long _ldapCacheGeneration;

    private readonly AnalyzerDbContext _context;
    private readonly IDataProtector _protector;
    private readonly IMemoryCache _cache;
    private readonly ILogger<DirectorySettingsService> _logger;

    // DbContext thread-safe degil; paralel LDAP aramalarinda ayar/parola okumalarini seri hale getirir.
    private readonly SemaphoreSlim _settingsReadGate = new(1, 1);

    public DirectorySettingsService(
        AnalyzerDbContext context,
        IDataProtectionProvider dataProtectionProvider,
        IMemoryCache cache,
        ILogger<DirectorySettingsService> logger)
    {
        _context = context;
        _protector = dataProtectionProvider.CreateProtector("Directory.SettingsProtector");
        _cache = cache;
        _logger = logger;
    }

    public async Task<ImapSettingsResponse> GetImapAsync(CancellationToken ct = default)
    {
        var dict = await LoadAsync(ImapPrefix, ct);
        return BuildImapResponse(dict);
    }

    public async Task<ImapSettingsResponse> SaveImapAsync(ImapSettingsRequest request, CancellationToken ct = default)
    {
        ValidateImap(request, allowEmptyPassword: true);

        await UpsertAsync(ImapEnabledKey, request.Enabled.ToString(), ct);
        await UpsertAsync(ImapHostKey, request.Host.Trim(), ct);
        await UpsertAsync(ImapPortKey, request.Port.ToString(), ct);
        await UpsertAsync(ImapSslKey, request.EnableSsl.ToString(), ct);
        await UpsertAsync(ImapUsernameKey, request.Username.Trim(), ct);
        await UpsertAsync(ImapFolderKey, string.IsNullOrWhiteSpace(request.Folder) ? "INBOX" : request.Folder.Trim(), ct);
        await UpsertAsync(ImapUnreadOnlyKey, request.UnreadOnly.ToString(), ct);
        await UpsertAsync(ImapLookbackDaysKey, Clamp(request.LookbackDays, 1, 365).ToString(), ct);
        await UpsertAsync(ImapMaxMessagesKey, Clamp(request.MaxMessages, 1, 10000).ToString(), ct);

        if (!string.IsNullOrWhiteSpace(request.Password))
            await UpsertAsync(ImapPasswordKey, _protector.Protect(request.Password), ct);

        return await GetImapAsync(ct);
    }

    public async Task<DirectorySettingsTestResult> TestImapAsync(ImapSettingsRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Password))
            request.Password = await GetSecretAsync(ImapPasswordKey, ct);

        ValidateImap(request, allowEmptyPassword: false);

        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(request.Host.Trim(), request.Port, ct);
            using var stream = request.EnableSsl
                ? await WrapSslAsync(client, request.Host.Trim(), ct)
                : client.GetStream();

            var greeting = await ReadImapAsync(stream, ct);
            if (!greeting.Contains("* OK", StringComparison.OrdinalIgnoreCase))
                return Result(false, $"IMAP sunucusu beklenen acilis yanitini vermedi: {Shorten(greeting)}");

            var username = EscapeImap(request.Username.Trim());
            var password = EscapeImap(request.Password ?? string.Empty);
            await WriteAsync(stream, $"A001 LOGIN \"{username}\" \"{password}\"\r\n", ct);
            var login = await ReadImapAsync(stream, ct, "A001");
            var ok = login.Contains("A001 OK", StringComparison.OrdinalIgnoreCase);

            await WriteAsync(stream, "A002 LOGOUT\r\n", ct);

            return ok
                ? Result(true, "IMAP baglantisi ve kimlik dogrulama basarili")
                : Result(false, $"IMAP kimlik dogrulama basarisiz: {Shorten(login)}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "IMAP connection test failed");
            return Result(false, $"IMAP testi basarisiz: {ex.Message}");
        }
    }

    public async Task<ImapInboxPreviewResponse> PreviewInboxAsync(ImapInboxRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Password))
            request.Password = await GetSecretAsync(ImapPasswordKey, ct);

        ValidateImap(request, allowEmptyPassword: false);

        var folder = string.IsNullOrWhiteSpace(request.Folder) ? "INBOX" : request.Folder.Trim();
        var limit = Clamp(request.PreviewCount <= 0 ? 20 : request.PreviewCount, 1, 100);
        var lookbackDays = Clamp(request.LookbackDays <= 0 ? 7 : request.LookbackDays, 1, 365);

        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(request.Host.Trim(), request.Port, ct);
            using var stream = request.EnableSsl
                ? await WrapSslAsync(client, request.Host.Trim(), ct)
                : client.GetStream();

            var greeting = await ReadImapAsync(stream, ct);
            if (!greeting.Contains("* OK", StringComparison.OrdinalIgnoreCase))
                return InboxResult(false, folder, $"IMAP sunucusu beklenen acilis yanitini vermedi: {Shorten(greeting)}");

            await WriteAsync(stream, $"A001 LOGIN \"{EscapeImap(request.Username.Trim())}\" \"{EscapeImap(request.Password ?? string.Empty)}\"\r\n", ct);
            var login = await ReadImapAsync(stream, ct, "A001");
            if (!login.Contains("A001 OK", StringComparison.OrdinalIgnoreCase))
                return InboxResult(false, folder, $"IMAP kimlik dogrulama basarisiz: {Shorten(login)}");

            await WriteAsync(stream, $"A002 SELECT \"{EscapeImap(folder)}\"\r\n", ct);
            var selected = await ReadImapAsync(stream, ct, "A002");
            if (!selected.Contains("A002 OK", StringComparison.OrdinalIgnoreCase))
                return InboxResult(false, folder, $"Klasor acilamadi: {Shorten(selected)}");

            var total = ParseExists(selected);
            var since = DateTime.UtcNow.AddDays(-lookbackDays).ToString("dd-MMM-yyyy", System.Globalization.CultureInfo.InvariantCulture);
            var criteria = request.UnreadOnly ? $"UNSEEN SINCE {since}" : $"SINCE {since}";
            await WriteAsync(stream, $"A003 SEARCH {criteria}\r\n", ct);
            var search = await ReadImapAsync(stream, ct, "A003");
            var ids = ParseSearchIds(search)
                .TakeLast(limit)
                .Reverse()
                .ToList();

            var messages = new List<ImapInboxMessageDto>();
            var tagNo = 4;
            foreach (var id in ids)
            {
                var tag = $"A{tagNo++:000}";
                await WriteAsync(stream, $"{tag} FETCH {id} (FLAGS RFC822.SIZE BODY.PEEK[HEADER.FIELDS (FROM SUBJECT DATE MESSAGE-ID IN-REPLY-TO REFERENCES)])\r\n", ct);
                var fetch = await ReadImapAsync(stream, ct, tag);
                messages.Add(ParseFetchedMessage(id, fetch));
            }

            await WriteAsync(stream, $"A{tagNo++:000} LOGOUT\r\n", ct);

            return new ImapInboxPreviewResponse
            {
                Success = true,
                Folder = folder,
                TotalMessages = total,
                ReturnedMessages = messages.Count,
                Messages = messages,
                Message = $"{folder} klasorunden {messages.Count} mail listelendi"
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "IMAP inbox preview failed");
            return InboxResult(false, folder, $"INBOX goruntuleme basarisiz: {ex.Message}");
        }
    }

    public async Task<ImapMessageContentResponse> GetInboxMessageAsync(ImapMessageContentRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.MessageId) || !int.TryParse(request.MessageId, out _))
            throw new ArgumentException("Gecerli bir IMAP mesaj id zorunludur");

        if (string.IsNullOrWhiteSpace(request.Password))
            request.Password = await GetSecretAsync(ImapPasswordKey, ct);

        ValidateImap(request, allowEmptyPassword: false);

        var folder = string.IsNullOrWhiteSpace(request.Folder) ? "INBOX" : request.Folder.Trim();

        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(request.Host.Trim(), request.Port, ct);
            using var stream = request.EnableSsl
                ? await WrapSslAsync(client, request.Host.Trim(), ct)
                : client.GetStream();

            var greeting = await ReadImapAsync(stream, ct);
            if (!greeting.Contains("* OK", StringComparison.OrdinalIgnoreCase))
                return MessageContentResult(false, request.MessageId, $"IMAP sunucusu beklenen acilis yanitini vermedi: {Shorten(greeting)}");

            await WriteAsync(stream, $"A001 LOGIN \"{EscapeImap(request.Username.Trim())}\" \"{EscapeImap(request.Password ?? string.Empty)}\"\r\n", ct);
            var login = await ReadImapAsync(stream, ct, "A001");
            if (!login.Contains("A001 OK", StringComparison.OrdinalIgnoreCase))
                return MessageContentResult(false, request.MessageId, $"IMAP kimlik dogrulama basarisiz: {Shorten(login)}");

            await WriteAsync(stream, $"A002 SELECT \"{EscapeImap(folder)}\"\r\n", ct);
            var selected = await ReadImapAsync(stream, ct, "A002");
            if (!selected.Contains("A002 OK", StringComparison.OrdinalIgnoreCase))
                return MessageContentResult(false, request.MessageId, $"Klasor acilamadi: {Shorten(selected)}");

            var fetch = await FetchImapMessageAsync(stream, request.MessageId, ct);
            await WriteAsync(stream, "A900 LOGOUT\r\n", ct);

            if (!IsTaggedOk(fetch))
                return MessageContentResult(false, request.MessageId, $"Mail icerigi alinamadi: {Shorten(fetch)}");

            var rawMessage = ExtractImapMessageContent(fetch, out var truncated);
            if (string.IsNullOrWhiteSpace(rawMessage))
                return MessageContentResult(false, request.MessageId, "Mail icerigi bos geldi veya okunamadi");

            var parsed = ParseMessageContent(request.MessageId, rawMessage);
            var fullMessageLoaded = false;
            try
            {
                var fullRawMessage = await GetFullImapMessageAsync(request, ct);
                parsed = ParseMessageContent(request.MessageId, fullRawMessage);
                fullMessageLoaded = true;
            }
            catch (Exception ex)
            {
                // Keep the message body available even when a very large attachment
                // cannot be listed within the download safety limit.
                _logger.LogInformation(ex, "IMAP attachment listing skipped for message {MessageId}", request.MessageId);
            }
            parsed.Truncated = !fullMessageLoaded && (truncated || rawMessage.Length >= ImapMessagePreviewBytes);
            parsed.Success = true;
            parsed.Message = parsed.Truncated
                ? "Mail icerigi onizleme limitiyle gosteriliyor"
                : "Mail icerigi alindi";
            return parsed;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "IMAP message content preview failed");
            return MessageContentResult(false, request.MessageId, $"Mail icerigi goruntulenemedi: {ex.Message}");
        }
    }

    public async Task<(byte[] Content, string FileName, string ContentType)> GetInboxAttachmentAsync(
        ImapAttachmentRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.AttachmentId) || !int.TryParse(request.AttachmentId, out var attachmentIndex) || attachmentIndex < 0)
            throw new ArgumentException("Gecerli bir ek kimligi zorunludur");

        var rawMessage = await GetFullImapMessageAsync(request, ct);
        var attachments = ExtractAttachments(rawMessage);
        if (attachmentIndex >= attachments.Count)
            throw new ArgumentException("Mail eki bulunamadi veya artik indirilemiyor");

        var attachment = attachments[attachmentIndex];
        return (
            DecodeMimeBytes(attachment.Body, attachment.TransferEncoding),
            SafeFileName(MimeFileName(attachment), $"ek-{attachmentIndex + 1}"),
            SafeContentType(attachment.ContentType));
    }

    public async Task<LdapSettingsResponse> GetLdapAsync(CancellationToken ct = default)
    {
        var dict = await LoadAsync(LdapPrefix, ct);
        return BuildLdapResponse(dict);
    }

    public async Task<LdapSettingsResponse> SaveLdapAsync(LdapSettingsRequest request, CancellationToken ct = default)
    {
        ValidateLdap(request);

        await UpsertAsync(LdapEnabledKey, request.Enabled.ToString(), ct);
        await UpsertAsync(LdapUseLdapsKey, request.UseLdaps.ToString(), ct);
        await UpsertAsync(LdapHostKey, request.Host.Trim(), ct);
        await UpsertAsync(LdapPortKey, request.Port.ToString(), ct);
        await UpsertAsync(LdapDomainKey, request.Domain.Trim(), ct);
        await UpsertAsync(LdapSearchBaseKey, request.SearchBase.Trim(), ct);
        await UpsertAsync(LdapServiceAccountKey, request.ServiceAccount.Trim(), ct);

        if (!string.IsNullOrWhiteSpace(request.ServicePassword))
            await UpsertAsync(LdapServicePasswordKey, _protector.Protect(request.ServicePassword), ct);

        InvalidateLdapCaches();

        return await GetLdapAsync(ct);
    }

    public async Task<DirectorySettingsTestResult> TestLdapAsync(LdapSettingsRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.ServicePassword))
            request.ServicePassword = await GetSecretAsync(LdapServicePasswordKey, ct);

        ValidateLdap(request);

        try
        {
            if (!string.IsNullOrWhiteSpace(request.ServiceAccount) && !string.IsNullOrWhiteSpace(request.ServicePassword))
            {
                var bindCandidates = BuildLdapBindCandidates(request.ServiceAccount, request.Domain).Distinct(StringComparer.OrdinalIgnoreCase);
                foreach (var bindName in bindCandidates)
                {
                    if (await TryLdapSimpleBindAsync(request.Host.Trim(), request.Port, request.UseLdaps, bindName, request.ServicePassword, ct))
                        return Result(true, "LDAP baglantisi ve servis hesabi bind testi basarili");
                }

                return Result(false, "LDAP servis hesabi bind testi basarisiz");
            }

            using var client = new TcpClient();
            await client.ConnectAsync(request.Host.Trim(), request.Port, ct);

            if (request.UseLdaps)
            {
                await using var ssl = await WrapSslAsync(client, request.Host.Trim(), ct);
                return Result(true, "LDAPS endpoint erisilebilir ve TLS el sikismasi basarili");
            }

            return Result(true, "LDAP endpoint erisilebilir. Servis hesabi girilirse bind testi de yapilir");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LDAP connection test failed");
            return Result(false, $"LDAP testi basarisiz: {ex.Message}");
        }
    }

    public async Task<LdapAuthenticationResult> AuthenticateLdapAsync(string username, string password, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return LdapAuthResult(false, username, "Kullanici adi ve sifre zorunludur");

        var settings = await GetLdapAsync(ct);
        if (!settings.Enabled)
            return LdapAuthResult(false, username, "LDAP login aktif degil");
        if (string.IsNullOrWhiteSpace(settings.Host))
            return LdapAuthResult(false, username, "LDAP sunucu adresi yapilandirilmamis");

        var normalizedUsername = NormalizeLoginUsername(username);
        var bindCandidates = BuildLdapBindCandidates(username, settings.Domain).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        try
        {
            foreach (var bindName in bindCandidates)
            {
                if (await TryLdapSimpleBindAsync(settings.Host.Trim(), settings.Port, settings.UseLdaps, bindName, password, ct))
                {
                    var email = await ResolveLdapEmailAsync(settings, normalizedUsername, username, bindName, password, ct);
                    return LdapAuthResult(true, normalizedUsername, "LDAP kimlik dogrulama basarili", email ?? BuildEmail(normalizedUsername, settings.Domain));
                }
            }

            return LdapAuthResult(false, normalizedUsername, "LDAP kullanici adi veya sifre hatali");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LDAP authentication failed for {Username}", normalizedUsername);
            return LdapAuthResult(false, normalizedUsername, $"LDAP login basarisiz: {ex.Message}");
        }
    }

    public async Task<LdapUserLookupResult> LookupLdapUserAsync(string username, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(username))
            return LdapLookupResult(false, username, "Kullanici adi zorunludur");

        // Yapilandirma kontrolleri cache'ten geldigi icin ucuzdur ve arama cache'ini
        // "LDAP kapali" kayitlariyla doldurmamak icin once burada yapilir.
        var settings = await GetLdapCachedAsync(ct);
        if (!settings.Enabled)
            return LdapLookupResult(false, username, "LDAP aktif degil");
        if (!settings.IsConfigured)
            return LdapLookupResult(false, username, "LDAP servis hesabi yapilandirilmamis");
        if (string.IsNullOrWhiteSpace(settings.SearchBase))
            return LdapLookupResult(false, username, "LDAP arama tabani yapilandirilmamis");

        var servicePassword = await GetLdapServicePasswordCachedAsync(ct);
        if (string.IsNullOrWhiteSpace(servicePassword))
            return LdapLookupResult(false, username, "LDAP servis hesabi sifresi kayitli degil");

        // Ayni kullanici hem tek istek icinde hem de istekler arasinda defalarca aranabiliyor.
        // Sonucu cache'ten donmek, kullanici basina TCP + bind + search maliyetini ortadan kaldirir.
        var cacheKey = LdapUserCacheKeyPrefix
            + Interlocked.Read(ref _ldapCacheGeneration).ToString()
            + ":" + username.Trim().ToLowerInvariant();

        if (_cache.TryGetValue<LdapUserLookupResult>(cacheKey, out var cachedResult) && cachedResult is not null)
            return cachedResult;

        var result = await SearchLdapUserAsync(settings, servicePassword, username, ct);
        _cache.Set(cacheKey, result, result.Success ? LdapUserHitCacheTtl : LdapUserMissCacheTtl);
        return result;
    }

    private async Task<LdapUserLookupResult> SearchLdapUserAsync(
        LdapSettingsResponse settings,
        string servicePassword,
        string username,
        CancellationToken ct)
    {
        var normalizedUsername = NormalizeLoginUsername(username);
        try
        {
            foreach (var bindName in BuildLdapBindCandidates(settings.ServiceAccount, settings.Domain).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var profile = await TrySearchLdapUserProfileAsync(settings, bindName, servicePassword, normalizedUsername, username, ct);
                if (profile?.Success == true)
                    return profile;
            }

            return LdapLookupResult(false, normalizedUsername, "LDAP kullanicisi bulunamadi");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LDAP user lookup failed for {Username}", normalizedUsername);
            return LdapLookupResult(false, normalizedUsername, $"LDAP kullanici arama basarisiz: {ex.Message}");
        }
    }

    public async Task<LdapAttributeDumpResult> DumpLdapUserAttributesAsync(string username, bool includeOperational = false, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(username))
            return LdapAttributeDump(false, username, "Kullanici adi zorunludur");

        var settings = await GetLdapAsync(ct);
        if (!settings.Enabled)
            return LdapAttributeDump(false, username, "LDAP aktif degil");
        if (!settings.IsConfigured)
            return LdapAttributeDump(false, username, "LDAP servis hesabi yapilandirilmamis");
        if (string.IsNullOrWhiteSpace(settings.SearchBase))
            return LdapAttributeDump(false, username, "LDAP arama tabani yapilandirilmamis");

        var servicePassword = await GetSecretAsync(LdapServicePasswordKey, ct);
        if (string.IsNullOrWhiteSpace(servicePassword))
            return LdapAttributeDump(false, username, "LDAP servis hesabi sifresi kayitli degil");

        var normalizedUsername = NormalizeLoginUsername(username);
        try
        {
            foreach (var bindName in BuildLdapBindCandidates(settings.ServiceAccount, settings.Domain).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var attributes = await TryDumpLdapUserAttributesAsync(
                    settings,
                    bindName,
                    servicePassword,
                    normalizedUsername,
                    username,
                    includeOperational,
                    ct);

                if (attributes.Any())
                    return LdapAttributeDump(true, normalizedUsername, "LDAP attribute listesi alindi", attributes);
            }

            return LdapAttributeDump(false, normalizedUsername, "LDAP kullanicisi bulunamadi");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LDAP attribute dump failed for {Username}", normalizedUsername);
            return LdapAttributeDump(false, normalizedUsername, $"LDAP attribute okuma basarisiz: {ex.Message}");
        }
    }

    private async Task<string?> ResolveLdapEmailAsync(
        LdapSettingsResponse settings,
        string normalizedUsername,
        string originalUsername,
        string successfulBindName,
        string userPassword,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(settings.SearchBase))
            return null;

        var servicePassword = await GetSecretAsync(LdapServicePasswordKey, ct);
        if (!string.IsNullOrWhiteSpace(settings.ServiceAccount) && !string.IsNullOrWhiteSpace(servicePassword))
        {
            foreach (var bindName in BuildLdapBindCandidates(settings.ServiceAccount, settings.Domain).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var email = await TrySearchLdapUserEmailAsync(settings, bindName, servicePassword, normalizedUsername, originalUsername, ct);
                if (!string.IsNullOrWhiteSpace(email))
                    return email;
            }
        }

        return await TrySearchLdapUserEmailAsync(settings, successfulBindName, userPassword, normalizedUsername, originalUsername, ct);
    }

    private async Task<LdapUserLookupResult?> TrySearchLdapUserProfileAsync(
        LdapSettingsResponse settings,
        string bindName,
        string password,
        string normalizedUsername,
        string originalUsername,
        CancellationToken ct)
    {
        var attributes = await TrySearchLdapUserAttributesAsync(settings, bindName, password, normalizedUsername, originalUsername, ct);
        if (attributes.Count == 0)
            return null;

        var email = FirstAttribute(attributes, "mail", "userPrincipalName") ?? BuildEmail(normalizedUsername, settings.Domain);
        var firstName = FirstAttribute(attributes, "givenName");
        var lastName = FirstAttribute(attributes, "sn");
        var fullName = FirstAttribute(attributes, "displayName");
        var department = FirstAttribute(attributes, "department", "company", "physicalDeliveryOfficeName", "title");
        var gender = FirstAttribute(attributes, "gender", "sex", "personalTitle")
            ?? InferGenderFromMemberOf(FirstAttribute(attributes, "memberOf"));
        if (string.IsNullOrWhiteSpace(fullName))
            fullName = string.Join(' ', new[] { firstName, lastName }.Where(x => !string.IsNullOrWhiteSpace(x))).Trim();

        return new LdapUserLookupResult
        {
            Success = true,
            Username = normalizedUsername,
            Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim(),
            FullName = string.IsNullOrWhiteSpace(fullName) ? null : fullName.Trim(),
            FirstName = string.IsNullOrWhiteSpace(firstName) ? null : firstName.Trim(),
            LastName = string.IsNullOrWhiteSpace(lastName) ? null : lastName.Trim(),
            Department = string.IsNullOrWhiteSpace(department) ? null : department.Trim(),
            Gender = string.IsNullOrWhiteSpace(gender) ? null : gender.Trim(),
            Message = "LDAP kullanicisi bulundu",
            TestedAt = DateTime.UtcNow
        };
    }

    private async Task<string?> TrySearchLdapUserEmailAsync(
        LdapSettingsResponse settings,
        string bindName,
        string password,
        string normalizedUsername,
        string originalUsername,
        CancellationToken ct)
    {
        try
        {
            var attributes = await TrySearchLdapUserAttributesAsync(settings, bindName, password, normalizedUsername, originalUsername, ct);
            return FirstAttribute(attributes, "mail", "userPrincipalName")?.Trim();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "LDAP user attribute lookup failed for {Username}", normalizedUsername);
        }

        return null;
    }

    private async Task<Dictionary<string, List<string>>> TryDumpLdapUserAttributesAsync(
        LdapSettingsResponse settings,
        string bindName,
        string password,
        string normalizedUsername,
        string originalUsername,
        bool includeOperational,
        CancellationToken ct)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(settings.Host.Trim(), settings.Port, ct);
        using var stream = settings.UseLdaps
            ? await WrapSslAsync(client, settings.Host.Trim(), ct)
            : client.GetStream();

        var bindRequest = BuildLdapSimpleBindRequest(1, bindName, password);
        await stream.WriteAsync(bindRequest, ct);
        await stream.FlushAsync(ct);

        var bindResponse = await ReadLdapResponseAsync(stream, ct);
        if (!IsLdapBindSuccess(bindResponse))
            return new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        var searchRequest = BuildLdapUserAttributeDumpSearchRequest(
            2,
            settings.SearchBase,
            normalizedUsername,
            originalUsername,
            settings.Domain,
            includeOperational);

        await stream.WriteAsync(searchRequest, ct);
        await stream.FlushAsync(ct);

        for (var i = 0; i < 20; i++)
        {
            var response = await ReadLdapResponseAsync(stream, ct);
            var operation = GetLdapProtocolOperationTag(response);
            if (operation == 0x64)
                return ParseLdapSearchResultAttributeValues(response);

            if (operation == 0x65)
                break;
        }

        return new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
    }

    private async Task<Dictionary<string, string>> TrySearchLdapUserAttributesAsync(
        LdapSettingsResponse settings,
        string bindName,
        string password,
        string normalizedUsername,
        string originalUsername,
        CancellationToken ct)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(settings.Host.Trim(), settings.Port, ct);
        using var stream = settings.UseLdaps
            ? await WrapSslAsync(client, settings.Host.Trim(), ct)
            : client.GetStream();

        var bindRequest = BuildLdapSimpleBindRequest(1, bindName, password);
        await stream.WriteAsync(bindRequest, ct);
        await stream.FlushAsync(ct);

        var bindResponse = await ReadLdapResponseAsync(stream, ct);
        if (!IsLdapBindSuccess(bindResponse))
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var searchRequest = BuildLdapUserSearchRequest(2, settings.SearchBase, normalizedUsername, originalUsername, settings.Domain);
        await stream.WriteAsync(searchRequest, ct);
        await stream.FlushAsync(ct);

        for (var i = 0; i < 20; i++)
        {
            var response = await ReadLdapResponseAsync(stream, ct);
            var operation = GetLdapProtocolOperationTag(response);
            if (operation == 0x64)
                return ParseLdapSearchResultAttributes(response);

            if (operation == 0x65)
                break;
        }

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    private static async Task<bool> TryLdapSimpleBindAsync(string host, int port, bool useLdaps, string bindName, string password, CancellationToken ct)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(host, port, ct);
        using var stream = useLdaps
            ? await WrapSslAsync(client, host, ct)
            : client.GetStream();

        var request = BuildLdapSimpleBindRequest(1, bindName, password);
        await stream.WriteAsync(request, ct);
        await stream.FlushAsync(ct);

        var response = await ReadLdapResponseAsync(stream, ct);
        return IsLdapBindSuccess(response);
    }

    private async Task<Dictionary<string, SystemSetting>> LoadAsync(string prefix, CancellationToken ct)
    {
        return await _context.SystemSettings
            .AsNoTracking()
            .Where(s => s.Key.StartsWith(prefix))
            .ToDictionaryAsync(s => s.Key, s => s, ct);
    }

    private async Task UpsertAsync(string key, string value, CancellationToken ct)
    {
        var entity = await _context.SystemSettings.FirstOrDefaultAsync(s => s.Key == key, ct);
        if (entity == null)
        {
            _context.SystemSettings.Add(new SystemSetting { Key = key, Value = value, UpdatedAt = DateTime.UtcNow });
        }
        else
        {
            entity.Value = value;
            entity.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// LDAP ayarlarini onbellekten doner. Cache bos ise DbContext'e yalniz tek bir cagri
    /// girecek sekilde kilitler; paralel arama yapan cagiricilar icin gereklidir.
    /// </summary>
    private async Task<LdapSettingsResponse> GetLdapCachedAsync(CancellationToken ct)
    {
        if (_cache.TryGetValue<LdapSettingsResponse>(LdapSettingsCacheKey, out var cached) && cached is not null)
            return cached;

        await _settingsReadGate.WaitAsync(ct);
        try
        {
            if (_cache.TryGetValue<LdapSettingsResponse>(LdapSettingsCacheKey, out cached) && cached is not null)
                return cached;

            var fresh = await GetLdapAsync(ct);
            _cache.Set(LdapSettingsCacheKey, fresh, LdapSettingsCacheTtl);
            return fresh;
        }
        finally
        {
            _settingsReadGate.Release();
        }
    }

    /// <summary>LDAP servis hesabi sifresini onbellekten doner (bkz. <see cref="GetLdapCachedAsync"/>).</summary>
    private async Task<string?> GetLdapServicePasswordCachedAsync(CancellationToken ct)
    {
        if (_cache.TryGetValue<string>(LdapServicePasswordCacheKey, out var cached) && !string.IsNullOrWhiteSpace(cached))
            return cached;

        await _settingsReadGate.WaitAsync(ct);
        try
        {
            if (_cache.TryGetValue<string>(LdapServicePasswordCacheKey, out cached) && !string.IsNullOrWhiteSpace(cached))
                return cached;

            var fresh = await GetSecretAsync(LdapServicePasswordKey, ct);
            if (!string.IsNullOrWhiteSpace(fresh))
                _cache.Set(LdapServicePasswordCacheKey, fresh, LdapSettingsCacheTtl);
            return fresh;
        }
        finally
        {
            _settingsReadGate.Release();
        }
    }

    /// <summary>LDAP ayarlari kaydedilince ayar, parola ve tum kullanici onbellegini gecersiz kilar.</summary>
    private void InvalidateLdapCaches()
    {
        _cache.Remove(LdapSettingsCacheKey);
        _cache.Remove(LdapServicePasswordCacheKey);
        Interlocked.Increment(ref _ldapCacheGeneration);
    }

    private async Task<string?> GetSecretAsync(string key, CancellationToken ct)
    {
        var setting = await _context.SystemSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == key, ct);
        if (setting == null || string.IsNullOrWhiteSpace(setting.Value)) return null;

        try
        {
            return _protector.Unprotect(setting.Value);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not decrypt directory setting {Key}", key);
            return null;
        }
    }

    private static ImapSettingsResponse BuildImapResponse(Dictionary<string, SystemSetting> dict)
    {
        var host = Get(dict, ImapHostKey);
        var username = Get(dict, ImapUsernameKey);
        var passwordSet = dict.ContainsKey(ImapPasswordKey) && !string.IsNullOrWhiteSpace(dict[ImapPasswordKey].Value);

        return new ImapSettingsResponse
        {
            Enabled = Bool(Get(dict, ImapEnabledKey), false),
            Host = host,
            Port = Int(Get(dict, ImapPortKey), 993),
            EnableSsl = Bool(Get(dict, ImapSslKey), true),
            Username = username,
            PasswordSet = passwordSet,
            Folder = Get(dict, ImapFolderKey, "INBOX"),
            UnreadOnly = Bool(Get(dict, ImapUnreadOnlyKey), true),
            LookbackDays = Int(Get(dict, ImapLookbackDaysKey), 7),
            MaxMessages = Int(Get(dict, ImapMaxMessagesKey), 500),
            IsConfigured = !string.IsNullOrWhiteSpace(host) && !string.IsNullOrWhiteSpace(username) && passwordSet,
            UpdatedAt = dict.Values.OrderByDescending(s => s.UpdatedAt).FirstOrDefault()?.UpdatedAt
        };
    }

    private static LdapSettingsResponse BuildLdapResponse(Dictionary<string, SystemSetting> dict)
    {
        var host = Get(dict, LdapHostKey);
        var serviceAccount = Get(dict, LdapServiceAccountKey);
        var passwordSet = dict.ContainsKey(LdapServicePasswordKey) && !string.IsNullOrWhiteSpace(dict[LdapServicePasswordKey].Value);
        var useLdaps = Bool(Get(dict, LdapUseLdapsKey), true);

        return new LdapSettingsResponse
        {
            Enabled = Bool(Get(dict, LdapEnabledKey), false),
            UseLdaps = useLdaps,
            Host = host,
            Port = Int(Get(dict, LdapPortKey), useLdaps ? 636 : 389),
            Domain = Get(dict, LdapDomainKey),
            SearchBase = Get(dict, LdapSearchBaseKey),
            ServiceAccount = serviceAccount,
            ServicePasswordSet = passwordSet,
            IsConfigured = !string.IsNullOrWhiteSpace(host) && !string.IsNullOrWhiteSpace(serviceAccount) && passwordSet,
            UpdatedAt = dict.Values.OrderByDescending(s => s.UpdatedAt).FirstOrDefault()?.UpdatedAt
        };
    }

    private static void ValidateImap(ImapSettingsRequest request, bool allowEmptyPassword)
    {
        if (string.IsNullOrWhiteSpace(request.Host)) throw new ArgumentException("IMAP sunucu adresi zorunludur");
        if (request.Port is < 1 or > 65535) throw new ArgumentException("IMAP port 1 ile 65535 arasinda olmalidir");
        if (string.IsNullOrWhiteSpace(request.Username)) throw new ArgumentException("IMAP kullanici adi zorunludur");
        if (!allowEmptyPassword && string.IsNullOrWhiteSpace(request.Password)) throw new ArgumentException("IMAP sifresi zorunludur");
    }

    private static void ValidateLdap(LdapSettingsRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Host)) throw new ArgumentException("LDAP sunucu adresi zorunludur");
        if (request.Port is < 1 or > 65535) throw new ArgumentException("LDAP port 1 ile 65535 arasinda olmalidir");
    }

    private static async Task<Stream> WrapSslAsync(TcpClient client, string host, CancellationToken ct)
    {
        var ssl = new SslStream(client.GetStream(), false, (_, _, _, _) => true);
        await ssl.AuthenticateAsClientAsync(host, null, System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13, false);
        return ssl;
    }

    private static async Task WriteAsync(Stream stream, string command, CancellationToken ct)
    {
        var bytes = Encoding.ASCII.GetBytes(command);
        await stream.WriteAsync(bytes, ct);
        await stream.FlushAsync(ct);
    }

    private static async Task<string> ReadImapAsync(Stream stream, CancellationToken ct, string? untilTag = null, int? maxBytes = null)
    {
        var buffer = new byte[4096];
        var builder = new StringBuilder();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));

        do
        {
            var read = await stream.ReadAsync(buffer, timeout.Token);
            if (read <= 0) break;
            builder.Append(Encoding.Latin1.GetString(buffer, 0, read));
            if (maxBytes.HasValue && builder.Length > maxBytes.Value)
                throw new InvalidOperationException($"IMAP mail boyutu {maxBytes.Value / (1024 * 1024)} MB sinirini asiyor");
        }
        while (untilTag != null && !HasTaggedCompletion(builder.ToString(), untilTag));

        return builder.ToString();
    }

    private static async Task<string> FetchImapMessageAsync(Stream stream, string messageId, CancellationToken ct)
    {
        var attempts = new[]
        {
            ("A003", $"A003 FETCH {messageId} (BODY.PEEK[HEADER] BODY.PEEK[TEXT]<0.{ImapMessagePreviewBytes}>)\r\n"),
            ("A004", $"A004 FETCH {messageId} (BODY.PEEK[]<0.{ImapMessagePreviewBytes}>)\r\n"),
            ("A005", $"A005 FETCH {messageId} (RFC822.HEADER RFC822.TEXT)\r\n")
        };

        var lastResponse = string.Empty;
        foreach (var (tag, command) in attempts)
        {
            await WriteAsync(stream, command, ct);
            lastResponse = await ReadImapAsync(stream, ct, tag);
            if (IsTaggedOk(lastResponse) && !string.IsNullOrWhiteSpace(ExtractImapMessageContent(lastResponse, out _)))
                return lastResponse;
        }

        return lastResponse;
    }

    private async Task<string> GetFullImapMessageAsync(ImapMessageContentRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.MessageId) || !int.TryParse(request.MessageId, out _))
            throw new ArgumentException("Gecerli bir IMAP mesaj id zorunludur");
        if (string.IsNullOrWhiteSpace(request.Password))
            request.Password = await GetSecretAsync(ImapPasswordKey, ct);
        ValidateImap(request, allowEmptyPassword: false);

        var folder = string.IsNullOrWhiteSpace(request.Folder) ? "INBOX" : request.Folder.Trim();
        using var client = new TcpClient();
        await client.ConnectAsync(request.Host.Trim(), request.Port, ct);
        using var stream = request.EnableSsl
            ? await WrapSslAsync(client, request.Host.Trim(), ct)
            : client.GetStream();

        var greeting = await ReadImapAsync(stream, ct);
        if (!greeting.Contains("* OK", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("IMAP sunucusu beklenen acilis yanitini vermedi");

        await WriteAsync(stream, $"A001 LOGIN \"{EscapeImap(request.Username.Trim())}\" \"{EscapeImap(request.Password ?? string.Empty)}\"\r\n", ct);
        var login = await ReadImapAsync(stream, ct, "A001");
        if (!login.Contains("A001 OK", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("IMAP kimlik dogrulama basarisiz");

        await WriteAsync(stream, $"A002 SELECT \"{EscapeImap(folder)}\"\r\n", ct);
        var selected = await ReadImapAsync(stream, ct, "A002");
        if (!selected.Contains("A002 OK", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("IMAP klasoru acilamadi");

        await WriteAsync(stream, $"A003 FETCH {request.MessageId} (BODY.PEEK[])\r\n", ct);
        var response = await ReadImapAsync(stream, ct, "A003", ImapFullMessageMaxBytes);
        await WriteAsync(stream, "A900 LOGOUT\r\n", ct);
        if (!IsTaggedOk(response))
            throw new InvalidOperationException("Mail eki okunamadi");

        var rawMessage = ExtractImapMessageContent(response, out _);
        if (string.IsNullOrWhiteSpace(rawMessage))
            throw new InvalidOperationException("Mail icerigi bos geldi veya okunamadi");
        return rawMessage;
    }

    private static async Task<byte[]> ReadLdapResponseAsync(Stream stream, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(LdapResponseTimeout);

        var header = new byte[2];
        await ReadExactlyAsync(stream, header, timeout.Token);
        if (header[0] != 0x30) return header;

        var length = await ReadBerLengthAsync(stream, header[1], timeout.Token);
        var payload = new byte[length];
        await ReadExactlyAsync(stream, payload, timeout.Token);

        var encodedLength = BerLength(payload.Length);
        var response = new byte[1 + encodedLength.Length + payload.Length];
        response[0] = header[0];
        Buffer.BlockCopy(encodedLength, 0, response, 1, encodedLength.Length);
        Buffer.BlockCopy(payload, 0, response, 1 + encodedLength.Length, payload.Length);
        return response;
    }

    private static async Task ReadExactlyAsync(Stream stream, byte[] buffer, CancellationToken ct)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset, buffer.Length - offset), ct);
            if (read <= 0) throw new IOException("LDAP sunucusundan eksik yanit alindi");
            offset += read;
        }
    }

    private static async Task<int> ReadBerLengthAsync(Stream stream, byte firstLengthByte, CancellationToken ct)
    {
        if ((firstLengthByte & 0x80) == 0) return firstLengthByte;

        var byteCount = firstLengthByte & 0x7F;
        if (byteCount <= 0 || byteCount > 4) throw new InvalidOperationException("LDAP yanit uzunlugu okunamadi");

        var bytes = new byte[byteCount];
        await ReadExactlyAsync(stream, bytes, ct);
        var length = 0;
        foreach (var b in bytes)
            length = (length << 8) | b;
        return length;
    }

    private static DirectorySettingsTestResult Result(bool success, string message) => new()
    {
        Success = success,
        Message = message,
        TestedAt = DateTime.UtcNow
    };

    private static LdapAuthenticationResult LdapAuthResult(bool success, string username, string message, string? email = null) => new()
    {
        Success = success,
        Username = NormalizeLoginUsername(username),
        Email = email,
        Message = message,
        TestedAt = DateTime.UtcNow
    };

    private static LdapUserLookupResult LdapLookupResult(bool success, string username, string message) => new()
    {
        Success = success,
        Username = NormalizeLoginUsername(username),
        Message = message,
        TestedAt = DateTime.UtcNow
    };

    private static LdapAttributeDumpResult LdapAttributeDump(
        bool success,
        string username,
        string message,
        Dictionary<string, List<string>>? attributes = null) => new()
    {
        Success = success,
        Username = NormalizeLoginUsername(username),
        Message = message,
        Attributes = attributes ?? new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase),
        TestedAt = DateTime.UtcNow
    };

    private static ImapInboxPreviewResponse InboxResult(bool success, string folder, string message) => new()
    {
        Success = success,
        Folder = folder,
        Message = message,
        TestedAt = DateTime.UtcNow
    };

    private static ImapMessageContentResponse MessageContentResult(bool success, string id, string message) => new()
    {
        Success = success,
        Id = id,
        Message = message,
        TestedAt = DateTime.UtcNow
    };

    private static int ParseExists(string response)
    {
        var match = Regex.Match(response, @"\*\s+(\d+)\s+EXISTS", RegexOptions.IgnoreCase);
        return match.Success && int.TryParse(match.Groups[1].Value, out var count) ? count : 0;
    }

    private static IEnumerable<string> ParseSearchIds(string response)
    {
        foreach (Match match in Regex.Matches(response, @"^\*\s+SEARCH\s+(.*)$", RegexOptions.IgnoreCase | RegexOptions.Multiline))
        {
            foreach (var id in match.Groups[1].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (int.TryParse(id, out _)) yield return id;
            }
        }
    }

    private static ImapInboxMessageDto ParseFetchedMessage(string id, string response)
    {
        return new ImapInboxMessageDto
        {
            Id = id,
            MessageId = Header(response, "Message-ID"),
            InReplyTo = Header(response, "In-Reply-To"),
            References = Header(response, "References"),
            From = DecodeMimeHeader(Header(response, "From")),
            Subject = DecodeMimeHeader(Header(response, "Subject")),
            Date = Header(response, "Date"),
            Size = LongMatch(response, @"RFC822\.SIZE\s+(\d+)"),
            Unread = !Regex.IsMatch(response, @"\\Seen\b", RegexOptions.IgnoreCase)
        };
    }

    private static ImapMessageContentResponse ParseMessageContent(string id, string rawMessage)
    {
        var (headers, body) = rawMessage.TrimStart().StartsWith("--", StringComparison.Ordinal)
            ? (string.Empty, rawMessage)
            : SplitHeadersAndBody(rawMessage);
        var contentType = Header(headers, "Content-Type");
        var leaves = ParseMimeLeaves(headers, body, contentType);
        var bodyText = ExtractReadableBody(leaves);

        return new ImapMessageContentResponse
        {
            Id = id,
            MessageId = Header(headers, "Message-ID"),
            InReplyTo = Header(headers, "In-Reply-To"),
            References = Header(headers, "References"),
            From = DecodeMimeHeader(Header(headers, "From")),
            Subject = DecodeMimeHeader(Header(headers, "Subject")),
            Date = Header(headers, "Date"),
            ContentType = string.IsNullOrWhiteSpace(contentType) ? "text/plain" : contentType,
            BodyText = NormalizeBodyText(bodyText),
            Attachments = ToAttachmentDtos(leaves)
        };
    }

    private static string ExtractReadableBody(IEnumerable<MimeLeafPart> leaves)
    {
        var parts = leaves.Where(part => !part.IsAttachment).ToList();
        var textPart = parts.FirstOrDefault(part => IsTextPlain(part.ContentType));
        if (textPart != null)
            return DecodeMimeBody(textPart.Body, textPart.TransferEncoding, Charset(textPart.ContentType));

        var htmlPart = parts.FirstOrDefault(part => IsTextHtml(part.ContentType));
        return htmlPart == null
            ? string.Empty
            : HtmlToText(DecodeMimeBody(htmlPart.Body, htmlPart.TransferEncoding, Charset(htmlPart.ContentType)));
    }

    private static string ExtractImapLiteral(string response, out bool truncated)
    {
        truncated = response.Contains($"<0.{ImapMessagePreviewBytes}>", StringComparison.OrdinalIgnoreCase);
        var match = Regex.Match(response, @"\{(\d+)\}\r?\n", RegexOptions.Multiline);
        if (!match.Success)
        {
            var start = response.IndexOf("\r\n", StringComparison.Ordinal);
            return start >= 0 ? response[(start + 2)..] : response;
        }

        var declaredLength = int.TryParse(match.Groups[1].Value, out var length) ? length : 0;
        var contentStart = match.Index + match.Length;
        var available = Math.Max(0, response.Length - contentStart);
        var take = declaredLength > 0 ? Math.Min(declaredLength, available) : available;
        truncated = truncated || declaredLength >= ImapMessagePreviewBytes;
        return response.Substring(contentStart, take);
    }

    private static string ExtractImapMessageContent(string response, out bool truncated)
    {
        truncated = response.Contains($"<0.{ImapMessagePreviewBytes}>", StringComparison.OrdinalIgnoreCase);
        var literals = new List<string>();

        foreach (Match match in Regex.Matches(response, @"\{(\d+)\}\r?\n", RegexOptions.Multiline))
        {
            var declaredLength = int.TryParse(match.Groups[1].Value, out var length) ? length : 0;
            var contentStart = match.Index + match.Length;
            var available = Math.Max(0, response.Length - contentStart);
            var take = declaredLength > 0 ? Math.Min(declaredLength, available) : available;
            if (take <= 0) continue;

            truncated = truncated || declaredLength >= ImapMessagePreviewBytes;
            literals.Add(response.Substring(contentStart, take));
        }

        if (literals.Count == 1)
            return literals[0];
        if (literals.Count > 1)
            return string.Join("\r\n\r\n", literals.Select(part => part.Trim('\r', '\n')));

        return ExtractImapLiteral(response, out truncated);
    }

    private static bool HasTaggedCompletion(string response, string tag) =>
        Regex.IsMatch(response, $@"(^|\r?\n){Regex.Escape(tag)}\s+(OK|NO|BAD)\b", RegexOptions.IgnoreCase);

    private static bool IsTaggedOk(string response) =>
        Regex.IsMatch(response, @"(^|\r?\n)A\d+\s+OK\b", RegexOptions.IgnoreCase);

    private static (string Headers, string Body) SplitHeadersAndBody(string value)
    {
        var separator = value.IndexOf("\r\n\r\n", StringComparison.Ordinal);
        var separatorLength = 4;
        if (separator < 0)
        {
            separator = value.IndexOf("\n\n", StringComparison.Ordinal);
            separatorLength = 2;
        }

        return separator < 0
            ? (value, string.Empty)
            : (value[..separator], value[(separator + separatorLength)..]);
    }

    private static IEnumerable<string> SplitMimeParts(string body, string boundary)
    {
        var marker = "--" + boundary;
        foreach (var part in body.Split(marker, StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = part.Trim('\r', '\n');
            if (trimmed == "--" || trimmed.StartsWith("--", StringComparison.Ordinal)) continue;
            if (!string.IsNullOrWhiteSpace(trimmed)) yield return trimmed;
        }
    }

    private static List<MimeLeafPart> ParseMimeLeaves(string headers, string body, string? suppliedContentType = null)
    {
        var contentType = suppliedContentType ?? Header(headers, "Content-Type");
        var boundary = IsMultipart(contentType)
            ? Parameter(contentType, "boundary")
            : DetectMimeBoundary(body);
        if (!string.IsNullOrWhiteSpace(boundary))
        {
            return SplitMimeParts(body, boundary)
                .SelectMany(part =>
                {
                    var (partHeaders, partBody) = SplitHeadersAndBody(part);
                    return ParseMimeLeaves(partHeaders, partBody);
                })
                .ToList();
        }

        return [new MimeLeafPart(
            contentType,
            Header(headers, "Content-Transfer-Encoding"),
            Header(headers, "Content-Disposition"),
            Header(headers, "Content-ID"),
            body,
            IsMimeAttachment(headers, contentType))];
    }

    private static List<MimeLeafPart> ExtractAttachments(string rawMessage)
    {
        var (headers, body) = rawMessage.TrimStart().StartsWith("--", StringComparison.Ordinal)
            ? (string.Empty, rawMessage)
            : SplitHeadersAndBody(rawMessage);
        return ParseMimeLeaves(headers, body)
            .Where(part => part.IsAttachment)
            .ToList();
    }

    private static List<ImapAttachmentDto> ToAttachmentDtos(IEnumerable<MimeLeafPart> leaves) => leaves
        .Where(part => part.IsAttachment)
        .Select((part, index) => new ImapAttachmentDto
        {
            Id = index.ToString(),
            FileName = SafeFileName(MimeFileName(part), $"ek-{index + 1}"),
            ContentType = SafeContentType(part.ContentType),
            Size = MimeDecodedSize(part),
            IsInline = part.Disposition.Contains("inline", StringComparison.OrdinalIgnoreCase)
        })
        .ToList();

    private static bool IsMimeAttachment(string headers, string contentType)
    {
        var disposition = Header(headers, "Content-Disposition");
        if (disposition.Contains("attachment", StringComparison.OrdinalIgnoreCase)) return true;
        // Logos, signature cards and other CID resources are sent as inline parts.
        // They are message content, not user-added downloadable attachments.
        if (disposition.Contains("inline", StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrWhiteSpace(Header(headers, "Content-ID"))) return false;
        return !string.IsNullOrWhiteSpace(Parameter(disposition, "filename")) ||
               !string.IsNullOrWhiteSpace(Parameter(contentType, "name"));
    }

    private static string MimeFileName(MimeLeafPart part) =>
        Parameter(part.Disposition, "filename") ??
        Parameter(part.ContentType, "name") ??
        "ek";

    private static long MimeDecodedSize(MimeLeafPart part)
    {
        try { return DecodeMimeBytes(part.Body, part.TransferEncoding).LongLength; }
        catch { return 0; }
    }

    private sealed record MimeLeafPart(
        string ContentType,
        string TransferEncoding,
        string Disposition,
        string ContentId,
        string Body,
        bool IsAttachment);

    private static string DecodeMimeBody(string body, string transferEncoding, string charset)
    {
        try
        {
            var bytes = DecodeMimeBytes(body, transferEncoding);
            var decoded = SafeEncoding(charset).GetString(bytes);

            // Some mail clients declare ISO-8859-9 but actually send UTF-8. Prefer the
            // alternative only when it clearly avoids replacement/mojibake characters.
            if (!charset.Contains("utf", StringComparison.OrdinalIgnoreCase))
            {
                var utf8 = Encoding.UTF8.GetString(bytes);
                if (EncodingDamageScore(utf8) < EncodingDamageScore(decoded))
                    return utf8;
            }

            return decoded;
        }
        catch
        {
            return body;
        }
    }

    private static byte[] DecodeMimeBytes(string body, string transferEncoding)
    {
        if (transferEncoding.Contains("base64", StringComparison.OrdinalIgnoreCase))
        {
            var compact = Regex.Replace(body, @"\s+", "");
            try
            {
                return Convert.FromBase64String(compact);
            }
            catch (FormatException)
            {
                // Keep a malformed MIME part readable instead of failing the whole mail.
                return Encoding.Latin1.GetBytes(body);
            }
        }

        return transferEncoding.Contains("quoted-printable", StringComparison.OrdinalIgnoreCase)
            ? DecodeQuotedPrintableBody(body)
            : Encoding.Latin1.GetBytes(body);
    }

    private static byte[] DecodeQuotedPrintableBody(string value)
    {
        value = Regex.Replace(value, @"=\r?\n", "");
        var bytes = new List<byte>();
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] == '=' && i + 2 < value.Length &&
                byte.TryParse(value.Substring(i + 1, 2), System.Globalization.NumberStyles.HexNumber, null, out var b))
            {
                bytes.Add(b);
                i += 2;
            }
            else
            {
                bytes.Add((byte)value[i]);
            }
        }
        return bytes.ToArray();
    }

    private static string HtmlToText(string html)
    {
        var text = Regex.Replace(html, @"<(br|p|div|tr|li|h[1-6])\b[^>]*>", "\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"<style\b[^>]*>.*?</style>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        text = Regex.Replace(text, @"<script\b[^>]*>.*?</script>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        text = Regex.Replace(text, "<[^>]+>", " ");
        return WebUtility.HtmlDecode(text);
    }

    private static string NormalizeBodyText(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "Gosterilecek metin icerigi bulunamadi.";
        var normalized = value.Replace("\r\n", "\n").Replace('\r', '\n');
        normalized = Regex.Replace(normalized, @"[ \t]+\n", "\n");
        normalized = Regex.Replace(normalized, @"\n{3,}", "\n\n");
        return normalized.Trim();
    }

    private static bool IsMultipart(string value) => value.Contains("multipart/", StringComparison.OrdinalIgnoreCase);

    private static bool IsTextPlain(string value) => value.Contains("text/plain", StringComparison.OrdinalIgnoreCase);

    private static bool IsTextHtml(string value) => value.Contains("text/html", StringComparison.OrdinalIgnoreCase);

    private static string? DetectMimeBoundary(string body)
    {
        var match = Regex.Match(
            body,
            @"^--(?<boundary>[^\r\n]+)\r?\nContent-Type:\s*",
            RegexOptions.IgnoreCase);
        return match.Success ? match.Groups["boundary"].Value.Trim() : null;
    }

    private static string Charset(string contentType) => Parameter(contentType, "charset") ?? "utf-8";

    private static string? Parameter(string header, string name)
    {
        var match = Regex.Match(header, $@"(?:^|;)\s*{Regex.Escape(name)}\s*=\s*(""?)([^"";]+)\1", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[2].Value.Trim() : null;
    }

    private static string SafeFileName(string? value, string fallback)
    {
        var fileName = Path.GetFileName(string.IsNullOrWhiteSpace(value) ? fallback : value.Trim());
        var invalid = Path.GetInvalidFileNameChars();
        fileName = new string(fileName.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(fileName) ? fallback : fileName;
    }

    private static string SafeContentType(string? value)
    {
        var contentType = (value ?? string.Empty).Split(';')[0].Trim();
        return Regex.IsMatch(contentType, @"^[\w.+-]+/[\w.+-]+$")
            ? contentType
            : "application/octet-stream";
    }

    private static Encoding SafeEncoding(string charset)
    {
        var normalized = (charset ?? string.Empty).Trim().Trim('"').ToLowerInvariant() switch
        {
            "iso-8859-9" or "iso8859-9" or "latin5" or "latin-5" => "windows-1254",
            "iso-8859-1" or "iso8859-1" or "latin1" or "latin-1" => "windows-1252",
            var value when string.IsNullOrWhiteSpace(value) => "utf-8",
            var value => value
        };
        try
        {
            return Encoding.GetEncoding(normalized);
        }
        catch
        {
            return Encoding.UTF8;
        }
    }

    private static int EncodingDamageScore(string value)
    {
        var replacementCount = value.Count(character => character == '\uFFFD');
        var mojibakeCount = value.Count(character => character is 'Ã' or 'Â' or 'â');
        return replacementCount * 10 + mojibakeCount;
    }

    private static string Header(string response, string name)
    {
        // RFC 5322 allows long headers such as Content-Type/boundary to continue on
        // indented lines. Treat those continuation lines as part of the same value.
        var match = Regex.Match(
            response,
            $"^{Regex.Escape(name)}:\\s*(?<value>[^\\r\\n]*(?:\\r?\\n[\\t ]+[^\\r\\n]*)*)",
            RegexOptions.IgnoreCase | RegexOptions.Multiline);
        return match.Success
            ? Regex.Replace(match.Groups["value"].Value, @"\\r?\\n[\\t ]+", " ").Trim()
            : string.Empty;
    }

    private static long LongMatch(string response, string pattern)
    {
        var match = Regex.Match(response, pattern, RegexOptions.IgnoreCase);
        return match.Success && long.TryParse(match.Groups[1].Value, out var value) ? value : 0;
    }

    private static string DecodeMimeHeader(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        return Regex.Replace(value, @"=\?([^?]+)\?([bqBQ])\?([^?]+)\?=", match =>
        {
            try
            {
                var encoding = SafeEncoding(match.Groups[1].Value);
                var mode = match.Groups[2].Value.ToUpperInvariant();
                var payload = match.Groups[3].Value;
                var bytes = mode == "B"
                    ? Convert.FromBase64String(payload)
                    : DecodeQuotedPrintableHeader(payload);
                return encoding.GetString(bytes);
            }
            catch
            {
                return match.Value;
            }
        }).Trim();
    }

    private static byte[] DecodeQuotedPrintableHeader(string value)
    {
        value = value.Replace('_', ' ');
        var bytes = new List<byte>();
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] == '=' && i + 2 < value.Length &&
                byte.TryParse(value.Substring(i + 1, 2), System.Globalization.NumberStyles.HexNumber, null, out var b))
            {
                bytes.Add(b);
                i += 2;
            }
            else
            {
                bytes.Add((byte)value[i]);
            }
        }
        return bytes.ToArray();
    }

    private static string Get(Dictionary<string, SystemSetting> dict, string key, string fallback = "") =>
        dict.TryGetValue(key, out var value) ? value.Value : fallback;

    private static bool Bool(string value, bool fallback) => bool.TryParse(value, out var parsed) ? parsed : fallback;

    private static int Int(string value, int fallback) => int.TryParse(value, out var parsed) ? parsed : fallback;

    private static int Clamp(int value, int min, int max) => Math.Min(Math.Max(value, min), max);

    private static string EscapeImap(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static string Shorten(string value) => value.Length <= 300 ? value : value[..300];

    private static List<string> BuildLdapBindCandidates(string username, string domain)
    {
        var trimmed = username.Trim();
        var normalized = NormalizeLoginUsername(trimmed);
        var candidates = new List<string>();

        if (trimmed.Contains('\\') || trimmed.Contains('@') || trimmed.Contains('='))
            candidates.Add(trimmed);

        if (!string.IsNullOrWhiteSpace(domain))
        {
            var cleanDomain = domain.Trim();
            candidates.Add(cleanDomain.Contains('.')
                ? $"{normalized}@{cleanDomain}"
                : $"{cleanDomain}\\{normalized}");
        }

        candidates.Add(normalized);
        return candidates;
    }

    private static string NormalizeLoginUsername(string value)
    {
        var normalized = value.Trim();
        var slash = normalized.LastIndexOf('\\');
        if (slash >= 0 && slash + 1 < normalized.Length) normalized = normalized[(slash + 1)..];
        var at = normalized.IndexOf('@');
        if (at > 0) normalized = normalized[..at];
        return normalized;
    }

    private static string? BuildEmail(string username, string domain)
    {
        if (string.IsNullOrWhiteSpace(domain) || !domain.Contains('.')) return null;
        return $"{username}@{domain.Trim()}";
    }

    private static string? FirstAttribute(Dictionary<string, string> attributes, params string[] names)
    {
        foreach (var name in names)
        {
            if (attributes.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }

    private static byte[] BuildLdapUserSearchRequest(int messageId, string searchBase, string normalizedUsername, string originalUsername, string domain)
    {
        var attributes = BerConstructed(
            0x30,
            BerOctetString("mail"),
            BerOctetString("userPrincipalName"),
            BerOctetString("displayName"),
            BerOctetString("givenName"),
            BerOctetString("sn"),
            BerOctetString("department"),
            BerOctetString("company"),
            BerOctetString("physicalDeliveryOfficeName"),
            BerOctetString("title"),
            BerOctetString("gender"),
            BerOctetString("sex"),
            BerOctetString("personalTitle"),
            BerOctetString("memberOf"));

        return BuildLdapUserSearchRequest(messageId, searchBase, normalizedUsername, originalUsername, domain, attributes);
    }

    private static byte[] BuildLdapUserAttributeDumpSearchRequest(
        int messageId,
        string searchBase,
        string normalizedUsername,
        string originalUsername,
        string domain,
        bool includeOperational)
    {
        var attributes = includeOperational
            ? BerConstructed(0x30, BerOctetString("*"), BerOctetString("+"))
            : BerConstructed(0x30);

        return BuildLdapUserSearchRequest(messageId, searchBase, normalizedUsername, originalUsername, domain, attributes);
    }

    private static byte[] BuildLdapUserSearchRequest(
        int messageId,
        string searchBase,
        string normalizedUsername,
        string originalUsername,
        string domain,
        byte[] attributes)
    {
        var searchRequest = BerConstructed(
            0x63,
            BerOctetString(searchBase.Trim()),
            BerEnumerated(2),
            BerEnumerated(0),
            BerInteger(1),
            BerInteger(10),
            BerBoolean(false),
            BuildLdapUserSearchFilter(normalizedUsername, originalUsername, domain),
            attributes);

        return BerConstructed(0x30, BerInteger(messageId), searchRequest);
    }

    private static byte[] BuildLdapUserSearchFilter(string normalizedUsername, string originalUsername, string domain)
    {
        var filters = new List<byte[]>
        {
            BerEqualityFilter("sAMAccountName", normalizedUsername),
            BerEqualityFilter("cn", normalizedUsername),
            BerEqualityFilter("uid", normalizedUsername)
        };

        var trimmedOriginal = originalUsername.Trim();
        if (!string.Equals(trimmedOriginal, normalizedUsername, StringComparison.OrdinalIgnoreCase))
        {
            filters.Add(BerEqualityFilter("userPrincipalName", trimmedOriginal));
            if (trimmedOriginal.Contains('@'))
                filters.Add(BerEqualityFilter("mail", trimmedOriginal));
        }

        if (!string.IsNullOrWhiteSpace(domain) && domain.Contains('.'))
        {
            var upn = $"{normalizedUsername}@{domain.Trim()}";
            filters.Add(BerEqualityFilter("userPrincipalName", upn));
            filters.Add(BerEqualityFilter("mail", upn));
        }

        return BerConstructed(0xA1, filters.DistinctBy(Convert.ToBase64String).ToArray());
    }

    private static byte[] BerEqualityFilter(string attribute, string value) =>
        BerConstructed(0xA3, BerOctetString(attribute), BerOctetString(value));

    private static byte GetLdapProtocolOperationTag(byte[] response)
    {
        var offset = 0;
        if (!TryReadTlv(response, ref offset, out var tag, out var start, out var length) || tag != 0x30)
            return 0;

        var messageOffset = start;
        if (!TryReadTlv(response, ref messageOffset, out _, out _, out _))
            return 0;

        return messageOffset < start + length ? response[messageOffset] : (byte)0;
    }

    private static Dictionary<string, string> ParseLdapSearchResultAttributes(byte[] response)
    {
        return ParseLdapSearchResultAttributeValues(response)
            .Where(pair => pair.Value.Count > 0)
            .ToDictionary(pair => pair.Key, pair => string.Join("\n", pair.Value), StringComparer.OrdinalIgnoreCase);
    }

    private static string? InferGenderFromMemberOf(string? memberOf)
    {
        if (string.IsNullOrWhiteSpace(memberOf)) return null;

        var hasFemale = false;
        var hasMale = false;
        foreach (var group in memberOf.Split(new[] { '\r', '\n', ';' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var normalized = NormalizeGenderSourceText(group);
            if (normalized.Contains("bayan", StringComparison.Ordinal) ||
                normalized.Contains("kadin", StringComparison.Ordinal))
            {
                hasFemale = true;
            }

            if (normalized.Contains("erkek", StringComparison.Ordinal) ||
                Regex.IsMatch(normalized, @"(^|[^a-z])bay(lar)?([^a-z]|$)", RegexOptions.IgnoreCase))
            {
                hasMale = true;
            }
        }

        return (hasMale, hasFemale) switch
        {
            (true, false) => "male",
            (false, true) => "female",
            _ => null
        };
    }

    private static string NormalizeGenderSourceText(string value)
    {
        return value
            .Trim()
            .ToLowerInvariant()
            .Replace('ı', 'i')
            .Replace('İ', 'i')
            .Replace('ş', 's')
            .Replace('Ş', 's')
            .Replace('ğ', 'g')
            .Replace('Ğ', 'g')
            .Replace('ü', 'u')
            .Replace('Ü', 'u')
            .Replace('ö', 'o')
            .Replace('Ö', 'o')
            .Replace('ç', 'c')
            .Replace('Ç', 'c');
    }

    private static Dictionary<string, List<string>> ParseLdapSearchResultAttributeValues(byte[] response)
    {
        var offset = 0;
        if (!TryReadTlv(response, ref offset, out var messageTag, out var messageStart, out var messageLength) || messageTag != 0x30)
            return new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        var messageOffset = messageStart;
        if (!TryReadTlv(response, ref messageOffset, out _, out _, out _))
            return new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        if (!TryReadTlv(response, ref messageOffset, out var entryTag, out var entryStart, out var entryLength) || entryTag != 0x64)
            return new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        var entryOffset = entryStart;
        if (!TryReadTlv(response, ref entryOffset, out _, out _, out _))
            return new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        if (!TryReadTlv(response, ref entryOffset, out var listTag, out var listStart, out var listLength) || listTag != 0x30)
            return new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        var attributes = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var listOffset = listStart;
        var listEnd = Math.Min(listStart + listLength, entryStart + entryLength);
        while (listOffset < listEnd)
        {
            if (!TryReadTlv(response, ref listOffset, out var attributeTag, out var attributeStart, out var attributeLength) || attributeTag != 0x30)
                break;

            var attributeOffset = attributeStart;
            if (!TryReadTlv(response, ref attributeOffset, out var nameTag, out var nameStart, out var nameLength) || nameTag != 0x04)
                continue;
            var name = ReadBerString(response, nameStart, nameLength);

            if (!TryReadTlv(response, ref attributeOffset, out var valuesTag, out var valuesStart, out var valuesLength) || valuesTag != 0x31)
                continue;

            var valueOffset = valuesStart;
            var valuesEnd = valuesStart + valuesLength;
            while (valueOffset < valuesEnd)
            {
                if (!TryReadTlv(response, ref valueOffset, out var valueTag, out var valueStart, out var valueLength) || valueTag != 0x04 || valueStart + valueLength > valuesEnd)
                    break;

                var value = ReadBerString(response, valueStart, valueLength);
                if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(value))
                {
                    if (!attributes.TryGetValue(name, out var values))
                    {
                        values = new List<string>();
                        attributes[name] = values;
                    }

                    values.Add(value);
                }
            }
        }

        _ = messageLength;
        return attributes;
    }

    private static bool TryReadTlv(byte[] data, ref int offset, out byte tag, out int valueStart, out int valueLength)
    {
        tag = 0;
        valueStart = 0;
        valueLength = 0;

        if (offset >= data.Length) return false;
        tag = data[offset++];
        if (offset >= data.Length) return false;

        var firstLengthByte = data[offset++];
        if ((firstLengthByte & 0x80) == 0)
        {
            valueLength = firstLengthByte;
        }
        else
        {
            var byteCount = firstLengthByte & 0x7F;
            if (byteCount <= 0 || byteCount > 4 || offset + byteCount > data.Length) return false;

            for (var i = 0; i < byteCount; i++)
                valueLength = (valueLength << 8) | data[offset++];
        }

        valueStart = offset;
        offset += valueLength;
        return valueLength >= 0 && offset <= data.Length;
    }

    private static string ReadBerString(byte[] data, int start, int length) =>
        start < 0 || length <= 0 || start + length > data.Length
            ? string.Empty
            : Encoding.UTF8.GetString(data, start, length);

    private static byte[] BuildLdapSimpleBindRequest(int messageId, string bindName, string password)
    {
        var messageIdPart = BerInteger(messageId);
        var versionPart = BerInteger(3);
        var namePart = BerOctetString(bindName);
        var passwordPart = BerContextString(0, password);
        var bindRequest = BerConstructed(0x60, versionPart, namePart, passwordPart);
        return BerConstructed(0x30, messageIdPart, bindRequest);
    }

    private static bool IsLdapBindSuccess(byte[] response)
    {
        for (var i = 0; i < response.Length - 2; i++)
        {
            if (response[i] == 0x0A && response[i + 1] == 0x01)
                return response[i + 2] == 0x00;
        }
        return false;
    }

    private static byte[] BerInteger(int value)
    {
        var bytes = value <= 255
            ? new[] { (byte)value }
            : BitConverter.GetBytes(value).Reverse().SkipWhile(b => b == 0).ToArray();
        return BerTlv(0x02, bytes);
    }

    private static byte[] BerOctetString(string value) => BerTlv(0x04, Encoding.UTF8.GetBytes(value));

    private static byte[] BerEnumerated(int value) => BerTlv(0x0A, new[] { (byte)value });

    private static byte[] BerBoolean(bool value) => BerTlv(0x01, new[] { value ? (byte)0xFF : (byte)0x00 });

    private static byte[] BerContextString(byte tag, string value) => BerTlv((byte)(0x80 | tag), Encoding.UTF8.GetBytes(value));

    private static byte[] BerConstructed(byte tag, params byte[][] children) => BerTlv(tag, children.SelectMany(x => x).ToArray());

    private static byte[] BerTlv(byte tag, byte[] value)
    {
        var length = BerLength(value.Length);
        var result = new byte[1 + length.Length + value.Length];
        result[0] = tag;
        Buffer.BlockCopy(length, 0, result, 1, length.Length);
        Buffer.BlockCopy(value, 0, result, 1 + length.Length, value.Length);
        return result;
    }

    private static byte[] BerLength(int length)
    {
        if (length < 0x80) return new[] { (byte)length };
        var bytes = BitConverter.GetBytes(length).Reverse().SkipWhile(b => b == 0).ToArray();
        return new[] { (byte)(0x80 | bytes.Length) }.Concat(bytes).ToArray();
    }
}
