namespace SiteHub.Contracts.Sites;

/// <summary>
/// Site listesi satır DTO'su. Tablo/MudDataGrid'de gösterilen alanlar.
///
/// <para><b>F.6 C.1:</b> <c>OrganizationName</c> alanı eklendi — flat <c>/api/sites</c>
/// endpoint'inde her satır Organization adını gösterir. Nested endpoint'te de
/// tutarlılık için doldurulur (UI o kolonu gizler).</para>
/// </summary>
public sealed record SiteListItemDto(
    Guid Id,
    Guid OrganizationId,
    string OrganizationName,
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
///
/// <para><b>F.6 C.1:</b> <c>OrganizationName</c> alanı eklendi — Detail sayfasında
/// breadcrumb'da "Firmalar › [Org Adı] › Siteler › [Site Adı]" için kullanılır.</para>
/// </summary>
public sealed record SiteDetailDto(
    Guid Id,
    Guid OrganizationId,
    string OrganizationName,
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
