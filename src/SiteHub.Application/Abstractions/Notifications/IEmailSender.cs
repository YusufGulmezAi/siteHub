namespace SiteHub.Application.Abstractions.Notifications;

/// <summary>
/// E-posta gönderme soyutlaması.
///
/// <para>Dev'de implementation: <c>SmtpEmailSender</c> → MailHog (localhost:1025).
/// Prod'da: SendGrid / AWS SES / Microsoft Graph gibi bir sağlayıcı.</para>
///
/// <para>Fire-and-forget değildir — exception fırlatabilir (SMTP hatası vb.).
/// Caller gerekirse try/catch eder. Email gönderimi başarısız olursa password reset
/// flow'unda kullanıcıya genel hata gösteririz.</para>
/// </summary>
public interface IEmailSender
{
    /// <summary>
    /// HTML içerikli e-posta gönderir.
    /// </summary>
    /// <param name="to">Alıcı adresi (tek). Bulk için ayrı method eklenir.</param>
    /// <param name="subject">Konu.</param>
    /// <param name="htmlBody">HTML body. Plain text alternatifi otomatik üretilir.</param>
    Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default);
}
