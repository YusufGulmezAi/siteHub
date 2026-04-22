using Microsoft.AspNetCore.Http;
using SiteHub.Application.Abstractions.Tenancy;
using SiteHub.Domain.Identity.Authorization;
using SiteHub.Domain.Identity.Sessions;
using SiteHub.Infrastructure.Authentication;

namespace SiteHub.Infrastructure.Tenancy;

/// <summary>
/// <see cref="ITenantContext"/>'in HTTP tabanlı implementasyonu (ADR-0014 §1).
///
/// <para>Session'ı <c>SessionValidationMiddleware</c>'in doldurduğu
/// <c>HttpContext.Items["SiteHub:Session"]</c> slotundan okur, içindeki
/// <see cref="ActiveContext"/>'ten değerleri çıkarır.</para>
///
/// <para><b>Lifecycle:</b> Scoped — Blazor Server'da her SignalR circuit kendi instance'ını alır.
/// Bu sayede aynı tarayıcının farklı sekmeleri farklı context'te çalışabilir (ADR-0005 §4).</para>
///
/// <para><b>Null-safety:</b> Session yoksa (login sayfası, 2FA bekleyen, logout sonrası)
/// tüm alanlar varsayılan değerler döner: ContextType=None, Id'ler null,
/// IsAdminImpersonating=false, IsSystemUser=false. Bu "fail-closed" davranış RLS
/// açısından güvenli: PostgreSQL session variable'ı NULL olunca hiçbir satır
/// döndürülmez (ADR-0002-v2 §3).</para>
///
/// <para><b>Faz F.4:</b> Site context'te <see cref="OrganizationId"/> artık
/// <see cref="ISiteOrgResolver"/> ile DB'den çözülüyor. Resolver'ın kendi IMemoryCache'i
/// var (5 dk TTL). Ayrıca per-request cache HttpContext.Items'da tutulur
/// (aynı request içinde tekrar DB lookup yapılmaz).</para>
/// </summary>
public sealed class HttpTenantContext : ITenantContext
{
    private const string PerRequestOrgIdCacheKey = "SiteHub:Tenant:ResolvedOrgId";

    private readonly IHttpContextAccessor _http;
    private readonly ISiteOrgResolver _siteOrgResolver;

    public HttpTenantContext(IHttpContextAccessor http, ISiteOrgResolver siteOrgResolver)
    {
        _http = http;
        _siteOrgResolver = siteOrgResolver;
    }

    private Session? Session =>
        _http.HttpContext?.Items[SessionValidationMiddleware.SessionContextKey] as Session;

    private ActiveContext? Active => Session?.ActiveContext;

    public bool IsAuthenticated => Session is not null && !Session.Pending2FA;

    public TenantContextType ContextType
    {
        get
        {
            if (!IsAuthenticated) return TenantContextType.None;
            if (Active is null) return TenantContextType.None;

            // MembershipContextType → TenantContextType mapping
            // Organization/Branch/ServiceOrganization hepsi "Organization" tenant kategorisine düşer.
            // Site tek başına ayrı kategori.
            return Active.ContextType switch
            {
                MembershipContextType.System => TenantContextType.System,
                MembershipContextType.Site => TenantContextType.Site,
                MembershipContextType.Organization => TenantContextType.Organization,
                MembershipContextType.Branch => TenantContextType.Organization,
                MembershipContextType.ServiceOrganization => TenantContextType.Organization,
                _ => TenantContextType.None,
            };
        }
    }

    public Guid? OrganizationId
    {
        get
        {
            var type = ContextType;
            if (type == TenantContextType.None || type == TenantContextType.System
                || type == TenantContextType.Resident)
                return null;

            // Organization context: ContextId doğrudan org id'dir
            if (Active?.ContextType == MembershipContextType.Organization)
                return Active.ContextId;

            // Site context: ContextId site id'dir, parent org'u resolver ile çöz.
            // ServiceOrganization/Branch: Faz F sonrası ele alınır (entity'ler yok).
            if (Active?.ContextType == MembershipContextType.Site && Active.ContextId.HasValue)
                return ResolveOrgForSiteContext(Active.ContextId.Value);

            return null;
        }
    }

    /// <summary>
    /// Site context'te parent OrganizationId'yi çözer. İki katmanlı cache:
    /// 1. Per-request: HttpContext.Items (aynı request tekrar DB'ye gitmez)
    /// 2. Global: IMemoryCache (process-wide, 5 dk TTL, SiteOrgResolver içinde)
    /// </summary>
    private Guid? ResolveOrgForSiteContext(Guid siteId)
    {
        var ctx = _http.HttpContext;
        if (ctx is not null && ctx.Items.TryGetValue(PerRequestOrgIdCacheKey, out var cached)
            && cached is Guid cachedGuid)
        {
            return cachedGuid;
        }

        // Sync-over-async: property getter içinde zorunlu.
        // Cache hit durumunda sync, miss durumunda DB sorgusu (async çalışır, await yok).
        // ASP.NET Core'da property'lerden sync-wait tipik ve kabul edilebilir
        // (Blazor Server SignalR'da risk minimal — circuit thread pool kullanır).
        var resolved = _siteOrgResolver
            .GetOrganizationIdAsync(siteId, CancellationToken.None)
            .GetAwaiter().GetResult();

        if (ctx is not null && resolved.HasValue)
        {
            ctx.Items[PerRequestOrgIdCacheKey] = resolved.Value;
        }

        return resolved;
    }

    public Guid? SiteId
    {
        get
        {
            if (Active?.ContextType == MembershipContextType.Site)
                return Active.ContextId;
            return null;
        }
    }

    /// <summary>
    /// Resident Portal henüz yok (Faz L). Şimdilik her zaman null.
    /// </summary>
    public Guid? ResidentPersonId => null;

    /// <summary>
    /// Admin impersonation altyapısı Faz E-Pre Gün 3'te eklenecek. Şimdilik her zaman false.
    /// </summary>
    public bool IsAdminImpersonating => false;

    public bool IsSystemUser
    {
        get
        {
            var session = Session;
            if (session is null || session.Pending2FA) return false;

            return session.AvailableContexts
                .Any(m => m.ContextType == (int)MembershipContextType.System);
        }
    }

    public Guid? LoginAccountId
    {
        get
        {
            var session = Session;
            if (session is null || session.Pending2FA) return null;
            return session.LoginAccountId.Value;
        }
    }
}
