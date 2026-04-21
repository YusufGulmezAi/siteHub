using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SiteHub.Application.Abstractions.Authentication;
using SiteHub.Application.Abstractions.Notifications;
using SiteHub.Application.Abstractions.Persistence;
using SiteHub.Domain.Identity;

namespace SiteHub.Application.Features.Authentication.PasswordReset;

/// <summary>
/// Şifre sıfırlama talep handler'ı (ADR-0011 §5).
///
/// <para>Akış:</para>
/// <list type="number">
///   <item>Input'u parse et (TCKN/email/telefon/VKN).</item>
///   <item>Person + LoginAccount bul.</item>
///   <item>Bulunamadıysa veya seçili kanal adresi yoksa → <b>sessizce başarılı dön</b>
///   (enumeration defense). Log'a yaz.</item>
///   <item>Bu LoginAccount'un eski açık token'larını invalidate et (UsedAt=now).</item>
///   <item>Yeni token üret + hash'le + DB'ye kaydet.</item>
///   <item>Email ise linki, SMS ise 6 haneli kodu gönder.</item>
/// </list>
///
/// <para>Her durumda aynı sonuç döner — UI "talimatlar gönderildi" mesajı gösterir.</para>
/// </summary>
public sealed class RequestPasswordResetHandler
    : IRequestHandler<RequestPasswordResetCommand, RequestPasswordResetResult>
{
    // Token yaşam süresi — ADR-0011 §5.2 varsayılan
    private static readonly TimeSpan TokenTtl = TimeSpan.FromMinutes(15);

    private readonly ISiteHubDbContext _db;
    private readonly IEmailSender _emailSender;
    private readonly ISmsSender _smsSender;
    private readonly TimeProvider _time;
    private readonly ILogger<RequestPasswordResetHandler> _logger;

    public RequestPasswordResetHandler(
        ISiteHubDbContext db,
        IEmailSender emailSender,
        ISmsSender smsSender,
        TimeProvider time,
        ILogger<RequestPasswordResetHandler> logger)
    {
        _db = db;
        _emailSender = emailSender;
        _smsSender = smsSender;
        _time = time;
        _logger = logger;
    }

    public async Task<RequestPasswordResetResult> Handle(
        RequestPasswordResetCommand command, CancellationToken ct)
    {
        var now = _time.GetUtcNow();

        // 1. Input parse
        var inputType = LoginInputParser.Detect(command.Input);
        if (inputType == LoginInputType.Unknown)
        {
            _logger.LogInformation(
                "PasswordReset: input formatı tanınmadı (IP: {Ip}).", command.IpAddress);
            return new RequestPasswordResetResult();
        }

        var normalizedInput = LoginInputParser.Normalize(command.Input, inputType);

        // 2. Person bul
        var person = await FindPersonAsync(normalizedInput, inputType, ct);
        if (person is null)
        {
            _logger.LogInformation(
                "PasswordReset: Person bulunamadı (type={Type}, IP: {Ip}).",
                inputType, command.IpAddress);
            return new RequestPasswordResetResult();
        }

        // 3. LoginAccount bul
        var account = await _db.LoginAccounts
            .FirstOrDefaultAsync(a => a.PersonId == person.Id, ct);

        if (account is null)
        {
            _logger.LogInformation(
                "PasswordReset: LoginAccount yok (personId={PersonId}).", person.Id);
            return new RequestPasswordResetResult();
        }

        // 4. Kanal hedef adresi var mı?
        string? targetAddress = command.Channel switch
        {
            ResetChannelChoice.Email => account.LoginEmail,
            ResetChannelChoice.Sms => person.MobilePhone,
            _ => null
        };

        if (string.IsNullOrWhiteSpace(targetAddress))
        {
            _logger.LogWarning(
                "PasswordReset: {Channel} kanalı için adres yok (accountId={AccountId}).",
                command.Channel, account.Id);
            return new RequestPasswordResetResult();
        }

        // 5. Eski açık token'ları invalidate
        var activeTokens = await _db.PasswordResetTokens
            .Where(t => t.LoginAccountId == account.Id && t.UsedAt == null && t.ExpiresAt > now)
            .ToListAsync(ct);

        foreach (var oldToken in activeTokens)
        {
            oldToken.MarkAsUsed(now, fromIp: null);
        }

        // 6. Yeni token üret (kanal-bazlı)
        var (plainToken, userVisibleCode) = command.Channel switch
        {
            ResetChannelChoice.Email => (TokenHasher.GenerateToken(), (string?)null),
            ResetChannelChoice.Sms   => (TokenHasher.GenerateNumericCode(), (string?)null),
            _ => throw new ArgumentOutOfRangeException()
        };

        var tokenHash = TokenHasher.Hash(plainToken);

        var domainChannel = command.Channel switch
        {
            ResetChannelChoice.Email => PasswordResetChannel.Email,
            ResetChannelChoice.Sms => PasswordResetChannel.Sms,
            _ => throw new ArgumentOutOfRangeException()
        };

        var token = PasswordResetToken.Create(
            loginAccountId: account.Id,
            tokenHash: tokenHash,
            channel: domainChannel,
            ttl: TokenTtl,
            now: now,
            requestedFromIp: command.IpAddress);

        _db.PasswordResetTokens.Add(token);
        await _db.SaveChangesAsync(ct);

        // 7. Gönder
        try
        {
            if (command.Channel == ResetChannelChoice.Email)
            {
                var resetUrl = BuildResetUrl(plainToken);
                var html = BuildResetEmail(person.FullName, resetUrl);
                await _emailSender.SendAsync(
                    to: targetAddress,
                    subject: "SiteHub — Şifre Sıfırlama Talebi",
                    htmlBody: html,
                    ct: ct);
            }
            else // Sms
            {
                var msg = $"SiteHub şifre sıfırlama kodunuz: {plainToken}. Bu kod 15 dakika geçerlidir.";
                await _smsSender.SendAsync(targetAddress, msg, ct);
            }

            _logger.LogInformation(
                "PasswordReset token gönderildi: accountId={AccountId}, channel={Channel}.",
                account.Id, command.Channel);
        }
        catch (Exception ex)
        {
            // Gönderim hatası — token yine DB'de var, kullanıcı tekrar deneyebilir.
            // Ama sonuç yine aynı: "talimatlar gönderildi" görünür (UX tutarlılığı + defans).
            _logger.LogError(ex,
                "PasswordReset gönderim hatası: accountId={AccountId}, channel={Channel}.",
                account.Id, command.Channel);
        }

        return new RequestPasswordResetResult();
    }

    // ─── Helpers ──────────────────────────────────────────────────────────

    private Task<Person?> FindPersonAsync(string normalizedInput, LoginInputType type, CancellationToken ct)
    {
        if (type is LoginInputType.Tckn or LoginInputType.Vkn or LoginInputType.Ykn)
        {
            NationalId id;
            try
            {
                id = type switch
                {
                    LoginInputType.Tckn => NationalId.CreateTckn(normalizedInput),
                    LoginInputType.Vkn  => NationalId.CreateVkn(normalizedInput),
                    LoginInputType.Ykn  => NationalId.CreateYkn(normalizedInput),
                    _ => throw new InvalidOperationException()
                };
            }
            catch
            {
                return Task.FromResult<Person?>(null);
            }

            return _db.Persons.FirstOrDefaultAsync(p => p.NationalId == id, ct);
        }

        return type switch
        {
            LoginInputType.Email => _db.Persons.FirstOrDefaultAsync(p => p.Email == normalizedInput, ct),
            LoginInputType.Mobile => _db.Persons.FirstOrDefaultAsync(p => p.MobilePhone == normalizedInput, ct),
            _ => Task.FromResult<Person?>(null)
        };
    }

    private static string BuildResetUrl(string token)
    {
        // MVP: localhost dev URL. İleride app base URL config'ten gelir.
        return $"http://localhost:5000/reset-password?token={Uri.EscapeDataString(token)}";
    }

    private static string BuildResetEmail(string fullName, string resetUrl)
    {
        return $"""
            <html>
              <body style="font-family: Arial, sans-serif; max-width: 600px; margin: auto;">
                <h2 style="color: #6D28D9;">SiteHub — Şifre Sıfırlama</h2>
                <p>Merhaba {System.Net.WebUtility.HtmlEncode(fullName)},</p>
                <p>Hesabınız için bir şifre sıfırlama talebi aldık. Aşağıdaki bağlantıya tıklayarak yeni şifrenizi belirleyebilirsiniz:</p>
                <p style="margin: 24px 0;">
                  <a href="{resetUrl}"
                     style="background: #6D28D9; color: white; padding: 12px 24px; text-decoration: none; border-radius: 8px; display: inline-block;">
                    Şifremi Sıfırla
                  </a>
                </p>
                <p>Veya bu bağlantıyı tarayıcınıza kopyalayın:</p>
                <p style="word-break: break-all; color: #6B7280; font-size: 13px;">{resetUrl}</p>
                <p style="color: #EF4444; margin-top: 24px;">
                  ⏰ Bu bağlantı <b>15 dakika</b> boyunca geçerlidir.
                </p>
                <p style="color: #6B7280; font-size: 13px; margin-top: 32px; border-top: 1px solid #E5E7EB; padding-top: 16px;">
                  Bu talebi siz yapmadıysanız bu e-postayı yok sayabilirsiniz. Şifreniz değişmeyecektir.
                </p>
              </body>
            </html>
            """;
    }
}
