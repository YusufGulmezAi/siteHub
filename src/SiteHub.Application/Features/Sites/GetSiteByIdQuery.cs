using MediatR;
using Microsoft.EntityFrameworkCore;
using SiteHub.Application.Abstractions.Persistence;
using SiteHub.Contracts.Sites;
using SiteHub.Domain.Tenancy.Sites;

namespace SiteHub.Application.Features.Sites;

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
            .Select(s => new SiteDetailDto(
                s.Id.Value,
                s.OrganizationId.Value,
                s.Code,
                s.Name,
                s.CommercialTitle,
                s.Address,
                s.ProvinceId.Value,
                s.DistrictId == null ? (Guid?)null : s.DistrictId.Value.Value,
                s.Iban,
                s.TaxId == null ? null : s.TaxId.Value,
                s.IsActive,
                s.CreatedAt,
                s.UpdatedAt,
                s.CreatedByName,
                s.UpdatedByName))
            .FirstOrDefaultAsync(ct);
    }
}
