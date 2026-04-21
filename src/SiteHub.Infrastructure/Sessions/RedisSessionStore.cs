using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using SiteHub.Application.Abstractions.Sessions;
using SiteHub.Domain.Identity;
using SiteHub.Domain.Identity.Sessions;
using SiteHub.Shared.Caching;
using StackExchange.Redis;

namespace SiteHub.Infrastructure.Sessions;

/// <summary>
/// Redis-tabanlı session deposu (ADR-0011 §7.1).
///
/// <para>Key şeması:</para>
/// <list type="bullet">
///   <item><c>session:{sessionId}</c> → Session JSON, 15 dakika sliding TTL</item>
///   <item><c>user:{loginAccountId}:sessions</c> → SADD ile sessionId set'i,
///         tek-oturum kuralı için ikincil indeks</item>
/// </list>
///
/// <para>CONCURRENCY: Redis transaction (MULTI) kullanılmıyor — "tek oturum" için küçük
/// yarış olabilir ama pratikte sorun değil çünkü yeni login sırasında eski session'ları
/// silme işlemi idempotent. Gerekirse ileride Lua script'e dönüştürülür.</para>
///
/// <para>JSON serialization: ActiveContext ve MembershipSummary record'ları normal serialize
/// olur. HashSet/IReadOnlySet için System.Text.Json default handler yeterli.</para>
/// </summary>
public sealed class RedisSessionStore : ISessionStore
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisSessionStore> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    // 15 dakika sliding TTL — her Get/Update uzatır.
    private static readonly TimeSpan SessionTtl = TimeSpan.FromMinutes(15);

    public RedisSessionStore(
        IConnectionMultiplexer redis,
        ILogger<RedisSessionStore> logger)
    {
        _redis = redis;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
    }

    private IDatabase Db => _redis.GetDatabase();

    public async Task SaveAsync(Session session, CancellationToken ct = default)
    {
        var key = SessionKey(session.SessionId);
        var indexKey = UserSessionsKey(session.LoginAccountId);

        var json = JsonSerializer.Serialize(session, _jsonOptions);

        var batch = Db.CreateBatch();
        var t1 = batch.StringSetAsync(key, json, SessionTtl);
        var t2 = batch.SetAddAsync(indexKey, session.SessionId.ToString());
        var t3 = batch.KeyExpireAsync(indexKey, TimeSpan.FromDays(1)); // secondary index TTL
        batch.Execute();

        await Task.WhenAll(t1, t2, t3).WaitAsync(ct);

        _logger.LogInformation(
            "Session oluşturuldu: {SessionId} (user={LoginAccountId}).",
            session.SessionId, session.LoginAccountId);
    }

    public async Task<Session?> GetAsync(SessionId sessionId, CancellationToken ct = default)
    {
        var key = SessionKey(sessionId);
        var value = await Db.StringGetAsync(key).WaitAsync(ct);

        if (!value.HasValue) return null;

        try
        {
            // Sliding TTL — her okumada 15 dk uzat
            await Db.KeyExpireAsync(key, SessionTtl).WaitAsync(ct);
            return JsonSerializer.Deserialize<Session>((string)value!, _jsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex,
                "Session {SessionId} deserialize edilemedi, siliniyor.", sessionId);
            await Db.KeyDeleteAsync(key).WaitAsync(ct);
            return null;
        }
    }

    public async Task UpdateAsync(Session session, CancellationToken ct = default)
    {
        var key = SessionKey(session.SessionId);
        var json = JsonSerializer.Serialize(session, _jsonOptions);
        await Db.StringSetAsync(key, json, SessionTtl).WaitAsync(ct);
    }

    public async Task DeleteAsync(SessionId sessionId, CancellationToken ct = default)
    {
        var key = SessionKey(sessionId);
        var existing = await Db.StringGetAsync(key).WaitAsync(ct);

        if (existing.HasValue)
        {
            // Index'ten de sil
            try
            {
                var session = JsonSerializer.Deserialize<Session>((string)existing!, _jsonOptions);
                if (session is not null)
                {
                    await Db.SetRemoveAsync(
                        UserSessionsKey(session.LoginAccountId),
                        sessionId.ToString()).WaitAsync(ct);
                }
            }
            catch
            {
                // Bozuk JSON → sadece main key silinir, zararsız
            }

            await Db.KeyDeleteAsync(key).WaitAsync(ct);
            _logger.LogInformation("Session silindi: {SessionId}.", sessionId);
        }
    }

    public async Task<IReadOnlyList<SessionId>> DeleteByLoginAccountAsync(
        LoginAccountId loginAccountId,
        CancellationToken ct = default)
    {
        var indexKey = UserSessionsKey(loginAccountId);
        var members = await Db.SetMembersAsync(indexKey).WaitAsync(ct);

        if (members.Length == 0) return Array.Empty<SessionId>();

        var result = new List<SessionId>(members.Length);

        foreach (var member in members)
        {
            if (SessionId.TryParse((string)member!, out var sid))
            {
                await Db.KeyDeleteAsync(SessionKey(sid)).WaitAsync(ct);
                result.Add(sid);
            }
        }

        await Db.KeyDeleteAsync(indexKey).WaitAsync(ct);

        if (result.Count > 0)
        {
            _logger.LogInformation(
                "Kullanıcıya ait {Count} session silindi (user={LoginAccountId}).",
                result.Count, loginAccountId);
        }

        return result;
    }

    private static string SessionKey(SessionId sessionId)
        => CacheKeys.Session.For(sessionId.ToString());

    private static string UserSessionsKey(LoginAccountId loginAccountId)
        => CacheKeys.Session.UserSessions(loginAccountId.Value);
}
