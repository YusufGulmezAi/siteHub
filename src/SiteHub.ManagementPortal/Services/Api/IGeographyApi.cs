using SiteHub.Contracts.Geography;

namespace SiteHub.ManagementPortal.Services.Api;

/// <summary>
/// Coğrafya (İl/İlçe) referans veri erişimi. UI form dropdown'ları için.
/// </summary>
public interface IGeographyApi
{
    Task<IReadOnlyList<ProvinceDto>> GetProvincesAsync(CancellationToken ct = default);

    Task<IReadOnlyList<DistrictDto>> GetDistrictsByProvinceAsync(
        Guid provinceId, CancellationToken ct = default);
}
