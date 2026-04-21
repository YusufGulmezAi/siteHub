namespace SiteHub.Shared.Caching;

/// <summary>
/// Uygulama genelinde generic cache arayüzü.
/// Implementation: RedisCacheStore (Infrastructure), InMemoryCacheStore (test).
///
/// Kullanım alanları (ADR-0011 + ADR-0016):
/// - User permissions cache (user:permissions:{loginAccountId})
/// - Active sessions (session:{sessionId})
/// - Reference data cache (addresses, banks — nadir değişir)
/// - Organization / Site metadata cache
///
/// Key naming convention: {namespace}:{entity}:{id} — örn. "user:permissions:01908a..."
/// </summary>
public interface ICacheStore
{
    /// <summary>Belirli bir key'in değerini getirir; yoksa default döner.</summary>
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);

    /// <summary>
    /// Değer atar. TTL belirtilmezse cache'in default TTL'si kullanılır (15 dk — ADR-0011).
    /// </summary>
    Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken ct = default);

    /// <summary>Key'i siler. Key yoksa hata vermez.</summary>
    Task RemoveAsync(string key, CancellationToken ct = default);

    /// <summary>Key mevcut mu?</summary>
    Task<bool> ExistsAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Pattern eşleşen tüm key'leri siler. Örn: "user:permissions:*".
    /// DİKKAT: Redis'te KEYS/SCAN operasyonu pahalıdır — dikkatli kullanın.
    /// </summary>
    Task RemoveByPatternAsync(string pattern, CancellationToken ct = default);

    /// <summary>
    /// Get-or-set atomik kombinasyonu. Cache miss durumunda factory'yi çağırır,
    /// sonucu cache'ler ve döner.
    /// </summary>
    Task<T> GetOrSetAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan? ttl = null,
        CancellationToken ct = default);
}
