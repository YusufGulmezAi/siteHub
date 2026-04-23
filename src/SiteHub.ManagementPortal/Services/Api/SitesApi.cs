using System.Net;
using System.Net.Http.Json;
using SiteHub.Contracts.Common;
using SiteHub.Contracts.Sites;

namespace SiteHub.ManagementPortal.Services.Api;

internal sealed class SitesApi : ISitesApi
{
    private readonly HttpClient _http;

    public SitesApi(HttpClient http)
    {
        _http = http;
    }

    /// <summary>
    /// F.6 C.1: Flat endpoint — tüm Organization'ların Site'ları.
    /// OrganizationsApi.GetAllAsync ile birebir aynı pattern.
    /// </summary>
    public async Task<PagedResult<SiteListItemDto>> GetAllAsync(
        int page = 1,
        int pageSize = 20,
        string? search = null,
        bool includeInactive = false,
        Guid? organizationId = null,
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
        if (organizationId.HasValue)
            qs.Add($"organizationId={organizationId.Value}");

        var url = $"/api/sites?{string.Join("&", qs)}";

        var result = await _http.GetFromJsonAsync<PagedResult<SiteListItemDto>>(url, ct);
        return result ?? PagedResult<SiteListItemDto>.Empty(page, pageSize);
    }

    public async Task<PagedResult<SiteListItemDto>> GetByOrganizationAsync(
        Guid organizationId,
        int page = 1,
        int pageSize = 20,
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

        var url = $"/api/organizations/{organizationId}/sites?{string.Join("&", qs)}";

        var result = await _http.GetFromJsonAsync<PagedResult<SiteListItemDto>>(url, ct);
        return result ?? PagedResult<SiteListItemDto>.Empty(page, pageSize);
    }

    public async Task<SiteDetailDto?> GetByIdAsync(Guid siteId, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"/api/sites/{siteId}", ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SiteDetailDto>(cancellationToken: ct);
    }

    public async Task<CreateSiteResponse> CreateAsync(
        Guid organizationId, CreateSiteRequest request, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync(
            $"/api/organizations/{organizationId}/sites", request, ct);

        // API her iki durumda da JSON body dönüyor (201 veya 4xx) — parse et
        var result = await response.Content.ReadFromJsonAsync<CreateSiteResponse>(
            cancellationToken: ct);
        return result ?? new CreateSiteResponse(false, null, null, "UnexpectedEmptyResponse", "Sunucudan beklenen yanıt gelmedi.");
    }

    public async Task<SiteStatusResponse> UpdateAsync(
        Guid siteId, UpdateSiteRequest request, CancellationToken ct = default)
    {
        var response = await _http.PutAsJsonAsync($"/api/sites/{siteId}", request, ct);
        var result = await response.Content.ReadFromJsonAsync<SiteStatusResponse>(
            cancellationToken: ct);
        return result ?? new SiteStatusResponse(false, "UnexpectedEmptyResponse", "Sunucudan beklenen yanıt gelmedi.");
    }

    public async Task<SiteStatusResponse> ActivateAsync(Guid siteId, CancellationToken ct = default) =>
        await PostStatusAsync($"/api/sites/{siteId}/activate", null, ct);

    public async Task<SiteStatusResponse> DeactivateAsync(Guid siteId, CancellationToken ct = default) =>
        await PostStatusAsync($"/api/sites/{siteId}/deactivate", null, ct);

    public async Task<SiteStatusResponse> DeleteAsync(
        Guid siteId, DeleteSiteRequest request, CancellationToken ct = default)
    {
        // DELETE with body — HttpMethod.Delete + manual content
        using var req = new HttpRequestMessage(HttpMethod.Delete, $"/api/sites/{siteId}")
        {
            Content = JsonContent.Create(request)
        };
        var response = await _http.SendAsync(req, ct);
        var result = await response.Content.ReadFromJsonAsync<SiteStatusResponse>(
            cancellationToken: ct);
        return result ?? new SiteStatusResponse(false, "UnexpectedEmptyResponse", "Sunucudan beklenen yanıt gelmedi.");
    }

    private async Task<SiteStatusResponse> PostStatusAsync(
        string url, object? body, CancellationToken ct)
    {
        var response = body is null
            ? await _http.PostAsync(url, null, ct)
            : await _http.PostAsJsonAsync(url, body, ct);

        var result = await response.Content.ReadFromJsonAsync<SiteStatusResponse>(
            cancellationToken: ct);
        return result ?? new SiteStatusResponse(false, "UnexpectedEmptyResponse", "Sunucudan beklenen yanıt gelmedi.");
    }
}
