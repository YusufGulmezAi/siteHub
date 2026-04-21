namespace SiteHub.Application.Abstractions.Tenancy;

/// <summary>
/// Kullanıcının aktif çalıştığı tenant context'inin tipi (ADR-0014 §1).
///
/// <para>NOT: Bu enum <see cref="SiteHub.Domain.Identity.Authorization.MembershipContextType"/>
/// ile KARIŞTIRILMAMALIDIR. İkisi farklı kavram:</para>
/// <list type="bullet">
///   <item><c>MembershipContextType</c>: Kullanıcının kimlik seviyesindeki rolü (System, Organization, Site, ServiceOrganization) —
///         Membership tablosunda saklanır.</item>
///   <item><c>TenantContextType</c>: O an'ki RLS/izolasyon context'i (System, Organization, Site, Resident) —
///         session + URL path segment'ten hesaplanır, RLS policy'lerini yönetir.</item>
/// </list>
/// </summary>
public enum TenantContextType
{
    /// <summary>Context henüz belirlenmemiş (login sayfası, logout sonrası).</summary>
    None = 0,

    /// <summary>SiteHub internal — Sistem Yöneticisi, tüm tenant'ları yönetebilir (ADR-0014 §4 impersonation ile).</summary>
    System = 1,

    /// <summary>Yönetim firması seviyesinde çalışma (organization-scoped veriler, altındaki siteler).</summary>
    Organization = 2,

    /// <summary>Belirli bir site seviyesinde çalışma (site-scoped veriler: aidat, duyuru vb.).</summary>
    Site = 3,

    /// <summary>Sakin (malik/kiracı) modu — cross-tenant erişim, sadece kendi residency'leri (ADR-0014 §1.a).</summary>
    Resident = 4
}
