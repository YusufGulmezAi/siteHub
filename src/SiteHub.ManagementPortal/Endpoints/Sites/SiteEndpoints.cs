using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using SiteHub.Application.Features.Sites;
using SiteHub.Contracts.Common;
using SiteHub.Contracts.Sites;

namespace SiteHub.ManagementPortal.Endpoints.Sites;

/// <summary>
/// Site (apartman/kompleks) CRUD endpoint'leri.
///
/// <para><b>URL pattern — Nested REST:</b></para>
/// <list type="bullet">
///   <item><c>GET /api/organizations/{orgId}/sites</c> — Organization'ın Site listesi</item>
///   <item><c>POST /api/organizations/{orgId}/sites</c> — Yeni Site</item>
///   <item><c>GET /api/sites/{id}</c> — Site detay (direct ID lookup)</item>
///   <item><c>PUT /api/sites/{id}</c> — Güncelle</item>
///   <item><c>POST /api/sites/{id}/activate|deactivate</c> — Durum</item>
///   <item><c>DELETE /api/sites/{id}</c> — Soft delete</item>
/// </list>
///
/// <para><b>Yetki (Faz F MVP):</b> authenticated yeterli. Gerçek permission check
/// (<c>site.create</c>, <c>site.edit</c>) ileride eklenir.</para>
///
/// <para><b>F.6 Cleanup:</b> Response DTO'ları (SiteListItemDto / SiteDetailDto / PagedResult)
/// artık Contracts'tan geliyor. Organizations hack-import'u kaldırıldı.</para>
/// </summary>
public sealed class SiteEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        // Org-scoped group (listeleme + oluşturma)
        var orgScoped = app.MapGroup("/api/organizations/{orgId:guid}/sites")
            .WithTags("Sites")
            .RequireAuthorization()
            .DisableAntiforgery();

        orgScoped.MapGet("/", GetListAsync).WithName("GetSitesByOrganization");
        orgScoped.MapPost("/", CreateAsync).WithName("CreateSite");

        // Direct-ID group (tek Site üzerinde işlemler — URL daha kısa)
        var byId = app.MapGroup("/api/sites")
            .WithTags("Sites")
            .RequireAuthorization()
            .DisableAntiforgery();

        byId.MapGet("/{id:guid}", GetByIdAsync).WithName("GetSiteById");
        byId.MapPut("/{id:guid}", UpdateAsync).WithName("UpdateSite");
        byId.MapPost("/{id:guid}/activate", ActivateAsync).WithName("ActivateSite");
        byId.MapPost("/{id:guid}/deactivate", DeactivateAsync).WithName("DeactivateSite");
        byId.MapDelete("/{id:guid}", DeleteAsync).WithName("DeleteSite");
    }

    // ─── GET /api/organizations/{orgId}/sites ───────────────────────────────

    public sealed record ListQueryParams(
        int Page = 1,
        int PageSize = 20,
        string? Search = null,
        bool IncludeInactive = false);

    private static async Task<Ok<PagedResult<SiteListItemDto>>> GetListAsync(
        Guid orgId,
        [AsParameters] ListQueryParams p,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.Send(
            new GetSitesQuery(orgId, p.Page, p.PageSize, p.Search, p.IncludeInactive), ct);
        return TypedResults.Ok(result);
    }

    // ─── GET /api/sites/{id} ────────────────────────────────────────────────

    private static async Task<IResult> GetByIdAsync(
        Guid id, IMediator mediator, CancellationToken ct)
    {
        var dto = await mediator.Send(new GetSiteByIdQuery(id), ct);
        return dto is null
            ? TypedResults.NotFound(new { message = "Site bulunamadı." })
            : TypedResults.Ok(dto);
    }

    // ─── POST /api/organizations/{orgId}/sites ──────────────────────────────

    public sealed record CreateRequestBody(
        string Name,
        Guid ProvinceId,
        string Address,
        string? CommercialTitle,
        Guid? DistrictId,
        string? Iban,
        string? TaxId);

    public sealed record CreateResponse(
        bool Success,
        Guid? SiteId,
        long? Code,
        string? FailureCode,
        string? Message);

    private static async Task<IResult> CreateAsync(
        Guid orgId,
        [FromBody] CreateRequestBody body,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.Send(new CreateSiteCommand(
            OrganizationId: orgId,
            Name: body.Name,
            ProvinceId: body.ProvinceId,
            Address: body.Address,
            CommercialTitle: body.CommercialTitle,
            DistrictId: body.DistrictId,
            Iban: body.Iban,
            TaxId: body.TaxId), ct);

        if (result.IsSuccess)
        {
            return TypedResults.Created(
                $"/api/sites/{result.SiteId}",
                new CreateResponse(
                    Success: true,
                    SiteId: result.SiteId,
                    Code: result.Code,
                    FailureCode: null,
                    Message: null));
        }

        var message = result.ErrorMessage ?? result.FailureCode switch
        {
            CreateSiteFailureCode.OrganizationNotFound => "Parent organizasyon bulunamadı.",
            CreateSiteFailureCode.NameAlreadyExists => "Bu ad zaten kullanılıyor.",
            CreateSiteFailureCode.ProvinceNotFound => "İl bulunamadı.",
            CreateSiteFailureCode.DistrictNotFound => "İlçe bulunamadı veya il ile uyuşmuyor.",
            CreateSiteFailureCode.InvalidTaxId => "VKN geçersiz.",
            CreateSiteFailureCode.InvalidIban => "IBAN geçersiz.",
            CreateSiteFailureCode.ValidationError => "Giriş doğrulama hatası.",
            _ => "Oluşturulamadı."
        };

        var status = result.FailureCode == CreateSiteFailureCode.OrganizationNotFound
            ? StatusCodes.Status404NotFound
            : StatusCodes.Status400BadRequest;

        return Results.Json(
            new CreateResponse(
                Success: false,
                SiteId: null,
                Code: null,
                FailureCode: result.FailureCode.ToString(),
                Message: message),
            statusCode: status);
    }

    // ─── PUT /api/sites/{id} ────────────────────────────────────────────────

    public sealed record UpdateRequestBody(
        string Name,
        string Address,
        Guid ProvinceId,
        string? CommercialTitle,
        Guid? DistrictId,
        string? Iban,
        string? TaxId);

    public sealed record StatusResponse(bool Success, string? Code, string? Message);

    private static async Task<IResult> UpdateAsync(
        Guid id,
        [FromBody] UpdateRequestBody body,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.Send(new UpdateSiteCommand(
            SiteId: id,
            Name: body.Name,
            Address: body.Address,
            ProvinceId: body.ProvinceId,
            CommercialTitle: body.CommercialTitle,
            DistrictId: body.DistrictId,
            Iban: body.Iban,
            TaxId: body.TaxId), ct);

        if (result.IsSuccess)
            return TypedResults.Ok(new StatusResponse(true, null, "Güncellendi."));

        var message = result.ErrorMessage ?? result.FailureCode switch
        {
            UpdateSiteFailureCode.NotFound => "Site bulunamadı.",
            UpdateSiteFailureCode.NameAlreadyExists => "Bu ad zaten kullanılıyor.",
            UpdateSiteFailureCode.ProvinceNotFound => "İl bulunamadı.",
            UpdateSiteFailureCode.DistrictNotFound => "İlçe bulunamadı veya il ile uyuşmuyor.",
            UpdateSiteFailureCode.InvalidTaxId => "VKN geçersiz.",
            UpdateSiteFailureCode.InvalidIban => "IBAN geçersiz.",
            UpdateSiteFailureCode.ValidationError => "Giriş doğrulama hatası.",
            _ => "Güncellenemedi."
        };

        var status = result.FailureCode == UpdateSiteFailureCode.NotFound
            ? StatusCodes.Status404NotFound
            : StatusCodes.Status400BadRequest;

        return Results.Json(
            new StatusResponse(false, result.FailureCode.ToString(), message),
            statusCode: status);
    }

    // ─── POST /api/sites/{id}/activate|deactivate ───────────────────────────

    private static async Task<IResult> ActivateAsync(
        Guid id, IMediator mediator, CancellationToken ct)
    {
        var result = await mediator.Send(new ActivateSiteCommand(id), ct);
        return MapStatusResult(result, "Aktifleştirildi.");
    }

    private static async Task<IResult> DeactivateAsync(
        Guid id, IMediator mediator, CancellationToken ct)
    {
        var result = await mediator.Send(new DeactivateSiteCommand(id), ct);
        return MapStatusResult(result, "Pasifleştirildi.");
    }

    // ─── DELETE /api/sites/{id} ─────────────────────────────────────────────

    public sealed record DeleteRequestBody(string Reason);

    private static async Task<IResult> DeleteAsync(
        Guid id,
        [FromBody] DeleteRequestBody body,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.Send(new DeleteSiteCommand(id, body.Reason), ct);
        return MapStatusResult(result, "Silindi.");
    }

    // ─── Helper ─────────────────────────────────────────────────────────────

    private static IResult MapStatusResult(SiteStatusResult result, string successMessage)
    {
        if (result.IsSuccess)
            return TypedResults.Ok(new StatusResponse(true, null, successMessage));

        var message = result.ErrorMessage ?? result.FailureCode switch
        {
            SiteStatusFailureCode.NotFound => "Site bulunamadı.",
            SiteStatusFailureCode.AlreadyDeleted => "Site zaten silinmiş.",
            SiteStatusFailureCode.ValidationError => "Giriş doğrulama hatası.",
            _ => "İşlem başarısız."
        };

        var status = result.FailureCode == SiteStatusFailureCode.NotFound
            ? StatusCodes.Status404NotFound
            : StatusCodes.Status400BadRequest;

        return Results.Json(
            new StatusResponse(false, result.FailureCode.ToString(), message),
            statusCode: status);
    }
}
