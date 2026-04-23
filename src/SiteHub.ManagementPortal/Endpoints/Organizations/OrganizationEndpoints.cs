using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using SiteHub.Application.Features.Organizations;
using SiteHub.Contracts.Common;
using SiteHub.Contracts.Organizations;

namespace SiteHub.ManagementPortal.Endpoints.Organizations;

/// <summary>
/// <c>/api/organizations</c> — Firma (Organization) CRUD endpoint'leri.
///
/// <para><b>Yetki (Faz E MVP):</b> <see cref="RequireAuthorization"/> — authenticated kullanıcı yeterli.
/// Gerçek permission check (<c>firm.create</c>, <c>firm.edit</c>, vb.) Faz F'de eklenir.</para>
///
/// <para><b>F.6 Cleanup:</b> Response DTO'ları (OrganizationListItemDto / OrganizationDetailDto /
/// PagedResult) artık Contracts'tan geliyor. Command/Query + Result tipleri Application'da kalır.</para>
/// </summary>
public sealed class OrganizationEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/organizations")
            .WithTags("Organizations")
            .RequireAuthorization()
            .DisableAntiforgery();

        group.MapGet("/", GetListAsync).WithName("GetOrganizations");
        group.MapGet("/{id:guid}", GetByIdAsync).WithName("GetOrganizationById");
        group.MapPost("/", CreateAsync).WithName("CreateOrganization");
        group.MapPut("/{id:guid}", UpdateAsync).WithName("UpdateOrganization");
        group.MapPost("/{id:guid}/activate", ActivateAsync).WithName("ActivateOrganization");
        group.MapPost("/{id:guid}/deactivate", DeactivateAsync).WithName("DeactivateOrganization");
        group.MapDelete("/{id:guid}", DeleteAsync).WithName("DeleteOrganization");
    }

    // ─── GET /api/organizations (list) ──────────────────────────────────────

    public sealed record ListQueryParams(
        int Page = 1,
        int PageSize = 20,
        string? Search = null,
        bool IncludeInactive = false);

    private static async Task<Ok<PagedResult<OrganizationListItemDto>>> GetListAsync(
        [AsParameters] ListQueryParams p,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.Send(
            new GetOrganizationsQuery(p.Page, p.PageSize, p.Search, p.IncludeInactive), ct);
        return TypedResults.Ok(result);
    }

    // ─── GET /api/organizations/{id} (detail) ───────────────────────────────

    private static async Task<IResult> GetByIdAsync(
        Guid id, IMediator mediator, CancellationToken ct)
    {
        var dto = await mediator.Send(new GetOrganizationByIdQuery(id), ct);
        return dto is null
            ? TypedResults.NotFound(new { message = "Organizasyon bulunamadı." })
            : TypedResults.Ok(dto);
    }

    // ─── POST /api/organizations (create) ───────────────────────────────────

    public sealed record CreateRequestBody(
        string Name,
        string CommercialTitle,
        string TaxId,
        string? Address,
        string? Phone,
        string? Email);

    public sealed record CreateResponse(
        bool Success,
        Guid? OrganizationId,
        long? Code,
        string? FailureCode,
        string? Message);

    private static async Task<IResult> CreateAsync(
        [FromBody] CreateRequestBody body,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.Send(new CreateOrganizationCommand(
            body.Name, body.CommercialTitle, body.TaxId,
            body.Address, body.Phone, body.Email), ct);

        if (result.IsSuccess)
        {
            return TypedResults.Created(
                $"/api/organizations/{result.OrganizationId}",
                new CreateResponse(
                    Success: true,
                    OrganizationId: result.OrganizationId,
                    Code: result.Code,
                    FailureCode: null,
                    Message: null));
        }

        var message = result.ErrorMessage ?? result.FailureCode switch
        {
            CreateOrganizationFailureCode.InvalidTaxId => "VKN geçersiz.",
            CreateOrganizationFailureCode.TaxIdAlreadyExists => "Bu VKN zaten kayıtlı.",
            CreateOrganizationFailureCode.ValidationError => "Giriş doğrulama hatası.",
            _ => "Oluşturulamadı."
        };

        return TypedResults.BadRequest(new CreateResponse(
            Success: false,
            OrganizationId: null,
            Code: null,
            FailureCode: result.FailureCode.ToString(),
            Message: message));
    }

    // ─── PUT /api/organizations/{id} (update) ───────────────────────────────

    public sealed record UpdateRequestBody(
        string Name,
        string CommercialTitle,
        string TaxId,
        string? Address,
        string? Phone,
        string? Email);

    public sealed record StatusResponse(bool Success, string? Code, string? Message);

    private static async Task<IResult> UpdateAsync(
        Guid id,
        [FromBody] UpdateRequestBody body,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.Send(new UpdateOrganizationCommand(
            id, body.Name, body.CommercialTitle, body.TaxId,
            body.Address, body.Phone, body.Email), ct);

        if (result.IsSuccess)
            return TypedResults.Ok(new StatusResponse(true, null, "Güncellendi."));

        var message = result.ErrorMessage ?? result.FailureCode switch
        {
            UpdateOrganizationFailureCode.NotFound => "Organizasyon bulunamadı.",
            UpdateOrganizationFailureCode.InvalidTaxId => "VKN geçersiz.",
            UpdateOrganizationFailureCode.TaxIdAlreadyExists => "Bu VKN başka firmaya ait.",
            UpdateOrganizationFailureCode.ValidationError => "Giriş doğrulama hatası.",
            _ => "Güncellenemedi."
        };

        var status = result.FailureCode == UpdateOrganizationFailureCode.NotFound
            ? StatusCodes.Status404NotFound
            : StatusCodes.Status400BadRequest;

        return Results.Json(
            new StatusResponse(false, result.FailureCode.ToString(), message),
            statusCode: status);
    }

    // ─── POST /api/organizations/{id}/activate ──────────────────────────────

    private static async Task<IResult> ActivateAsync(
        Guid id, IMediator mediator, CancellationToken ct)
    {
        var result = await mediator.Send(new ActivateOrganizationCommand(id), ct);
        return MapStatusResult(result, "Aktifleştirildi.");
    }

    // ─── POST /api/organizations/{id}/deactivate ────────────────────────────

    private static async Task<IResult> DeactivateAsync(
        Guid id, IMediator mediator, CancellationToken ct)
    {
        var result = await mediator.Send(new DeactivateOrganizationCommand(id), ct);
        return MapStatusResult(result, "Pasifleştirildi.");
    }

    // ─── DELETE /api/organizations/{id} ─────────────────────────────────────

    public sealed record DeleteRequestBody(string Reason);

    private static async Task<IResult> DeleteAsync(
        Guid id,
        [FromBody] DeleteRequestBody body,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.Send(
            new DeleteOrganizationCommand(id, body.Reason), ct);
        return MapStatusResult(result, "Silindi.");
    }

    // ─── Helper — durum response mapleyici ─────────────────────────────────

    private static IResult MapStatusResult(OrganizationStatusResult result, string successMessage)
    {
        if (result.IsSuccess)
            return TypedResults.Ok(new StatusResponse(true, null, successMessage));

        var message = result.ErrorMessage ?? result.FailureCode switch
        {
            OrganizationStatusFailureCode.NotFound => "Organizasyon bulunamadı.",
            OrganizationStatusFailureCode.AlreadyDeleted => "Organizasyon zaten silinmiş.",
            OrganizationStatusFailureCode.ValidationError => "Giriş doğrulama hatası.",
            _ => "İşlem başarısız."
        };

        var status = result.FailureCode == OrganizationStatusFailureCode.NotFound
            ? StatusCodes.Status404NotFound
            : StatusCodes.Status400BadRequest;

        return Results.Json(
            new StatusResponse(false, result.FailureCode.ToString(), message),
            statusCode: status);
    }
}
