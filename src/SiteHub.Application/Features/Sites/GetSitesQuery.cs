using MediatR;
using Microsoft.EntityFrameworkCore;
using SiteHub.Application.Abstractions.Persistence;
using SiteHub.Contracts.Common;
using SiteHub.Contracts.Sites;
using SiteHub.Domain.Tenancy.Organizations;
using SiteHub.Domain.Text;

namespace SiteHub.Application.Features.Sites;

/// <summary>
/// Bir Organization'a ait Site listesi — sayfalı + aranabilir.
///
/// <para>Nested REST: <c>GET /api/organizations/{orgId}/sites</c>
/// URL'den gelen <paramref name="OrganizationId"/> zorunlu.</para>
///
/// <para><b>Arama:</b> SearchText doluysa Site.SearchText kolonunda LIKE (Code + Name +
/// CommercialTitle + Address + TaxId + Iban birleşik aranır).</para>
///
/// <para><b>F.6 Cleanup:</b> Response DTO'ları Contracts.Sites'a konsolide edildi.
/// PagedResult&lt;T&gt; artık Contracts.Common'dan geliyor.</para>
/// </summary>
public sealed record GetSitesQuery(
    Guid OrganizationId,
    int Page = 1,
    int PageSize = 20,
    string? SearchText = null,
    bool IncludeInactive = false,
    bool IncludeDeleted = false)
    : IRequest<PagedResult<SiteListItemDto>>;

public sealed class GetSitesHandler
    : IRequestHandler<GetSitesQuery, PagedResult<SiteListItemDto>>
{
    private const int MaxPageSize = 100;

    private readonly ISiteHubDbContext _db;

    public GetSitesHandler(ISiteHubDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<SiteListItemDto>> Handle(
        GetSitesQuery q, CancellationToken ct)
    {
        var page = Math.Max(1, q.Page);
        var pageSize = Math.Clamp(q.PageSize, 1, MaxPageSize);
        var orgId = OrganizationId.FromGuid(q.OrganizationId);

        var query = _db.Sites
            .AsNoTracking()
            .Where(s => s.OrganizationId == orgId);

        if (!q.IncludeDeleted)
            query = query.Where(s => s.DeletedAt == null);

        if (!q.IncludeInactive)
            query = query.Where(s => s.IsActive);

        if (!string.IsNullOrWhiteSpace(q.SearchText))
        {
            var normalized = TurkishNormalizer.Normalize(q.SearchText.Trim());
            var pattern = $"%{normalized}%";
            query = query.Where(s => EF.Functions.Like(s.SearchText, pattern));
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(s => s.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new SiteListItemDto(
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
                s.CreatedAt))
            .ToListAsync(ct);

        return new PagedResult<SiteListItemDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }
}
