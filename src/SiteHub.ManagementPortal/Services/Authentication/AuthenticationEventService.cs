using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace SiteHub.ManagementPortal.Services.Authentication;

/// <summary>
/// <see cref="IAuthenticationEventService"/> Singleton implementasyonu (F.6 Madde 9).
///
/// <para>Session bazlı tek-tetikleme: <see cref="ConcurrentDictionary{TKey, TValue}"/>
/// ile her session identifier'ı için bir bayrak tutuluyor. Aynı session'a paralel 401'ler
/// gelirse ilk tanesi event raise eder, sonrakiler yoksayılır.</para>
///
/// <para><b>Bellek yönetimi:</b> Flag'ler 30 dakika sonra otomatik temizlenir
/// (Dictionary sınırsız büyümesin). Kullanıcı tekrar login olunca yeni session
/// identifier ile gelir — eski flag bellekten de silinmiş olur.</para>
/// </summary>
internal sealed class AuthenticationEventService : IAuthenticationEventService, IDisposable
{
    // Session identifier → (flag + expiration time)
    private readonly ConcurrentDictionary<string, DateTime> _raisedSessions = new();
    private readonly ILogger<AuthenticationEventService> _logger;
    private readonly Timer _cleanupTimer;

    public AuthenticationEventService(ILogger<AuthenticationEventService> logger)
    {
        _logger = logger;
        // 10 dakikada bir eski flag'leri temizle
        _cleanupTimer = new Timer(CleanupOldEntries, null,
            TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10));
    }

    public event Func<SessionExpiredEventArgs, Task>? SessionExpired;

    public async Task RaiseSessionExpiredAsync(string sessionIdentifier)
    {
        if (string.IsNullOrWhiteSpace(sessionIdentifier))
        {
            _logger.LogDebug("SessionExpired tetiklenemedi: sessionIdentifier boş.");
            return;
        }

        // Tek-tetikleme — bu session için daha önce tetiklenmiş mi?
        var now = DateTime.UtcNow;
        if (!_raisedSessions.TryAdd(sessionIdentifier, now))
        {
            _logger.LogDebug(
                "SessionExpired tekrar tetiklendi (session={Session}), yoksayıldı.",
                TruncateForLog(sessionIdentifier));
            return;
        }

        _logger.LogInformation(
            "SessionExpired event tetiklendi (session={Session}).",
            TruncateForLog(sessionIdentifier));

        var handlers = SessionExpired;
        if (handlers is null) return;

        var args = new SessionExpiredEventArgs(sessionIdentifier);

        foreach (Func<SessionExpiredEventArgs, Task> handler in
                 handlers.GetInvocationList().Cast<Func<SessionExpiredEventArgs, Task>>())
        {
            try
            {
                await handler(args);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SessionExpired handler patladı.");
            }
        }
    }

    private void CleanupOldEntries(object? state)
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-30);
        var toRemove = _raisedSessions
            .Where(kvp => kvp.Value < cutoff)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in toRemove)
        {
            _raisedSessions.TryRemove(key, out _);
        }

        if (toRemove.Count > 0)
        {
            _logger.LogDebug("SessionExpired flag cleanup: {Count} eski kayıt silindi.",
                toRemove.Count);
        }
    }

    // Güvenlik: session id'yi log'a tam yazma (cookie değeri olabilir)
    private static string TruncateForLog(string sessionIdentifier)
        => sessionIdentifier.Length <= 8
            ? "***"
            : $"{sessionIdentifier.Substring(0, 4)}...{sessionIdentifier.Substring(sessionIdentifier.Length - 4)}";

    public void Dispose() => _cleanupTimer.Dispose();
}
