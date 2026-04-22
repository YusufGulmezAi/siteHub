using SiteHub.Contracts.Common;
using SiteHub.Contracts.Organizations;

namespace SiteHub.ManagementPortal.Services.Api;

/// <summary>
/// Organizasyon listeleme client. Sistem admin için dropdown kaynağı.
/// Full CRUD ileride (Organization UI faz'ında) eklenecek.
/// </summary>
public interface IOrganizationsApi
{
    /// <summary>
    /// <c>GET /api/organizations?page&amp;pageSize&amp;search&amp;includeInactive</c>
    /// Sistem admin tüm org'ları görür (RLS is_system_user bypass).
    /// </summary>
    Task<PagedResult<OrganizationListItemDto>> GetAllAsync(
        int page = 1,
        int pageSize = 100,
        string? search = null,
        bool includeInactive = false,
        CancellationToken ct = default);
}
