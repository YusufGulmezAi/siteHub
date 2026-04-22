using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SiteHub.Application.Abstractions.Persistence;
using SiteHub.Application.Abstractions.Tenancy;
using SiteHub.Domain.Tenancy.Sites;

namespace SiteHub.Application.Features.Sites;

// ─── Activate ────────────────────────────────────────────────────────────

public sealed record ActivateSiteCommand(Guid SiteId)
    : IRequest<SiteStatusResult>;

public sealed class ActivateSiteHandler
    : IRequestHandler<ActivateSiteCommand, SiteStatusResult>
{
    private readonly ISiteHubDbContext _db;
    private readonly ILogger<ActivateSiteHandler> _logger;

    public ActivateSiteHandler(ISiteHubDbContext db, ILogger<ActivateSiteHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<SiteStatusResult> Handle(
        ActivateSiteCommand cmd, CancellationToken ct)
    {
        var siteId = SiteId.FromGuid(cmd.SiteId);
        var site = await _db.Sites.FirstOrDefaultAsync(s => s.Id == siteId, ct);
        if (site is null)
            return SiteStatusResult.Failure(SiteStatusFailureCode.NotFound);

        site.Activate();
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Site aktifleştirildi: id={SiteId}.", site.Id);
        return SiteStatusResult.Success();
    }
}

// ─── Deactivate ──────────────────────────────────────────────────────────

public sealed record DeactivateSiteCommand(Guid SiteId)
    : IRequest<SiteStatusResult>;

public sealed class DeactivateSiteHandler
    : IRequestHandler<DeactivateSiteCommand, SiteStatusResult>
{
    private readonly ISiteHubDbContext _db;
    private readonly ILogger<DeactivateSiteHandler> _logger;

    public DeactivateSiteHandler(ISiteHubDbContext db, ILogger<DeactivateSiteHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<SiteStatusResult> Handle(
        DeactivateSiteCommand cmd, CancellationToken ct)
    {
        var siteId = SiteId.FromGuid(cmd.SiteId);
        var site = await _db.Sites.FirstOrDefaultAsync(s => s.Id == siteId, ct);
        if (site is null)
            return SiteStatusResult.Failure(SiteStatusFailureCode.NotFound);

        site.Deactivate();
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Site pasifleştirildi: id={SiteId}.", site.Id);
        return SiteStatusResult.Success();
    }
}

// ─── Delete (soft) ───────────────────────────────────────────────────────

/// <summary>
/// Soft delete. Audit için <paramref name="Reason"/> zorunlu (ADR-0006).
/// </summary>
public sealed record DeleteSiteCommand(Guid SiteId, string Reason)
    : IRequest<SiteStatusResult>;

public sealed class DeleteSiteHandler
    : IRequestHandler<DeleteSiteCommand, SiteStatusResult>
{
    private readonly ISiteHubDbContext _db;
    private readonly ISiteOrgResolver _siteOrgResolver;
    private readonly TimeProvider _time;
    private readonly ILogger<DeleteSiteHandler> _logger;

    public DeleteSiteHandler(
        ISiteHubDbContext db,
        ISiteOrgResolver siteOrgResolver,
        TimeProvider time,
        ILogger<DeleteSiteHandler> logger)
    {
        _db = db;
        _siteOrgResolver = siteOrgResolver;
        _time = time;
        _logger = logger;
    }

    public async Task<SiteStatusResult> Handle(
        DeleteSiteCommand cmd, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cmd.Reason))
            return SiteStatusResult.Failure(
                SiteStatusFailureCode.ValidationError,
                "Silme sebebi zorunludur (ADR-0006).");

        var siteId = SiteId.FromGuid(cmd.SiteId);
        var site = await _db.Sites.FirstOrDefaultAsync(s => s.Id == siteId, ct);
        if (site is null)
            return SiteStatusResult.Failure(SiteStatusFailureCode.NotFound);

        try
        {
            site.SoftDelete(cmd.Reason, _time.GetUtcNow());
        }
        catch (InvalidOperationException)
        {
            return SiteStatusResult.Failure(
                SiteStatusFailureCode.AlreadyDeleted,
                "Bu Site zaten silinmiş.");
        }
        catch (ArgumentException ex)
        {
            return SiteStatusResult.Failure(
                SiteStatusFailureCode.ValidationError, ex.Message);
        }

        await _db.SaveChangesAsync(ct);

        // Faz F.4: Cache invalidate — silinen Site'ın resolve çağrıları artık null dönsün
        _siteOrgResolver.InvalidateCacheFor(site.Id.Value);

        _logger.LogInformation(
            "Site soft-delete: id={SiteId}, reason={Reason}.", site.Id, cmd.Reason);
        return SiteStatusResult.Success();
    }
}

// ─── Ortak result tipi ───────────────────────────────────────────────────

public sealed record SiteStatusResult(
    bool IsSuccess,
    SiteStatusFailureCode FailureCode = SiteStatusFailureCode.None,
    string? ErrorMessage = null)
{
    public static SiteStatusResult Success() => new(true);
    public static SiteStatusResult Failure(SiteStatusFailureCode code, string? message = null)
        => new(false, code, message);
}

public enum SiteStatusFailureCode
{
    None = 0,
    NotFound = 1,
    AlreadyDeleted = 2,
    ValidationError = 3,
}
