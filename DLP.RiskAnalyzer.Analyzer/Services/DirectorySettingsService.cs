using System.Net.Security;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using DLP.RiskAnalyzer.Analyzer.Data;
using DLP.RiskAnalyzer.Analyzer.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace DLP.RiskAnalyzer.Analyzer.Services;

public class DirectorySettingsService : IDirectorySettingsService
{
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

    private const string LdapPrefix = "ldap_";
    private const string LdapEnabledKey = "ldap_enabled";
    private const string LdapUseLdapsKey = "ldap_use_ldaps";
    private const string LdapHostKey = "ldap_host";
    private const string LdapPortKey = "ldap_port";
    private const string LdapDomainKey = "ldap_domain";
    private const string LdapSearchBaseKey = "ldap_search_base";
    private const string LdapServiceAccountKey = "ldap_service_account";
    private const string LdapServicePasswordKey = "ldap_service_password_protected";

    private readonly AnalyzerDbContext _context;
    private readonly IDataProtector _protector;
    private readonly ILogger<DirectorySettingsService> _logger;

    public DirectorySettingsService(
        AnalyzerDbContext context,
        IDataProtectionProvider dataProtectionProvider,
        ILogger<DirectorySettingsService> logger)
    {
        _context = context;
        _protector = dataProtectionProvider.CreateProtector("Directory.SettingsProtector");
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
                await WriteAsync(stream, $"{tag} FETCH {id} (FLAGS RFC822.SIZE BODY.PEEK[HEADER.FIELDS (FROM SUBJECT DATE)])\r\n", ct);
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

            await WriteAsync(stream, $"A003 FETCH {request.MessageId} (BODY.PEEK[]<0.{ImapMessagePreviewBytes}>)\r\n", ct);
            var fetch = await ReadImapAsync(stream, ct, "A003");
            await WriteAsync(stream, "A004 LOGOUT\r\n", ct);

            if (!fetch.Contains("A003 OK", StringComparison.OrdinalIgnoreCase))
                return MessageContentResult(false, request.MessageId, $"Mail icerigi alinamadi: {Shorten(fetch)}");

            var rawMessage = ExtractImapLiteral(fetch, out var truncated);
            if (string.IsNullOrWhiteSpace(rawMessage))
                return MessageContentResult(false, request.MessageId, "Mail icerigi bos geldi veya okunamadi");

            var parsed = ParseMessageContent(request.MessageId, rawMessage);
            parsed.Truncated = truncated || rawMessage.Length >= ImapMessagePreviewBytes;
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

        return await GetLdapAsync(ct);
    }

    public async Task<DirectorySettingsTestResult> TestLdapAsync(LdapSettingsRequest request, CancellationToken ct = default)
    {
        ValidateLdap(request);

        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(request.Host.Trim(), request.Port, ct);

            if (request.UseLdaps)
            {
                await using var ssl = await WrapSslAsync(client, request.Host.Trim(), ct);
                return Result(true, "LDAPS endpoint erisilebilir ve TLS el sikismasi basarili");
            }

            return Result(true, "LDAP endpoint erisilebilir");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LDAP connection test failed");
            return Result(false, $"LDAP testi basarisiz: {ex.Message}");
        }
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

