using MediatR;
using Microsoft.EntityFrameworkCore;
using SiteHub.Application.Abstractions.Persistence;
using SiteHub.Contracts.Sites;
using SiteHub.Domain.Tenancy.Sites;

namespace SiteHub.Application.Features.Sites;

/// <summary>
/// Tek bir Site'ın tam detayı. <c>GET /api/sites/{id}</c>.
/// Bulunamazsa <c>null</c> döner.
///
/// <para><b>F.6 C.1:</b> Response'a <c>OrganizationName</c> alanı eklendi.
/// Explicit Join ile Organization tablosundan çekilir.</para>
/// </summary>
public sealed record GetSiteByIdQuery(Guid SiteId)
    : IRequest<SiteDetailDto?>;

public sealed class GetSiteByIdHandler
    : IRequestHandler<GetSiteByIdQuery, SiteDetailDto?>
{
    private readonly ISiteHubDbContext _db;

    public GetSiteByIdHandler(ISiteHubDbContext db)
    {
        _db = db;
    }

    public async Task<SiteDetailDto?> Handle(
        GetSiteByIdQuery q, CancellationToken ct)
    {
        var siteId = SiteId.FromGuid(q.SiteId);

        return await _db.Sites
            .AsNoTracking()
            .Where(s => s.Id == siteId && s.DeletedAt == null)
            .Join(_db.Organizations.AsNoTracking(),
                  s => s.OrganizationId,
                  o => o.Id,
                  (s, o) => new { Site = s, OrgName = o.Name })
            .Select(x => new SiteDetailDto(
                x.Site.Id.Value,
                x.Site.OrganizationId.Value,
                x.OrgName,
                x.Site.Code,
                x.Site.Name,
                x.Site.CommercialTitle,
                x.Site.Address,
                x.Site.ProvinceId.Value,
                x.Site.DistrictId == null ? (Guid?)null : x.Site.DistrictId.Value.Value,
                x.Site.Iban,
                x.Site.TaxId == null ? null : x.Site.TaxId.Value,
                x.Site.IsActive,
                x.Site.CreatedAt,
                x.Site.UpdatedAt,
                x.Site.CreatedByName,
                x.Site.UpdatedByName))
            .FirstOrDefaultAsync(ct);
    }
}
