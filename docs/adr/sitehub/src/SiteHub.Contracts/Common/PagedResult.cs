namespace SiteHub.Contracts.Common;

/// <summary>
/// Sayfalı liste sonucu için standart yapı.
/// Endpoint: GET /api/sites?page=2&pageSize=50&sort=name
/// Response: ApiResponse&lt;PagedResult&lt;SiteDto&gt;&gt;
/// </summary>
public sealed class PagedResult<T>
{
    public required IReadOnlyList<T> Items { get; init; }
    public required int Page { get; init; }
    public required int PageSize { get; init; }
    public required int TotalCount { get; init; }

    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasNext => Page < TotalPages;
    public bool HasPrevious => Page > 1;

    public static PagedResult<T> Empty(int page = 1, int pageSize = 50) => new()
    {
        Items = Array.Empty<T>(),
        Page = page,
        PageSize = pageSize,
        TotalCount = 0
    };
}

/// <summary>
/// Sayfalama parametreleri için standart query object.
/// UI'dan controller'a geçer.
/// </summary>
public sealed class PagedRequest
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
    public string? SortBy { get; init; }
    public SortDirection SortDirection { get; init; } = SortDirection.Ascending;
    public string? SearchTerm { get; init; }

    public int Skip => Math.Max(0, (Page - 1) * PageSize);
    public int Take => Math.Clamp(PageSize, 1, 500);
}

public enum SortDirection
{
    Ascending,
    Descending
}
