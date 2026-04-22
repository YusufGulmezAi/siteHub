namespace SiteHub.Contracts.Sites;

/// <summary>
/// Site listesi satır DTO'su. Tablo/MudDataGrid'de gösterilen alanlar.
///
/// <para><b>Not:</b> <c>Application.Features.Sites.SiteListItemDto</c>'nun birebir kopyası
/// (Faz F.6 Seçenek B — duplicate kabul, cleanup sonra).</para>
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
/// Site detay DTO'su. Tek Site görüntüleme/düzenleme için tam alan seti + audit.
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
