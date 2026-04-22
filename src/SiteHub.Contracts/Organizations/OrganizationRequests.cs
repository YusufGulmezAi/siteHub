namespace SiteHub.Contracts.Organizations;

/// <summary>
/// Yeni Organizasyon oluşturma request'i. <c>POST /api/organizations</c>.
/// Code backend'de generate edilir (100001-999999 arasında), body'de yoktur.
/// </summary>
public sealed record CreateOrganizationRequest(
    string Name,
    string CommercialTitle,
    string TaxId,
    string? Address = null,
    string? Phone = null,
    string? Email = null);

/// <summary>
/// Organizasyon güncelleme request'i. <c>PUT /api/organizations/{id}</c>.
/// Code, IsActive değişmez — sadece aşağıdaki alanlar.
/// </summary>
public sealed record UpdateOrganizationRequest(
    string Name,
    string CommercialTitle,
    string TaxId,
    string? Address = null,
    string? Phone = null,
    string? Email = null);

/// <summary>
/// Soft delete request. <c>DELETE /api/organizations/{id}</c> body'si.
/// Sebep zorunlu (audit trail).
/// </summary>
public sealed record DeleteOrganizationRequest(string Reason);

/// <summary>
/// <c>POST /api/organizations</c> response shape'i.
/// Başarıda <c>Success=true, OrganizationId+Code dolu</c>.
/// Hatada <c>Success=false, FailureCode+Message dolu</c>.
/// </summary>
public sealed record CreateOrganizationResponse(
    bool Success,
    Guid? OrganizationId,
    long? Code,
    string? FailureCode,
    string? Message);

/// <summary>
/// Update/Activate/Deactivate/Delete response shape'i.
/// </summary>
public sealed record OrganizationStatusResponse(
    bool Success,
    string? Code,
    string? Message);
