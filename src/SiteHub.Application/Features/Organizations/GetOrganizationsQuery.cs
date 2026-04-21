using MediatR;
using Microsoft.EntityFrameworkCore;
using SiteHub.Application.Abstractions.Persistence;
using SiteHub.Domain.Text;

namespace SiteHub.Application.Features.Organizations;

/// <summary>
/// Organizasyon listesini sayfalı + aranabilir getirir.
///
/// <para><b>Arama:</b> <see cref="SearchText"/> doluysa SearchText kolonunda ILIKE yapar.
/// Code + Name + CommercialTitle + TaxId + Phone + Email birleşik alanı.</para>
///
/// <para><b>Durum filtresi:</b></para>
/// <list type="bullet">
///   <item><see cref="IncludeInactive"/>=false (default): sadece aktif firmalar</item>
///   <item><see cref="IncludeInactive"/>=true: pasifleri de getir</item>
///   <item><see cref="IncludeDeleted"/>=true: silinmişleri de getir (admin için)</item>
/// </list>
/// </summary>
public sealed record GetOrganizationsQuery(
    int Page = 1,
    int PageSize = 20,
    string? SearchText = null,
    bool IncludeInactive = false,
    bool IncludeDeleted = false)
    : IRequest<PagedResult<OrganizationListItemDto>>;

public sealed class GetOrganizationsHandler
    : IRequestHandler<GetOrganizationsQuery, PagedResult<OrganizationListItemDto>>
{
    // Güvenlik: page size'ı üstten sınırla (DoS/performans)
    private const int MaxPageSize = 100;

    private readonly ISiteHubDbContext _db;

    public GetOrganizationsHandler(ISiteHubDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<OrganizationListItemDto>> Handle(
        GetOrganizationsQuery q, CancellationToken ct)
    {
        var page = Math.Max(1, q.Page);
        var pageSize = Math.Clamp(q.PageSize, 1, MaxPageSize);

        var query = _db.Organizations.AsNoTracking().AsQueryable();

        if (!q.IncludeDeleted)
            query = query.Where(o => o.DeletedAt == null);

        if (!q.IncludeInactive)
            query = query.Where(o => o.IsActive);

        if (!string.IsNullOrWhiteSpace(q.SearchText))
        {
            // Türkçe normalize (lowercase + aksan kaldır), sonra LIKE.
            // SearchText kolonu zaten lowercase + TurkishCsAs deterministic collation
            // ile tutulduğu için Like yeterli (ILIKE'a gerek yok).
            var normalized = TurkishNormalizer.Normalize(q.SearchText.Trim());
            var pattern = $"%{normalized}%";
            query = query.Where(o => EF.Functions.Like(o.SearchText, pattern));
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new OrganizationListItemDto(
                o.Id.Value,
                o.Code,
                o.Name,
                o.CommercialTitle,
                o.TaxId.Value,
                o.Phone,
                o.Email,
                o.IsActive,
                o.CreatedAt))
            .ToListAsync(ct);

        return new PagedResult<OrganizationListItemDto>(items, totalCount, page, pageSize);
    }
}
