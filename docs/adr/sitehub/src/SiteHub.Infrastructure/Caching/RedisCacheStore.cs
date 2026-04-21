using System.Text.Json;
using SiteHub.Shared.Caching;
using StackExchange.Redis;

namespace SiteHub.Infrastructure.Caching;

/// <summary>
/// ICacheStore'un Redis implementasyonu (StackExchange.Redis).
///
/// Serileştirme: JSON (System.Text.Json). Primitive tipler (string, int) JSON
/// içinde basit değer olarak saklanır; karmaşık tipler tam JSON serialize edilir.
///
/// Not: Redis multiplexer uygulama ömrü boyunca tek instance olarak kullanılır
/// (singleton DI registration — bkz. DependencyInjection.cs).
/// </summary>
public sealed class RedisCacheStore : ICacheStore
{
    private readonly IConnectionMultiplexer _redis;
    private readonly JsonSerializerOptions _jsonOptions;

    public RedisCacheStore(IConnectionMultiplexer redis)
    {
        _redis = redis;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    private IDatabase Db => _redis.GetDatabase();

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var value = await Db.StringGetAsync(key);
        if (value.IsNullOrEmpty) return default;

        try
        {
            // Explicit string cast — RedisValue hem string hem byte[]'e implicit
            // dönüşebildiği için Deserialize<T> overload resolution ambiguous olur.
            return JsonSerializer.Deserialize<T>((string)value!, _jsonOptions);
        }
        catch (JsonException)
        {
            // Bozuk veri: key'i siler, null döner (self-healing).
            await Db.KeyDeleteAsync(key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var serialized = JsonSerializer.Serialize(value, _jsonOptions);
        await Db.StringSetAsync(key, serialized, ttl ?? CacheTtl.Metadata);
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        await Db.KeyDeleteAsync(key);
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return await Db.KeyExistsAsync(key);
    }

    public async Task RemoveByPatternAsync(string pattern, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        // SCAN kullanılır (KEYS yerine) — büyük veri setlerinde Redis'i bloklamaz.
        // Her endpoint'ten keys toplanır, topluca silinir.
        foreach (var endpoint in _redis.GetEndPoints())
        {
            var server = _redis.GetServer(endpoint);
            var keys = server.Keys(pattern: pattern).ToArray();
            if (keys.Length > 0)
            {
                await Db.KeyDeleteAsync(keys);
            }
        }
    }

    public async Task<T> GetOrSetAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan? ttl = null,
        CancellationToken ct = default)
    {
        var existing = await GetAsync<T>(key, ct);
        if (existing is not null) return existing;

        var value = await factory(ct);
        if (value is not null)
        {
            await SetAsync(key, value, ttl, ct);
        }
        return value!;
    }
}
