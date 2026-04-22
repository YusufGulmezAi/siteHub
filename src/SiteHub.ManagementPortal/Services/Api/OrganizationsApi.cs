using System.Net;
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

    public async Task<OrganizationDetailDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"/api/organizations/{id}", ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<OrganizationDetailDto>(cancellationToken: ct);
    }

    public async Task<CreateOrganizationResponse> CreateAsync(
        CreateOrganizationRequest request, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("/api/organizations", request, ct);

        // API her iki durumda da (201 veya 4xx) JSON body döner — parse et
        var result = await response.Content.ReadFromJsonAsync<CreateOrganizationResponse>(
            cancellationToken: ct);
        return result ?? new CreateOrganizationResponse(
            false, null, null, "UnexpectedEmptyResponse",
            "Sunucudan beklenen yanıt gelmedi.");
    }

    public async Task<OrganizationStatusResponse> UpdateAsync(
        Guid id, UpdateOrganizationRequest request, CancellationToken ct = default)
    {
        var response = await _http.PutAsJsonAsync($"/api/organizations/{id}", request, ct);
        var result = await response.Content.ReadFromJsonAsync<OrganizationStatusResponse>(
            cancellationToken: ct);
        return result ?? new OrganizationStatusResponse(
            false, "UnexpectedEmptyResponse", "Sunucudan beklenen yanıt gelmedi.");
    }

    public async Task<OrganizationStatusResponse> ActivateAsync(
        Guid id, CancellationToken ct = default) =>
        await PostStatusAsync($"/api/organizations/{id}/activate", null, ct);

    public async Task<OrganizationStatusResponse> DeactivateAsync(
        Guid id, CancellationToken ct = default) =>
        await PostStatusAsync($"/api/organizations/{id}/deactivate", null, ct);

    public async Task<OrganizationStatusResponse> DeleteAsync(
        Guid id, DeleteOrganizationRequest request, CancellationToken ct = default)
    {
        // DELETE with body — HttpMethod.Delete + manual content
        using var req = new HttpRequestMessage(HttpMethod.Delete, $"/api/organizations/{id}")
        {
            Content = JsonContent.Create(request)
        };
        var response = await _http.SendAsync(req, ct);
        var result = await response.Content.ReadFromJsonAsync<OrganizationStatusResponse>(
            cancellationToken: ct);
        return result ?? new OrganizationStatusResponse(
            false, "UnexpectedEmptyResponse", "Sunucudan beklenen yanıt gelmedi.");
    }

    private async Task<OrganizationStatusResponse> PostStatusAsync(
        string url, object? body, CancellationToken ct)
    {
        var response = body is null
            ? await _http.PostAsync(url, null, ct)
            : await _http.PostAsJsonAsync(url, body, ct);

        var result = await response.Content.ReadFromJsonAsync<OrganizationStatusResponse>(
            cancellationToken: ct);
        return result ?? new OrganizationStatusResponse(
            false, "UnexpectedEmptyResponse", "Sunucudan beklenen yanıt gelmedi.");
    }
}
