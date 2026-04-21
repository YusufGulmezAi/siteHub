using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SiteHub.Application.Abstractions.Persistence;
using SiteHub.Domain.Identity;
using SiteHub.Domain.Tenancy.Organizations;

namespace SiteHub.Application.Features.Organizations;

/// <summary>
/// Organizasyon temel bilgilerini günceller (ad, unvan, VKN, iletişim).
/// Code alanı değişmez.
/// </summary>
public sealed record UpdateOrganizationCommand(
    Guid OrganizationId,
    string Name,
    string CommercialTitle,
    string TaxId,
    string? Address,
    string? Phone,
    string? Email)
    : IRequest<UpdateOrganizationResult>;

public sealed record UpdateOrganizationResult(
    bool IsSuccess,
    UpdateOrganizationFailureCode FailureCode = UpdateOrganizationFailureCode.None,
    string? ErrorMessage = null)
{
    public static UpdateOrganizationResult Success() => new(true);

    public static UpdateOrganizationResult Failure(UpdateOrganizationFailureCode code, string? message = null)
        => new(false, code, message);
}

public enum UpdateOrganizationFailureCode
{
    None = 0,
    NotFound = 1,
    InvalidTaxId = 2,
    TaxIdAlreadyExists = 3,    // Farklı bir firma bu VKN'i kullanıyor
    ValidationError = 4,
}

public sealed class UpdateOrganizationHandler
    : IRequestHandler<UpdateOrganizationCommand, UpdateOrganizationResult>
{
    private readonly ISiteHubDbContext _db;
    private readonly ILogger<UpdateOrganizationHandler> _logger;

    public UpdateOrganizationHandler(
        ISiteHubDbContext db,
        ILogger<UpdateOrganizationHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<UpdateOrganizationResult> Handle(
        UpdateOrganizationCommand cmd, CancellationToken ct)
    {
        var orgId = OrganizationId.FromGuid(cmd.OrganizationId);

        var org = await _db.Organizations.FirstOrDefaultAsync(o => o.Id == orgId, ct);
        if (org is null)
            return UpdateOrganizationResult.Failure(UpdateOrganizationFailureCode.NotFound);

        // VKN parse
        NationalId newTaxId;
        try
        {
            newTaxId = NationalId.Parse(cmd.TaxId);
            if (newTaxId.Type != NationalIdType.VKN)
            {
                return UpdateOrganizationResult.Failure(
                    UpdateOrganizationFailureCode.InvalidTaxId,
                    "VKN (10 haneli) gereklidir.");
            }
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException)
        {
            return UpdateOrganizationResult.Failure(
                UpdateOrganizationFailureCode.InvalidTaxId,
                "VKN formatı geçersiz.");
        }

        // VKN başka firmaya ait mi? (kendisi hariç)
        if (!org.TaxId.Equals(newTaxId))
        {
            var exists = await _db.Organizations
                .AnyAsync(o => o.TaxId == newTaxId && o.Id != orgId, ct);

            if (exists)
            {
                return UpdateOrganizationResult.Failure(
                    UpdateOrganizationFailureCode.TaxIdAlreadyExists,
                    $"Bu VKN ({newTaxId.Value}) ile kayıtlı başka firma mevcut.");
            }
        }

        try
        {
            org.Rename(cmd.Name, cmd.CommercialTitle);
            org.ChangeTaxId(newTaxId);
            org.UpdateContact(cmd.Address, cmd.Phone, cmd.Email);
        }
        catch (ArgumentException ex)
        {
            return UpdateOrganizationResult.Failure(
                UpdateOrganizationFailureCode.ValidationError, ex.Message);
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Organizasyon güncellendi: id={OrgId}, code={Code}.", org.Id, org.Code);

        return UpdateOrganizationResult.Success();
    }
}
