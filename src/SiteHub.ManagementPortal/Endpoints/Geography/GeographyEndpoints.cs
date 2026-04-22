using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using SiteHub.Application.Features.Geography;

namespace SiteHub.ManagementPortal.Endpoints.Geography;

/// <summary>
/// Coğrafya (İl, İlçe) read-only endpoint'leri.
///
/// <para><b>URL'ler:</b></para>
/// <list type="bullet">
///   <item><c>GET /api/geography/provinces</c> — Tüm iller (81 kayıt)</item>
///   <item><c>GET /api/geography/provinces/{provinceId}/districts</c> — İle göre ilçeler</item>
/// </list>
///
/// <para><b>Yetki:</b> Authenticated yeterli — coğrafya referans veri, her kullanıcı okuyabilir.
/// İleride anonymous'a da açılabilir (login öncesi self-service adres formu için).</para>
/// </summary>
public sealed class GeographyEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/geography")
            .WithTags("Geography")
            .RequireAuthorization()
            .DisableAntiforgery();

        group.MapGet("/provinces", GetProvincesAsync).WithName("GetProvinces");
        group.MapGet("/provinces/{provinceId:guid}/districts", GetDistrictsAsync)
            .WithName("GetDistrictsByProvince");
    }

    private static async Task<Ok<IReadOnlyList<ProvinceDto>>> GetProvincesAsync(
        IMediator mediator, CancellationToken ct)
    {
        var result = await mediator.Send(new GetProvincesQuery(), ct);
        return TypedResults.Ok(result);
    }

    private static async Task<Ok<IReadOnlyList<DistrictDto>>> GetDistrictsAsync(
        Guid provinceId, IMediator mediator, CancellationToken ct)
    {
        var result = await mediator.Send(new GetDistrictsByProvinceQuery(provinceId), ct);
        return TypedResults.Ok(result);
    }
}
