using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using SiteHub.Application.Abstractions.Notifications;

namespace SiteHub.Infrastructure.Notifications;

/// <summary>
/// MailKit tabanlı SMTP e-posta gönderici.
///
/// <para>Dev: localhost:1025 (MailHog — tüm mail'leri yakalar, gerçek gönderim yok).
/// UI: <c>http://localhost:8025</c>'te görüntülenir.</para>
///
/// <para>Prod: SendGrid/AWS SES/vb. SMTP relay — config'te Host/Port/Credentials değişir.</para>
///
/// <para>Plain text alternatifi HTML'den otomatik türetilir (basit strip).</para>
/// </summary>
public sealed class SmtpEmailSender : IEmailSender
{
    private readonly EmailOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(
        string to,
        string subject,
        string htmlBody,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(to))
            throw new ArgumentException("Alıcı adresi boş olamaz.", nameof(to));

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;

        var builder = new BodyBuilder
        {
            HtmlBody = htmlBody,
            TextBody = StripHtml(htmlBody)
        };
        message.Body = builder.ToMessageBody();

        using var client = new SmtpClient();

        try
        {
            var secureSocket = _options.UseSsl
                ? SecureSocketOptions.StartTlsWhenAvailable
                : SecureSocketOptions.None;

            await client.ConnectAsync(_options.Host, _options.Port, secureSocket, ct);

            if (!string.IsNullOrEmpty(_options.Username))
                await client.AuthenticateAsync(_options.Username, _options.Password, ct);

            await client.SendAsync(message, ct);
            await client.DisconnectAsync(quit: true, ct);

            _logger.LogInformation(
                "E-posta gönderildi: to={To}, subject={Subject}", to, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "E-posta gönderim hatası: to={To}, subject={Subject}", to, subject);
            throw;
        }
    }

    /// <summary>Basit HTML → text dönüşümü (fallback için, plain text client'lar için).</summary>
    private static string StripHtml(string html)
    {
        if (string.IsNullOrEmpty(html)) return string.Empty;
        return System.Text.RegularExpressions.Regex.Replace(html, "<.*?>", string.Empty);
    }
}
