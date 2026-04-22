using MediatR;
using Microsoft.EntityFrameworkCore;
using SiteHub.Application.Abstractions.Persistence;
using SiteHub.Domain.Geography;

namespace SiteHub.Application.Features.Geography;

/// <summary>
/// Bir ilin tüm ilçelerini alfabetik sırayla döner.
///
/// <para><b>Kullanım:</b> IL seçildikten sonra cascading İlçe dropdown'u doldurur.</para>
///
/// <para>İlgili il yoksa boş liste döner (frontend dropdown'u devre dışı kalır).</para>
/// </summary>
public sealed record GetDistrictsByProvinceQuery(Guid ProvinceId)
    : IRequest<IReadOnlyList<DistrictDto>>;

public sealed record DistrictDto(
    Guid Id,
    Guid ProvinceId,
    string Name,
    int ExternalId);

public sealed class GetDistrictsByProvinceHandler
    : IRequestHandler<GetDistrictsByProvinceQuery, IReadOnlyList<DistrictDto>>
{
    private readonly ISiteHubDbContext _db;

    public GetDistrictsByProvinceHandler(ISiteHubDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<DistrictDto>> Handle(
        GetDistrictsByProvinceQuery q, CancellationToken ct)
    {
        var pid = ProvinceId.FromGuid(q.ProvinceId);

        return await _db.Districts
            .AsNoTracking()
            .Where(d => d.ProvinceId == pid)
            .OrderBy(d => d.Name)
            .Select(d => new DistrictDto(
                d.Id.Value,
                d.ProvinceId.Value,
                d.Name,
                d.ExternalId))
            .ToListAsync(ct);
    }
}
