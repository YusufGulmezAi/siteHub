namespace SiteHub.Domain.Identity.Authorization;

/// <summary>
/// Bir rolün hangi bağlamda kullanılabildiği (ADR-0011 §6.3).
///
/// Scope × Membership.ContextType eşleşmeli:
/// - System scope'lu rol → sadece System context'e atanabilir
/// - Organization scope'lu rol → Organization veya Branch context
/// - Site scope'lu rol → sadece Site context
/// - ServiceOrganization scope'lu rol → ServiceOrganization context
/// </summary>
public enum RoleScope
{
    /// <summary>Sistem geneli yetki (SystemAdmin vb.). Tek bir "context" olarak düşünülür (ID = null).</summary>
    System = 1,

    /// <summary>Organizasyon (Yönetim Firması) seviyesi yetki.</summary>
    Organization = 2,

    /// <summary>Site seviyesi yetki.</summary>
    Site = 3,

    /// <summary>Servis Firması seviyesi yetki.</summary>
    ServiceOrganization = 4
}
