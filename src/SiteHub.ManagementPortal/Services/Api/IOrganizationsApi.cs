using SiteHub.Contracts.Common;
using SiteHub.Contracts.Organizations;

namespace SiteHub.ManagementPortal.Services.Api;

/// <summary>
/// Organizasyon CRUD client. Sistem admin tüm operasyonları yapar;
/// organization-scoped kullanıcı sadece kendi org'unu düzenler (RLS enforce eder).
/// </summary>
public interface IOrganizationsApi
{
    /// <summary>
    /// <c>GET /api/organizations?page&amp;pageSize&amp;search&amp;includeInactive</c>
    /// </summary>
    Task<PagedResult<OrganizationListItemDto>> GetAllAsync(
        int page = 1,
        int pageSize = 100,
        string? search = null,
        bool includeInactive = false,
        CancellationToken ct = default);

    /// <summary>
    /// <c>GET /api/organizations/{id}</c> — Tek organization detayı (Form ve Detail sayfaları).
    /// Bulunamazsa <c>null</c> döner.
    /// </summary>
    Task<OrganizationDetailDto?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// <c>POST /api/organizations</c>
    /// </summary>
    Task<CreateOrganizationResponse> CreateAsync(
        CreateOrganizationRequest request, CancellationToken ct = default);

    /// <summary>
    /// <c>PUT /api/organizations/{id}</c>
    /// </summary>
    Task<OrganizationStatusResponse> UpdateAsync(
        Guid id, UpdateOrganizationRequest request, CancellationToken ct = default);

    /// <summary>
    /// <c>POST /api/organizations/{id}/activate</c>
    /// </summary>
    Task<OrganizationStatusResponse> ActivateAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// <c>POST /api/organizations/{id}/deactivate</c>
    /// </summary>
    Task<OrganizationStatusResponse> DeactivateAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// <c>DELETE /api/organizations/{id}</c> (soft delete, reason zorunlu)
    /// </summary>
    Task<OrganizationStatusResponse> DeleteAsync(
        Guid id, DeleteOrganizationRequest request, CancellationToken ct = default);
}
