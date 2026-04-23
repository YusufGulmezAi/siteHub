using MediatR;
using Microsoft.EntityFrameworkCore;
using SiteHub.Application.Abstractions.Persistence;
using SiteHub.Contracts.Common;
using SiteHub.Contracts.Sites;
using SiteHub.Domain.Tenancy.Organizations;
using SiteHub.Domain.Text;

namespace SiteHub.Application.Features.Sites;

/// <summary>
/// Tüm Site'ları sayfalı + aranabilir getirir (flat endpoint).
///
/// <para>Flat REST: <c>GET /api/sites</c> — Organization filtre yok, RLS
/// otomatik filtreler:</para>
/// <list type="bullet">
///   <item>System Admin: tüm Organization'ların Site'ları</item>
///   <item>Organization user: sadece kendi Organization'ının Site'ları (RLS)</item>
/// </list>
///
/// <para><b>Arama:</b> Site.SearchText kolonunda LIKE (Site kendi alanları).
/// Organization adı ile arama dahil değil — o kolon görsel kolayık sağlar,
/// kullanıcı gözle süzer.</para>
///
/// <para><b>Opsiyonel <paramref name="OrganizationId"/>:</b> Verilmişse o org'a
/// filtrelenir. UI'da kullanılmaz (nested endpoint zaten var) ama backend'de hazır.</para>
///
/// <para><b>F.6 C.1 yeni.</b></para>
/// </summary>
public sealed record GetAllSitesQuery(
    int Page = 1,
    int PageSize = 20,
    string? SearchText = null,
    bool IncludeInactive = false,
    bool IncludeDeleted = false,
    Guid? OrganizationId = null)
    : IRequest<PagedResult<SiteListItemDto>>;

public sealed class GetAllSitesHandler
    : IRequestHandler<GetAllSitesQuery, PagedResult<SiteListItemDto>>
{
    private const int MaxPageSize = 100;

    private readonly ISiteHubDbContext _db;

    public GetAllSitesHandler(ISiteHubDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<SiteListItemDto>> Handle(
        GetAllSitesQuery q, CancellationToken ct)
    {
        var page = Math.Max(1, q.Page);
        var pageSize = Math.Clamp(q.PageSize, 1, MaxPageSize);

        var query = _db.Sites.AsNoTracking().AsQueryable();

        // Opsiyonel organization filter (UI kullanmaz, RLS zaten yapar — ama
        // System Admin için manuel filtre gerekebilir, hazır dursun).
        if (q.OrganizationId.HasValue)
        {
            var orgId = OrganizationId.FromGuid(q.OrganizationId.Value);
            query = query.Where(s => s.OrganizationId == orgId);
        }

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
            .Join(_db.Organizations.AsNoTracking(),
                  s => s.OrganizationId,
                  o => o.Id,
                  (s, o) => new { Site = s, OrgName = o.Name })
            .OrderByDescending(x => x.Site.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new SiteListItemDto(
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
                x.Site.CreatedAt))
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
