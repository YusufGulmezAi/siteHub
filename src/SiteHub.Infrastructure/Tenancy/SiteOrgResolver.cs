using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SiteHub.Application.Abstractions.Persistence;
using SiteHub.Application.Abstractions.Tenancy;
using SiteHub.Domain.Tenancy.Sites;

namespace SiteHub.Infrastructure.Tenancy;

/// <summary>
/// <see cref="ISiteOrgResolver"/>'in IMemoryCache tabanlı implementasyonu.
///
/// <para><b>Lazy DbContext:</b> Constructor'da direkt <c>ISiteHubDbContext</c> alınmaz —
/// çünkü DbContext'in çözümü <c>TenantContextConnectionInterceptor</c> → <c>ITenantContext</c> →
/// <c>HttpTenantContext</c> → <c>ISiteOrgResolver</c> döngüsünü tetikleyebilir. Bunun yerine
/// <see cref="IServiceProvider"/> inject edilir, DbContext sadece cache miss'te resolve edilir.</para>
///
/// <para><b>Cache:</b> Global IMemoryCache (process-wide), 5 dk sliding TTL.</para>
/// <para><b>Cache key:</b> <c>sitehub:site-org:{siteId}</c></para>
/// </summary>
internal sealed class SiteOrgResolver : ISiteOrgResolver
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);
    private const string CacheKeyPrefix = "sitehub:site-org:";

    private readonly IServiceProvider _services;
    private readonly IMemoryCache _cache;
    private readonly ILogger<SiteOrgResolver> _logger;

    public SiteOrgResolver(
        IServiceProvider services,
        IMemoryCache cache,
        ILogger<SiteOrgResolver> logger)
    {
        _services = services;
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

        // Lazy resolve — circular dependency'yi kırar
        var db = _services.GetRequiredService<ISiteHubDbContext>();

        var orgId = await db.Sites
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(s => s.Id == sid)
            .Select(s => (Guid?)s.OrganizationId.Value)
            .FirstOrDefaultAsync(ct);

        if (orgId is null)
        {
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
