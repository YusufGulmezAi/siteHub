namespace SiteHub.Application.Abstractions.Authentication;

/// <summary>
/// 2FA kod doğrulama girişimlerini sınırlayan servis.
///
/// <para>ADR-0011 §4.5 + login lockout ile tutarlı:</para>
/// <list type="bullet">
///   <item>5 yanlış girişim → 15 dk block</item>
///   <item>Sayaç Redis'te (hızlı, cross-instance paylaşımlı)</item>
///   <item>Başarılı doğrulama → sayaç sıfırlanır</item>
/// </list>
///
/// <para>Login lockout (LoginAccount.FailedLoginCount) parola için; bu ayrı bir
/// sayaç — 2FA kodu için. İkisi bağımsız çalışır.</para>
/// </summary>
public interface I2FARateLimiter
{
    /// <summary>
    /// Mevcut deneme sayısı + block kontrolü.
    /// </summary>
    Task<RateLimitStatus> CheckAsync(Guid loginAccountId, CancellationToken ct = default);

    /// <summary>
    /// Yanlış girişim kaydet. Eşik aşılınca block başlatır, geri kalan süreyi döner.
    /// </summary>
    Task<RateLimitStatus> RecordFailedAttemptAsync(Guid loginAccountId, CancellationToken ct = default);

    /// <summary>Sayacı sıfırlar (başarılı girişim veya admin reset sonrası).</summary>
    Task ResetAsync(Guid loginAccountId, CancellationToken ct = default);
}

/// <summary>
/// Rate limit durumu.
/// </summary>
/// <param name="IsBlocked">Şu an block'lı mı?</param>
/// <param name="AttemptsSoFar">Bu pencerede kaç yanlış girişim olmuş?</param>
/// <param name="AttemptsRemaining">Block tetiklenene kadar kaç girişim hakkı kaldı?</param>
/// <param name="BlockedUntil">Block bittiği zaman (UTC). <c>IsBlocked=false</c> ise null.</param>
public sealed record RateLimitStatus(
    bool IsBlocked,
    int AttemptsSoFar,
    int AttemptsRemaining,
    DateTimeOffset? BlockedUntil);
