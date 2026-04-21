using MediatR;
using Microsoft.EntityFrameworkCore;
using SiteHub.Application.Abstractions.Persistence;
using SiteHub.Domain.Tenancy.Organizations;

namespace SiteHub.Application.Features.Organizations;

public sealed record GetOrganizationByIdQuery(Guid OrganizationId)
    : IRequest<OrganizationDetailDto?>;

public sealed class GetOrganizationByIdHandler
    : IRequestHandler<GetOrganizationByIdQuery, OrganizationDetailDto?>
{
    private readonly ISiteHubDbContext _db;

    public GetOrganizationByIdHandler(ISiteHubDbContext db)
    {
        _db = db;
    }

    public async Task<OrganizationDetailDto?> Handle(
        GetOrganizationByIdQuery q, CancellationToken ct)
    {
        var orgId = OrganizationId.FromGuid(q.OrganizationId);

        return await _db.Organizations
            .AsNoTracking()
            .Where(o => o.Id == orgId && o.DeletedAt == null)
            .Select(o => new OrganizationDetailDto(
                o.Id.Value,
                o.Code,
                o.Name,
                o.CommercialTitle,
                o.TaxId.Value,
                o.Address,
                o.Phone,
                o.Email,
                o.IsActive,
                o.CreatedAt,
                o.UpdatedAt,
                o.CreatedByName,
                o.UpdatedByName))
            .FirstOrDefaultAsync(ct);
    }
}
