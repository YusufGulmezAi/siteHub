using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SiteHub.Application.Abstractions.Persistence;
using SiteHub.Domain.Geography;
using SiteHub.Domain.Identity;
using SiteHub.Domain.Tenancy.Sites;

namespace SiteHub.Application.Features.Sites;

/// <summary>
/// Site temel bilgilerini günceller (ad, unvan, adres, il/ilçe, IBAN, VKN).
/// Code alanı ve OrganizationId DEĞİŞMEZ.
/// </summary>
public sealed record UpdateSiteCommand(
    Guid SiteId,
    string Name,
    string Address,
    Guid ProvinceId,
    string? CommercialTitle = null,
    Guid? DistrictId = null,
    string? Iban = null,
    string? TaxId = null)
    : IRequest<UpdateSiteResult>;

public sealed record UpdateSiteResult(
    bool IsSuccess,
    UpdateSiteFailureCode FailureCode = UpdateSiteFailureCode.None,
    string? ErrorMessage = null)
{
    public static UpdateSiteResult Success() => new(true);

    public static UpdateSiteResult Failure(UpdateSiteFailureCode code, string? message = null)
        => new(false, code, message);
}

public enum UpdateSiteFailureCode
{
    None = 0,
    NotFound = 1,
    NameAlreadyExists = 2,      // Aynı org içinde başka bir Site bu ismi kullanıyor
    ProvinceNotFound = 3,
    DistrictNotFound = 4,
    InvalidTaxId = 5,
    InvalidIban = 6,
    ValidationError = 7,
}

public sealed class UpdateSiteHandler
    : IRequestHandler<UpdateSiteCommand, UpdateSiteResult>
{
    private readonly ISiteHubDbContext _db;
    private readonly ILogger<UpdateSiteHandler> _logger;

    public UpdateSiteHandler(
        ISiteHubDbContext db,
        ILogger<UpdateSiteHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<UpdateSiteResult> Handle(
        UpdateSiteCommand cmd, CancellationToken ct)
    {
        var siteId = SiteId.FromGuid(cmd.SiteId);
        var provinceId = ProvinceId.FromGuid(cmd.ProvinceId);
        var districtId = cmd.DistrictId.HasValue
            ? DistrictId.FromGuid(cmd.DistrictId.Value)
            : (DistrictId?)null;

        var site = await _db.Sites.FirstOrDefaultAsync(s => s.Id == siteId, ct);
        if (site is null)
            return UpdateSiteResult.Failure(UpdateSiteFailureCode.NotFound);

        // 1. İsim başka Site'ye ait mi? (kendisi hariç, aynı org içinde)
        var newName = (cmd.Name ?? string.Empty).Trim();
        if (!string.IsNullOrEmpty(newName) && site.Name != newName)
        {
            var nameExists = await _db.Sites.AnyAsync(
                s => s.OrganizationId == site.OrganizationId
                     && s.Name == newName
                     && s.Id != siteId,
                ct);

            if (nameExists)
            {
                return UpdateSiteResult.Failure(
                    UpdateSiteFailureCode.NameAlreadyExists,
                    $"Bu organizasyon içinde '{newName}' adında başka bir Site zaten var.");
            }
        }

        // 2. Province var mı?
        var provinceExists = await _db.Provinces.AnyAsync(p => p.Id == provinceId, ct);
        if (!provinceExists)
        {
            return UpdateSiteResult.Failure(
                UpdateSiteFailureCode.ProvinceNotFound,
                "Seçilen il bulunamadı.");
        }

        // 3. District (opsiyonel) il ile uyuşuyor mu?
        if (districtId.HasValue)
        {
            var district = await _db.Districts
                .AsNoTracking()
                .Where(d => d.Id == districtId.Value)
                .Select(d => new { d.ProvinceId })
                .FirstOrDefaultAsync(ct);

            if (district is null)
            {
                return UpdateSiteResult.Failure(
                    UpdateSiteFailureCode.DistrictNotFound,
                    "Seçilen ilçe bulunamadı.");
            }
            if (district.ProvinceId != provinceId)
            {
                return UpdateSiteResult.Failure(
                    UpdateSiteFailureCode.DistrictNotFound,
                    "Seçilen ilçe, il ile uyuşmuyor.");
            }
        }

        // 4. TaxId parse (eğer verilmişse)
        NationalId? newTaxId = null;
        if (!string.IsNullOrWhiteSpace(cmd.TaxId))
        {
            try
            {
                newTaxId = NationalId.Parse(cmd.TaxId);
                if (newTaxId.Type != NationalIdType.VKN)
                {
                    return UpdateSiteResult.Failure(
                        UpdateSiteFailureCode.InvalidTaxId,
                        "Site vergi numarası VKN olmalıdır.");
                }
            }
            catch (Exception ex) when (ex is ArgumentException or FormatException)
            {
                return UpdateSiteResult.Failure(
                    UpdateSiteFailureCode.InvalidTaxId,
                    "VKN formatı geçersiz.");
            }
        }

        // 5. Mutasyonları uygula — factory/mutation iç validation'ları yapar
        try
        {
            site.Rename(newName, cmd.CommercialTitle);
            site.ChangeAddress(cmd.Address ?? string.Empty, provinceId, districtId);

            // IBAN — null temizle, dolu ise set
            if (string.IsNullOrWhiteSpace(cmd.Iban))
            {
                if (site.Iban is not null)
                    site.ClearIban("Güncelleme ile IBAN kaldırıldı.");
            }
            else
            {
                site.SetIban(cmd.Iban);
            }

            // TaxId — null temizle, dolu ise set
            if (newTaxId is null)
            {
                if (site.TaxId is not null)
                    site.ClearTaxId();
            }
            else
            {
                site.SetTaxId(newTaxId);
            }
        }
        catch (ArgumentException ex) when (ex.Message.Contains("IBAN", StringComparison.Ordinal))
        {
            return UpdateSiteResult.Failure(
                UpdateSiteFailureCode.InvalidIban, ex.Message);
        }
        catch (ArgumentException ex)
        {
            return UpdateSiteResult.Failure(
                UpdateSiteFailureCode.ValidationError, ex.Message);
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Site güncellendi: id={SiteId}, code={Code}.", site.Id, site.Code);

        return UpdateSiteResult.Success();
    }
}
