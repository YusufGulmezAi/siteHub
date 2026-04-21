using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SiteHub.Application.Abstractions.Persistence;
using SiteHub.Domain.Tenancy.Organizations;

namespace SiteHub.Application.Features.Organizations;

// ─── Activate ────────────────────────────────────────────────────────────

public sealed record ActivateOrganizationCommand(Guid OrganizationId)
    : IRequest<OrganizationStatusResult>;

public sealed class ActivateOrganizationHandler
    : IRequestHandler<ActivateOrganizationCommand, OrganizationStatusResult>
{
    private readonly ISiteHubDbContext _db;
    private readonly ILogger<ActivateOrganizationHandler> _logger;

    public ActivateOrganizationHandler(ISiteHubDbContext db, ILogger<ActivateOrganizationHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<OrganizationStatusResult> Handle(
        ActivateOrganizationCommand cmd, CancellationToken ct)
    {
        var orgId = OrganizationId.FromGuid(cmd.OrganizationId);
        var org = await _db.Organizations.FirstOrDefaultAsync(o => o.Id == orgId, ct);
        if (org is null)
            return OrganizationStatusResult.Failure(OrganizationStatusFailureCode.NotFound);

        org.Activate();
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Organizasyon aktifleştirildi: id={OrgId}.", org.Id);
        return OrganizationStatusResult.Success();
    }
}

// ─── Deactivate ──────────────────────────────────────────────────────────

public sealed record DeactivateOrganizationCommand(Guid OrganizationId)
    : IRequest<OrganizationStatusResult>;

public sealed class DeactivateOrganizationHandler
    : IRequestHandler<DeactivateOrganizationCommand, OrganizationStatusResult>
{
    private readonly ISiteHubDbContext _db;
    private readonly ILogger<DeactivateOrganizationHandler> _logger;

    public DeactivateOrganizationHandler(ISiteHubDbContext db, ILogger<DeactivateOrganizationHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<OrganizationStatusResult> Handle(
        DeactivateOrganizationCommand cmd, CancellationToken ct)
    {
        var orgId = OrganizationId.FromGuid(cmd.OrganizationId);
        var org = await _db.Organizations.FirstOrDefaultAsync(o => o.Id == orgId, ct);
        if (org is null)
            return OrganizationStatusResult.Failure(OrganizationStatusFailureCode.NotFound);

        org.Deactivate();
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Organizasyon pasifleştirildi: id={OrgId}.", org.Id);
        return OrganizationStatusResult.Success();
    }
}

// ─── Delete (soft) ───────────────────────────────────────────────────────

/// <summary>
/// Soft delete. <c>DeletedAt</c> doldurulur, liste sorgularından düşer.
/// Geri alma ileride "restore" endpoint'i ile yapılabilir (Faz G+).
/// </summary>
public sealed record DeleteOrganizationCommand(Guid OrganizationId, string Reason)
    : IRequest<OrganizationStatusResult>;

public sealed class DeleteOrganizationHandler
    : IRequestHandler<DeleteOrganizationCommand, OrganizationStatusResult>
{
    private readonly ISiteHubDbContext _db;
    private readonly TimeProvider _time;
    private readonly ILogger<DeleteOrganizationHandler> _logger;

    public DeleteOrganizationHandler(
        ISiteHubDbContext db,
        TimeProvider time,
        ILogger<DeleteOrganizationHandler> logger)
    {
        _db = db;
        _time = time;
        _logger = logger;
    }

    public async Task<OrganizationStatusResult> Handle(
        DeleteOrganizationCommand cmd, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cmd.Reason))
            return OrganizationStatusResult.Failure(
                OrganizationStatusFailureCode.ValidationError,
                "Silme sebebi zorunludur (ADR-0006).");

        var orgId = OrganizationId.FromGuid(cmd.OrganizationId);
        var org = await _db.Organizations.FirstOrDefaultAsync(o => o.Id == orgId, ct);
        if (org is null)
            return OrganizationStatusResult.Failure(OrganizationStatusFailureCode.NotFound);

        try
        {
            org.SoftDelete(cmd.Reason, _time.GetUtcNow());
        }
        catch (InvalidOperationException)
        {
            return OrganizationStatusResult.Failure(
                OrganizationStatusFailureCode.AlreadyDeleted,
                "Bu organizasyon zaten silinmiş.");
        }
        catch (ArgumentException ex)
        {
            return OrganizationStatusResult.Failure(
                OrganizationStatusFailureCode.ValidationError, ex.Message);
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation(
            "Organizasyon soft-delete: id={OrgId}, reason={Reason}.", org.Id, cmd.Reason);
        return OrganizationStatusResult.Success();
    }
}

// ─── Ortak result tipi ───────────────────────────────────────────────────

public sealed record OrganizationStatusResult(
    bool IsSuccess,
    OrganizationStatusFailureCode FailureCode = OrganizationStatusFailureCode.None,
    string? ErrorMessage = null)
{
    public static OrganizationStatusResult Success() => new(true);
    public static OrganizationStatusResult Failure(OrganizationStatusFailureCode code, string? message = null)
        => new(false, code, message);
}

public enum OrganizationStatusFailureCode
{
    None = 0,
    NotFound = 1,
    AlreadyDeleted = 2,
    ValidationError = 3,
}
