using Hangfire;
using Microsoft.Extensions.DependencyInjection;

namespace SiteHub.Infrastructure.BackgroundJobs;

/// <summary>
/// Uygulama başlangıcında Hangfire recurring job'larını kaydeder.
///
/// <para>Hangfire'ın <c>IRecurringJobManager</c>'ı server başlatıldıktan sonra
/// hazır olur. Bu extension <c>app.Services</c> üzerinden DI scope açar ve
/// job'ları idempotent şekilde register eder (mevcutsa günceller, yoksa ekler).</para>
/// </summary>
public static class RecurringJobsRegistration
{
    public static void RegisterSiteHubRecurringJobs(this IServiceProvider services)
    {
        var manager = services.GetRequiredService<IRecurringJobManager>();

        // Her gece 03:00 (Europe/Istanbul) — password reset token cleanup
        manager.AddOrUpdate<PasswordResetTokenCleanupJob>(
            recurringJobId: PasswordResetTokenCleanupJob.JobId,
            methodCall: job => job.ExecuteAsync(CancellationToken.None),
            cronExpression: "0 3 * * *",
            options: new RecurringJobOptions
            {
                TimeZone = FindTurkeyTimeZone(),
                MisfireHandling = MisfireHandlingMode.Relaxed,
            });
    }

    /// <summary>
    /// Platform farkı için toleranslı TZ çözümü.
    /// Linux: "Europe/Istanbul"
    /// Windows: "Turkey Standard Time" veya "Europe/Istanbul" (Win10+ ICU)
    /// </summary>
    private static TimeZoneInfo FindTurkeyTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul"); }
        catch (TimeZoneNotFoundException) { }

        try { return TimeZoneInfo.FindSystemTimeZoneById("Turkey Standard Time"); }
        catch (TimeZoneNotFoundException) { }

        // UTC+3 sabit fallback (Türkiye DST uygulamıyor)
        return TimeZoneInfo.CreateCustomTimeZone("Turkey", TimeSpan.FromHours(3), "Turkey", "Turkey");
    }
}
