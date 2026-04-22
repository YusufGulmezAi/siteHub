using SiteHub.Contracts.Common;
using SiteHub.Contracts.Sites;

namespace SiteHub.ManagementPortal.Services.Api;

/// <summary>
/// Site CRUD endpoint'leri için tip-güvenli client (nested REST).
/// </summary>
public interface ISitesApi
{
    /// <summary>
    /// <c>GET /api/organizations/{orgId}/sites?page&amp;pageSize&amp;search&amp;includeInactive</c>
    /// </summary>
    Task<PagedResult<SiteListItemDto>> GetByOrganizationAsync(
        Guid organizationId,
        int page = 1,
        int pageSize = 20,
        string? search = null,
        bool includeInactive = false,
        CancellationToken ct = default);

    /// <summary>
    /// <c>GET /api/sites/{id}</c>
    /// </summary>
    Task<SiteDetailDto?> GetByIdAsync(Guid siteId, CancellationToken ct = default);

    /// <summary>
    /// <c>POST /api/organizations/{orgId}/sites</c>
    /// </summary>
    Task<CreateSiteResponse> CreateAsync(
        Guid organizationId, CreateSiteRequest request, CancellationToken ct = default);

    /// <summary>
    /// <c>PUT /api/sites/{id}</c>
    /// </summary>
    Task<SiteStatusResponse> UpdateAsync(
        Guid siteId, UpdateSiteRequest request, CancellationToken ct = default);

    /// <summary>
    /// <c>POST /api/sites/{id}/activate</c>
    /// </summary>
    Task<SiteStatusResponse> ActivateAsync(Guid siteId, CancellationToken ct = default);

    /// <summary>
    /// <c>POST /api/sites/{id}/deactivate</c>
    /// </summary>
    Task<SiteStatusResponse> DeactivateAsync(Guid siteId, CancellationToken ct = default);

    /// <summary>
    /// <c>DELETE /api/sites/{id}</c> (soft delete, reason zorunlu)
    /// </summary>
    Task<SiteStatusResponse> DeleteAsync(
        Guid siteId, DeleteSiteRequest request, CancellationToken ct = default);
}