    private static async Task<string> ReadImapAsync(Stream stream, CancellationToken ct, string? untilTag = null)
    {
        var buffer = new byte[4096];
        var builder = new StringBuilder();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));

        do
        {
            var read = await stream.ReadAsync(buffer, timeout.Token);
            if (read <= 0) break;
            builder.Append(Encoding.Latin1.GetString(buffer, 0, read));
        }
        while (untilTag != null && !builder.ToString().Contains(untilTag, StringComparison.OrdinalIgnoreCase));

        return builder.ToString();
    }

    private static DirectorySettingsTestResult Result(bool success, string message) => new()
    {
        Success = success,
        Message = message,
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
            From = DecodeMimeHeader(Header(response, "From")),
            Subject = DecodeMimeHeader(Header(response, "Subject")),
            Date = Header(response, "Date"),
            Size = LongMatch(response, @"RFC822\.SIZE\s+(\d+)"),
            Unread = !Regex.IsMatch(response, @"\\Seen\b", RegexOptions.IgnoreCase)
        };
    }

    private static ImapMessageContentResponse ParseMessageContent(string id, string rawMessage)
    {
        var (headers, body) = SplitHeadersAndBody(rawMessage);
        var contentType = Header(headers, "Content-Type");
        var transferEncoding = Header(headers, "Content-Transfer-Encoding");
        var bodyText = ExtractReadableBody(headers, body, contentType, transferEncoding);

        return new ImapMessageContentResponse
        {
            Id = id,
            From = DecodeMimeHeader(Header(headers, "From")),
            Subject = DecodeMimeHeader(Header(headers, "Subject")),
            Date = Header(headers, "Date"),
            ContentType = string.IsNullOrWhiteSpace(contentType) ? "text/plain" : contentType,
            BodyText = NormalizeBodyText(bodyText)
        };
    }

    private static string ExtractReadableBody(string headers, string body, string contentType, string transferEncoding)
    {
        if (IsMultipart(contentType))
        {
            var boundary = Parameter(contentType, "boundary");
            if (!string.IsNullOrWhiteSpace(boundary))
            {
                var parts = SplitMimeParts(body, boundary).ToList();
                var textPart = parts.Select(ParseMimePart).FirstOrDefault(p => IsTextPlain(p.ContentType) && !p.IsAttachment);
                if (textPart.Body != null)
                    return DecodeMimeBody(textPart.Body, textPart.TransferEncoding, Charset(textPart.ContentType));

                var htmlPart = parts.Select(ParseMimePart).FirstOrDefault(p => IsTextHtml(p.ContentType) && !p.IsAttachment);
                if (htmlPart.Body != null)
                    return HtmlToText(DecodeMimeBody(htmlPart.Body, htmlPart.TransferEncoding, Charset(htmlPart.ContentType)));
            }
        }

        var decoded = DecodeMimeBody(body, transferEncoding, Charset(contentType));
        return IsTextHtml(contentType) ? HtmlToText(decoded) : decoded;
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

    private static (string ContentType, string TransferEncoding, bool IsAttachment, string? Body) ParseMimePart(string part)
    {
        var (headers, body) = SplitHeadersAndBody(part);
        return (
            Header(headers, "Content-Type"),
            Header(headers, "Content-Transfer-Encoding"),
            Header(headers, "Content-Disposition").Contains("attachment", StringComparison.OrdinalIgnoreCase),
            body
        );
    }

    private static string DecodeMimeBody(string body, string transferEncoding, string charset)
    {
        var encoding = SafeEncoding(charset);
        try
        {
            if (transferEncoding.Contains("base64", StringComparison.OrdinalIgnoreCase))
            {
                var compact = Regex.Replace(body, @"\s+", "");
                return encoding.GetString(Convert.FromBase64String(compact));
            }

            if (transferEncoding.Contains("quoted-printable", StringComparison.OrdinalIgnoreCase))
                return encoding.GetString(DecodeQuotedPrintableBody(body));

            return encoding.GetString(Encoding.Latin1.GetBytes(body));
        }
        catch
        {
            return body;
        }
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

    private static string Charset(string contentType) => Parameter(contentType, "charset") ?? "utf-8";

    private static string? Parameter(string header, string name)
    {
        var match = Regex.Match(header, $@"(?:^|;)\s*{Regex.Escape(name)}\s*=\s*(""?)([^"";]+)\1", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[2].Value.Trim() : null;
    }

    private static Encoding SafeEncoding(string charset)
    {
        try
        {
            return Encoding.GetEncoding(string.IsNullOrWhiteSpace(charset) ? "utf-8" : charset);
        }
        catch
        {
            return Encoding.UTF8;
        }
    }

    private static string Header(string response, string name)
    {
        var match = Regex.Match(response, $"^{Regex.Escape(name)}:\\s*(.+)$", RegexOptions.IgnoreCase | RegexOptions.Multiline);
        return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
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
                var encoding = Encoding.GetEncoding(match.Groups[1].Value);
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
}
