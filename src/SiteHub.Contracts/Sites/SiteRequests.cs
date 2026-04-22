namespace SiteHub.Contracts.Sites;

/// <summary>
/// Yeni Site oluşturma request'i. <c>POST /api/organizations/{orgId}/sites</c>.
/// <c>organizationId</c> URL'den gelir, body'de bulunmaz.
/// </summary>
public sealed record CreateSiteRequest(
    string Name,
    Guid ProvinceId,
    string Address,
    string? CommercialTitle = null,
    Guid? DistrictId = null,
    string? Iban = null,
    string? TaxId = null);

/// <summary>
/// Site güncelleme request'i. <c>PUT /api/sites/{id}</c>.
/// Code, OrganizationId değişmez — sadece aşağıdaki alanlar.
/// </summary>
public sealed record UpdateSiteRequest(
    string Name,
    string Address,
    Guid ProvinceId,
    string? CommercialTitle = null,
    Guid? DistrictId = null,
    string? Iban = null,
    string? TaxId = null);

/// <summary>
/// Soft delete request. <c>DELETE /api/sites/{id}</c> body'si.
/// ADR-0006 gereği sebep zorunlu (audit trail).
/// </summary>
public sealed record DeleteSiteRequest(string Reason);

/// <summary>
/// <c>POST /api/organizations/{orgId}/sites</c> response shape'i.
/// Başarıda <c>Success=true, SiteId+Code dolu</c>. Hatada <c>FailureCode+Message dolu</c>.
/// </summary>
public sealed record CreateSiteResponse(
    bool Success,
    Guid? SiteId,
    long? Code,
    string? FailureCode,
    string? Message);

/// <summary>
/// Update/Activate/Deactivate/Delete response shape'i.
/// </summary>
public sealed record SiteStatusResponse(
    bool Success,
    string? Code,
    string? Message);
