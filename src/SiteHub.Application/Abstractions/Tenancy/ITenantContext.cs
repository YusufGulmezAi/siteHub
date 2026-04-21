namespace SiteHub.Application.Abstractions.Tenancy;

/// <summary>
/// Aktif tenant context — o anki request/circuit için RLS ve izolasyon bilgisi (ADR-0014 §1).
///
/// <para><b>Kullanım:</b> Infrastructure'dan read-only alınır. Command/Query handler'lar buna
/// dayanarak hangi Organization/Site için çalıştıklarını bilir. EF Core query filter'ları
/// ve PostgreSQL RLS session variable'ları bu değerlerden beslenir.</para>
///
/// <para><b>Lifecycle:</b> Scoped (per-request / per-circuit). Blazor Server'da her
/// SignalR circuit kendi instance'ını alır, bu sayede ADR-0005 §4 gereği
/// <b>aynı tarayıcıda farklı sekmelerde farklı context'ler</b> doğal olarak izole olur.</para>
///
/// <para><b>Değer kaynakları:</b></para>
/// <list type="number">
///   <item>URL path segment (<c>/c/{contextType}/{contextId}/...</c>) — birincil</item>
///   <item>Session'ın <see cref="SiteHub.Domain.Identity.Sessions.ActiveContext"/>'i — ikincil</item>
///   <item>Default: login sonrası en yüksek seviye Membership (ADR-0005 §3)</item>
/// </list>
///
/// <para><b>Null davranışı:</b> Login sayfası, 2FA bekleyen kullanıcı, logout sonrası →
/// <see cref="ContextType"/> = None, tüm Id'ler null. RLS policy'leri bu durumda
/// "hiçbir satır dönmez" (fail-closed) davranışı gösterir.</para>
/// </summary>
public interface ITenantContext
{
    /// <summary>
    /// Aktif context'in tipi. None = unauthenticated / login sayfası.
    /// </summary>
    TenantContextType ContextType { get; }

    /// <summary>
    /// Aktif organization'ın Id'si. ContextType=Organization veya Site ise dolu,
    /// System/Resident/None ise null.
    /// </summary>
    Guid? OrganizationId { get; }

    /// <summary>
    /// Aktif site'nin Id'si. Sadece ContextType=Site ise dolu.
    /// </summary>
    Guid? SiteId { get; }

    /// <summary>
    /// Resident context aktifse, login olmuş sakinin PersonId'si.
    /// Sakinin cross-tenant residency'lerine erişimini belirler (ADR-0014 §1.a).
    /// Faz L'ye kadar her zaman null döner (Resident Portal henüz yok).
    /// </summary>
    Guid? ResidentPersonId { get; }

    /// <summary>
    /// Sistem Yöneticisi "destek modunda" başka bir Organization/Site gibi görüntülüyor mu?
    /// True ise RLS bypass edilir ve tüm erişim audit'e yazılır (ADR-0014 §4).
    /// Faz E-Pre Gün 3'e kadar her zaman false döner (impersonation altyapısı sonra gelecek).
    /// </summary>
    bool IsAdminImpersonating { get; }

    /// <summary>
    /// Kullanıcı Sistem Yöneticisi mi? Bu context seçiminden bağımsız kimlik-seviyesi bilgi.
    /// True ise (impersonation olmasa bile) system-level raporlara erişim gibi ek yetkiler açılır.
    /// Session.AvailableContexts içinde System seviyesinde Membership varsa true döner.
    /// </summary>
    bool IsSystemUser { get; }

    /// <summary>
    /// Kullanıcı login olmuş durumda mı? (<see cref="ContextType"/> != None demek).
    /// Basit shorthand.
    /// </summary>
    bool IsAuthenticated { get; }
}
