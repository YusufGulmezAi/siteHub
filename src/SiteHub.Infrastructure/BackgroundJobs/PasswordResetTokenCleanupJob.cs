using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SiteHub.Application.Abstractions.Persistence;

namespace SiteHub.Infrastructure.BackgroundJobs;

/// <summary>
/// Password reset token'larını temizleyen Hangfire recurring job.
///
/// <para>Policy: <b>30 gün</b> grace period. Token <c>ExpiresAt</c>'ı geçmiş olsa bile
/// <c>CreatedAt &gt; now - 30 gün</c> ise saklanır (KVKK trace).</para>
///
/// <para>Her gece 03:00'te çalışır (Europe/Istanbul zaman dilimi).</para>
///
/// <para>Hangfire bu sınıfı DI'dan resolve eder — burada public constructor lazım.</para>
/// </summary>
public sealed class PasswordResetTokenCleanupJob
{
    public const string JobId = "password-reset-token-cleanup";

    private static readonly TimeSpan GracePeriod = TimeSpan.FromDays(30);

    private readonly ISiteHubDbContext _db;
    private readonly TimeProvider _time;
    private readonly ILogger<PasswordResetTokenCleanupJob> _logger;

    public PasswordResetTokenCleanupJob(
        ISiteHubDbContext db,
        TimeProvider time,
        ILogger<PasswordResetTokenCleanupJob> logger)
    {
        _db = db;
        _time = time;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        var threshold = _time.GetUtcNow() - GracePeriod;

        _logger.LogInformation(
            "PasswordResetTokenCleanupJob başlıyor. Threshold: {Threshold:O}", threshold);

        // CreatedAt grace period'tan daha eski olanları sil
        // (ExpiresAt'a bakmıyoruz — 30 gün önce expire olmuş token hiçbir işe yaramaz)
        var deleted = await _db.PasswordResetTokens
            .Where(t => t.CreatedAt < threshold)
            .ExecuteDeleteAsync(ct);

        _logger.LogInformation(
            "PasswordResetTokenCleanupJob tamamlandı. Silinen token sayısı: {Count}.",
            deleted);
    }
}
