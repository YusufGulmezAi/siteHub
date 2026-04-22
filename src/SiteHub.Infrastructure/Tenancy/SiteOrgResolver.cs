using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SiteHub.Application.Abstractions.Persistence;
using SiteHub.Application.Abstractions.Tenancy;
using SiteHub.Domain.Tenancy.Sites;

namespace SiteHub.Infrastructure.Tenancy;

/// <summary>
/// <see cref="ISiteOrgResolver"/>'in IMemoryCache tabanlı implementasyonu.
///
/// <para><b>Cache:</b> Global IMemoryCache (process-wide), 5 dk sliding TTL.</para>
///
/// <para><b>Thread-safety:</b> IMemoryCache thread-safe, SemaphoreSlim gerekmiyor.
/// Ender bir duplicate DB sorgusu (concurrent initial load) performans problemi değil.</para>
///
/// <para><b>Key format:</b> <c>sitehub:site-org:{siteId}</c></para>
///
/// <para><b>RLS farkında DEĞİL:</b> Resolver DB'den okurken current tenant context
/// session variable'larını dikkate almaz (AsNoTracking + global query filter
/// bypass). Çünkü kullanıcı kendi Site context'indeyken Organization'ı çözmeye
/// çalışıyor — chicken-and-egg. Bu yüzden EF Core query filter değil, ham SQL
/// kullanır (.IgnoreQueryFilters() yeterli olmayabilir çünkü RLS policy de
/// devrede olacak F.5'te).</para>
///
/// <para><b>F.5 ile etkileşim:</b> Site RLS policy'si yazıldığında resolver
/// çalışmalı — policy "is_system_user=true OR org_id match" diye kurulduğu için
/// login olmuş kullanıcı kendi Site'ını görebilir. Edge case: kullanıcı Site
/// değişmeden hemen önce logout + başka Site'e geçtiğinde cache kirli olabilir.
/// InvalidateCacheFor çağrısıyla yönetilir.</para>
/// </summary>
internal sealed class SiteOrgResolver : ISiteOrgResolver
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);
    private const string CacheKeyPrefix = "sitehub:site-org:";

    private readonly ISiteHubDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly ILogger<SiteOrgResolver> _logger;

    public SiteOrgResolver(
        ISiteHubDbContext db,
        IMemoryCache cache,
        ILogger<SiteOrgResolver> logger)
    {
        _db = db;
        _cache = cache;
        _logger = logger;
    }

    public async Task<Guid?> GetOrganizationIdAsync(Guid siteId, CancellationToken ct = default)
    {
        var cacheKey = GetCacheKey(siteId);

        if (_cache.TryGetValue<Guid>(cacheKey, out var cachedOrgId))
        {
            return cachedOrgId;
        }

        var sid = SiteId.FromGuid(siteId);

        // IgnoreQueryFilters: soft-deleted Site'lar için de resolve et
        // (cache tutarlılığı; DeletedAt kontrolü SoftDelete handler'ında
        // InvalidateCacheFor çağrılarak yönetilir).
        var orgId = await _db.Sites
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(s => s.Id == sid)
            .Select(s => (Guid?)s.OrganizationId.Value)
            .FirstOrDefaultAsync(ct);

        if (orgId is null)
        {
            // Site yok — cache'leme (null'ları cachelemek tehlikeli, race condition'da
            // yeni eklenen Site invisible kalır)
            _logger.LogDebug("Site {SiteId} resolver'da bulunamadı.", siteId);
            return null;
        }

        _cache.Set(cacheKey, orgId.Value, CacheTtl);
        _logger.LogDebug(
            "Site {SiteId} → Organization {OrgId} cache'lendi (TTL {Ttl}).",
            siteId, orgId.Value, CacheTtl);

        return orgId;
    }

    public void InvalidateCacheFor(Guid siteId)
    {
        var cacheKey = GetCacheKey(siteId);
        _cache.Remove(cacheKey);
        _logger.LogDebug("Site {SiteId} cache invalidate edildi.", siteId);
    }

    private static string GetCacheKey(Guid siteId) =>
        CacheKeyPrefix + siteId.ToString("N");
}
