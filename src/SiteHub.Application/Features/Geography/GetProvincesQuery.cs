using MediatR;
using Microsoft.EntityFrameworkCore;
using SiteHub.Application.Abstractions.Persistence;

namespace SiteHub.Application.Features.Geography;

/// <summary>
/// Tüm illeri (Türkiye 81 il) alfabetik sırayla döner.
///
/// <para><b>Neden paging yok?</b> Sabit 81 kayıt. Cache edilebilir (ileride IMemoryCache).</para>
///
/// <para><b>Kullanım:</b> Form dropdown'ları (Site, Organization adres seçimi).</para>
/// </summary>
public sealed record GetProvincesQuery : IRequest<IReadOnlyList<ProvinceDto>>;

public sealed record ProvinceDto(
    Guid Id,
    string Name,
    string PlateCode,
    int ExternalId);

public sealed class GetProvincesHandler
    : IRequestHandler<GetProvincesQuery, IReadOnlyList<ProvinceDto>>
{
    private readonly ISiteHubDbContext _db;

    public GetProvincesHandler(ISiteHubDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ProvinceDto>> Handle(
        GetProvincesQuery q, CancellationToken ct)
    {
        return await _db.Provinces
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .Select(p => new ProvinceDto(
                p.Id.Value,
                p.Name,
                p.PlateCode,
                p.ExternalId))
            .ToListAsync(ct);
    }
}
