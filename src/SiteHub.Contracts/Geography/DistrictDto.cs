namespace SiteHub.Contracts.Geography;

/// <summary>
/// İlçe bilgisi. UI cascading dropdown için (IL seçimi sonrası).
/// </summary>
public sealed record DistrictDto(
    Guid Id,
    Guid ProvinceId,
    string Name,
    int ExternalId);
