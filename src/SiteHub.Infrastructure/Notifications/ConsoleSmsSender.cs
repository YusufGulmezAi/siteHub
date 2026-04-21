using Microsoft.Extensions.Logging;
using SiteHub.Application.Abstractions.Notifications;

namespace SiteHub.Infrastructure.Notifications;

/// <summary>
/// MVP placeholder — SMS'i console'a yazdırır, gerçekten göndermez.
///
/// <para>Dev + test için yeterli. Prod'da Netgsm/İletimerkezi/Twilio implementasyonu
/// <see cref="ISmsSender"/>'ı bununla swap ederek aktive edilir.</para>
///
/// <para>Log seviyesi <c>Warning</c> — dev'de console'da göze çarpsın diye.</para>
/// </summary>
public sealed class ConsoleSmsSender : ISmsSender
{
    private readonly ILogger<ConsoleSmsSender> _logger;

    public ConsoleSmsSender(ILogger<ConsoleSmsSender> logger) => _logger = logger;

    public Task SendAsync(string e164Phone, string message, CancellationToken ct = default)
    {
        _logger.LogWarning(
            """
            ═══════════════════════════════════════════════════
            [FAKE SMS] Bu mesaj gerçekten gönderilmedi.
            TO: {Phone}
            MSG: {Message}
            ═══════════════════════════════════════════════════
            """,
            e164Phone, message);

        return Task.CompletedTask;
    }
}
