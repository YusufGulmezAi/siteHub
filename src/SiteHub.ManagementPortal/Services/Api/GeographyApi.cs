using System.Net.Http.Json;
using SiteHub.Contracts.Geography;

namespace SiteHub.ManagementPortal.Services.Api;

/// <summary>
/// <see cref="IGeographyApi"/>'nin HttpClient tabanlı implementasyonu.
///
/// <para>Typed client pattern (DI'da <c>AddHttpClient&lt;IGeographyApi, GeographyApi&gt;()</c>
/// ile kaydedilir). BaseAddress + CookieForwardingHandler otomatik gelir.</para>
/// </summary>
internal sealed class GeographyApi : IGeographyApi
{
    private readonly HttpClient _http;

    public GeographyApi(HttpClient http)
    {
        _http = http;
    }

    public async Task<IReadOnlyList<ProvinceDto>> GetProvincesAsync(CancellationToken ct = default)
    {
        var result = await _http.GetFromJsonAsync<List<ProvinceDto>>(
            "/api/geography/provinces", ct);
        return result ?? new List<ProvinceDto>();
    }

    public async Task<IReadOnlyList<DistrictDto>> GetDistrictsByProvinceAsync(
        Guid provinceId, CancellationToken ct = default)
    {
        var result = await _http.GetFromJsonAsync<List<DistrictDto>>(
            $"/api/geography/provinces/{provinceId}/districts", ct);
        return result ?? new List<DistrictDto>();
    }
}
