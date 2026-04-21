namespace SiteHub.Application.Features.Organizations;

/// <summary>
/// Liste ekranları için özet DTO — tabloda göstermek için gerekli minimum alanlar.
/// </summary>
public sealed record OrganizationListItemDto(
    Guid Id,
    long Code,
    string Name,
    string CommercialTitle,
    string TaxId,
    string? Phone,
    string? Email,
    bool IsActive,
    DateTimeOffset CreatedAt);

/// <summary>
/// Detay ekranı için tam DTO — düzenleme formuna doldurmak için.
/// </summary>
public sealed record OrganizationDetailDto(
    Guid Id,
    long Code,
    string Name,
    string CommercialTitle,
    string TaxId,
    string? Address,
    string? Phone,
    string? Email,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    string? CreatedByName,
    string? UpdatedByName);

/// <summary>
/// Sayfa sonucu — toplam kayıt ve sayfa bilgisiyle.
/// </summary>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize)
{
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;
}
