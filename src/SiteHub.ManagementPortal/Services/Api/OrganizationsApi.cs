using System.Net.Http.Json;
using SiteHub.Contracts.Common;
using SiteHub.Contracts.Organizations;

namespace SiteHub.ManagementPortal.Services.Api;

internal sealed class OrganizationsApi : IOrganizationsApi
{
    private readonly HttpClient _http;

    public OrganizationsApi(HttpClient http)
    {
        _http = http;
    }

    public async Task<PagedResult<OrganizationListItemDto>> GetAllAsync(
        int page = 1,
        int pageSize = 100,
        string? search = null,
        bool includeInactive = false,
        CancellationToken ct = default)
    {
        var qs = new List<string>
        {
            $"page={page}",
            $"pageSize={pageSize}",
            $"includeInactive={includeInactive.ToString().ToLowerInvariant()}"
        };
        if (!string.IsNullOrWhiteSpace(search))
            qs.Add($"search={Uri.EscapeDataString(search)}");

        var url = $"/api/organizations?{string.Join("&", qs)}";

        var result = await _http.GetFromJsonAsync<PagedResult<OrganizationListItemDto>>(url, ct);
        return result ?? PagedResult<OrganizationListItemDto>.Empty(page, pageSize);
    }
}
