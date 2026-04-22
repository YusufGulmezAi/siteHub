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
/// <para><b>Faz E-Pre Gün 1 kapsamı (bu dosya):</b> Temel interface + Session entegrasyonu.
/// URL path segment (<c>/c/{type}/{id}/...</c>) ile override, Admin impersonation,
/// Resident context Gün 2+ ve Gün 3'te eklenir.</para>
/// </summary>
public sealed class HttpTenantContext : ITenantContext
{
    private readonly IHttpContextAccessor _http;

    public HttpTenantContext(IHttpContextAccessor http) => _http = http;

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
            // Organization / Site / Branch / ServiceOrg — hepsinde aktif organization var.
            // System modunda organization yok. Resident'ta da yok.
            var type = ContextType;
            if (type == TenantContextType.None || type == TenantContextType.System
                || type == TenantContextType.Resident)
                return null;

            // NOT: Session'daki Active.ContextId, ContextType=Organization ise OrganizationId,
            // ContextType=Site ise SiteId. Organization'ı ayrıca Branch/Site durumunda da
            // resolvelamak gerek — bu MVP'de Session'da ayrıca saklanmıyor.
            //
            // Gün 2'de: DB'den OrganizationId resolve eden mekanizma eklenir (cache + resolver).
            // Şimdilik sadece ContextType=Organization durumunu destekliyoruz; Site context'inde
            // OrganizationId bilinmediği için null döner (bu eksiklik bilinçli, Gün 2'de çözülecek).
            if (Active?.ContextType == MembershipContextType.Organization)
                return Active.ContextId;

            return null;
        }
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

            // Session açılışında AvailableContexts snapshot'ında System seviyesi membership
            // varsa kullanıcı System kullanıcısıdır. Context seçiminden bağımsız bilgi.
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
