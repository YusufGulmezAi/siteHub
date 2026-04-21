using SiteHub.Domain.Identity;
using SiteHub.Domain.Identity.Sessions;

namespace SiteHub.Application.Abstractions.Sessions;

/// <summary>
/// Session depolama soyutlaması. Implementation: Redis (Infrastructure/Sessions/RedisSessionStore).
///
/// <para>ADR-0011 §7.2 "Tek IP, Tek Cihaz, Tek Oturum" kuralı:</para>
/// <list type="bullet">
///   <item>Her kullanıcının yalnızca TEK aktif session'ı olabilir</item>
///   <item>Yeni login → eski session'ları kapat (DeleteByLoginAccountAsync)</item>
///   <item>GetAsync Redis TTL'ini uzatır (sliding expiration, 15 dk)</item>
/// </list>
///
/// <para>Redis key şeması:</para>
/// <list type="bullet">
///   <item>session:{sessionId} → Session JSON (15 dk TTL)</item>
///   <item>user:{loginAccountId}:sessions → Set&lt;sessionId&gt; (secondary index — "tek oturum" kontrolü için)</item>
/// </list>
/// </summary>
public interface ISessionStore
{
    /// <summary>
    /// Session oluşturur + Redis'e yazar. TTL 15 dakika.
    /// Aynı LoginAccount'un eski session'ları önce kapatılır (tek session kuralı).
    /// </summary>
    Task SaveAsync(Session session, CancellationToken ct = default);

    /// <summary>
    /// Session'ı Redis'ten oku + TTL'i yenile (sliding).
    /// null = session yok veya expire olmuş.
    /// </summary>
    Task<Session?> GetAsync(SessionId sessionId, CancellationToken ct = default);

    /// <summary>
    /// Session'ı günceller (ActiveContext değişimi, LastActivityAt touch, vs.).
    /// TTL de sliding olarak yenilenir.
    /// </summary>
    Task UpdateAsync(Session session, CancellationToken ct = default);

    /// <summary>Session'ı siler (logout, IP change, device mismatch).</summary>
    Task DeleteAsync(SessionId sessionId, CancellationToken ct = default);

    /// <summary>
    /// Bir kullanıcıya ait TÜM session'ları siler.
    /// Kullanım: Yeni login (tek oturum), parola değişimi (tüm cihazlardan logout).
    /// </summary>
    /// <returns>Silinen session ID'lerinin listesi (SignalR broadcast için).</returns>
    Task<IReadOnlyList<SessionId>> DeleteByLoginAccountAsync(
        LoginAccountId loginAccountId,
        CancellationToken ct = default);
}
