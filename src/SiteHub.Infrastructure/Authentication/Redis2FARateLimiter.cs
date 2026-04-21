using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SiteHub.Application.Abstractions.Authentication;
using SiteHub.Shared.Caching;

namespace SiteHub.Infrastructure.Authentication;

/// <summary>
/// 2FA rate limiter — Redis tabanlı hesap-başına sayaç.
///
/// <para>Algoritma (sabit pencere):</para>
/// <list type="bullet">
///   <item>Key: <c>twofactor:rate:{accountId}</c></item>
///   <item>TTL = block süresi (sabit pencere)</item>
///   <item>Her yanlış girişim → sayaç +1</item>
///   <item>Eşik aşılınca ayrı bir block key set edilir (<c>twofactor:block:{accountId}</c>)</item>
///   <item>Başarılı doğrulama → her iki key silinir</item>
/// </list>
///
/// <para>Eşik + süre <see cref="LoginSecurityOptions"/> ile konfigüre edilir
/// (dev'de 1 dk, prod'da 15 dk).</para>
/// </summary>
public sealed class Redis2FARateLimiter : I2FARateLimiter
{
    private readonly ICacheStore _cache;
    private readonly TimeProvider _time;
    private readonly LoginSecurityOptions _options;
    private readonly ILogger<Redis2FARateLimiter> _logger;

    private int MaxAttempts => _options.TwoFactorMaxAttempts;
    private TimeSpan BlockDuration => _options.TwoFactorBlockDuration;

    public Redis2FARateLimiter(
        ICacheStore cache,
        TimeProvider time,
        IOptions<LoginSecurityOptions> options,
        ILogger<Redis2FARateLimiter> logger)
    {
        _cache = cache;
        _time = time;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<RateLimitStatus> CheckAsync(Guid loginAccountId, CancellationToken ct = default)
    {
        var blockUntilTicks = await _cache.GetAsync<long?>(BuildBlockKey(loginAccountId), ct);
        if (blockUntilTicks.HasValue)
        {
            var blockedUntil = new DateTimeOffset(blockUntilTicks.Value, TimeSpan.Zero);
            if (blockedUntil > _time.GetUtcNow())
            {
                return new RateLimitStatus(
                    IsBlocked: true,
                    AttemptsSoFar: MaxAttempts,
                    AttemptsRemaining: 0,
                    BlockedUntil: blockedUntil);
            }
        }

        var count = await _cache.GetAsync<int?>(BuildCounterKey(loginAccountId), ct) ?? 0;

        return new RateLimitStatus(
            IsBlocked: false,
            AttemptsSoFar: count,
            AttemptsRemaining: Math.Max(0, MaxAttempts - count),
            BlockedUntil: null);
    }

    public async Task<RateLimitStatus> RecordFailedAttemptAsync(
        Guid loginAccountId, CancellationToken ct = default)
    {
        var counterKey = BuildCounterKey(loginAccountId);
        var current = await _cache.GetAsync<int?>(counterKey, ct) ?? 0;
        var newCount = current + 1;

        await _cache.SetAsync(counterKey, newCount, BlockDuration, ct);

        if (newCount >= MaxAttempts)
        {
            var blockedUntil = _time.GetUtcNow() + BlockDuration;

            await _cache.SetAsync(
                BuildBlockKey(loginAccountId),
                blockedUntil.UtcTicks,
                BlockDuration,
                ct);

            _logger.LogWarning(
                "2FA rate limit BLOCK: accountId={AccountId}, until={Until:O}.",
                loginAccountId, blockedUntil);

            return new RateLimitStatus(
                IsBlocked: true,
                AttemptsSoFar: newCount,
                AttemptsRemaining: 0,
                BlockedUntil: blockedUntil);
        }

        _logger.LogInformation(
            "2FA rate limit: accountId={AccountId}, attempts={Count}/{Max}.",
            loginAccountId, newCount, MaxAttempts);

        return new RateLimitStatus(
            IsBlocked: false,
            AttemptsSoFar: newCount,
            AttemptsRemaining: MaxAttempts - newCount,
            BlockedUntil: null);
    }

    public async Task ResetAsync(Guid loginAccountId, CancellationToken ct = default)
    {
        await _cache.RemoveAsync(BuildCounterKey(loginAccountId), ct);
        await _cache.RemoveAsync(BuildBlockKey(loginAccountId), ct);
    }

    private static string BuildCounterKey(Guid id) => $"twofactor:rate:{id}";
    private static string BuildBlockKey(Guid id) => $"twofactor:block:{id}";
}
