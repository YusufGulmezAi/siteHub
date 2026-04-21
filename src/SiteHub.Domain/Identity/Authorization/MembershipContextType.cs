namespace SiteHub.Domain.Identity.Authorization;

/// <summary>
/// Membership'in hangi bağlama (context) verildiği (ADR-0011 §9).
///
/// RoleScope ile eşleşir ama Branch eklenmiştir:
/// - System     → ContextId null
/// - Organization → ContextId = OrganizationId
/// - Branch     → ContextId = BranchId (v2'de branch tablosu gelir; MVP'de var ama kullanılmıyor)
/// - Site       → ContextId = SiteId
/// - ServiceOrganization → ContextId = ServiceOrganizationId
/// </summary>
public enum MembershipContextType
{
    System = 1,
    Organization = 2,
    Branch = 3,
    Site = 4,
    ServiceOrganization = 5
}
