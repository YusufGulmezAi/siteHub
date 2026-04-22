namespace SiteHub.Application.Features.Sites;

/// <summary>
/// Liste ekranları için özet DTO.
/// </summary>
public sealed record SiteListItemDto(
    Guid Id,
    Guid OrganizationId,
    long Code,
    string Name,
    string? CommercialTitle,
    string Address,
    Guid ProvinceId,
    Guid? DistrictId,
    string? Iban,
    string? TaxId,
    bool IsActive,
    DateTimeOffset CreatedAt);

/// <summary>
/// Detay ekranı için tam DTO.
/// </summary>
public sealed record SiteDetailDto(
    Guid Id,
    Guid OrganizationId,
    long Code,
    string Name,
    string? CommercialTitle,
    string Address,
    Guid ProvinceId,
    Guid? DistrictId,
    string? Iban,
    string? TaxId,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    string? CreatedByName,
    string? UpdatedByName);
