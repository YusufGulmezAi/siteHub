using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SiteHub.Application.Abstractions.CodeGeneration;
using SiteHub.Application.Abstractions.Persistence;
using SiteHub.Domain.Geography;
using SiteHub.Domain.Identity;
using SiteHub.Domain.Tenancy.Organizations;
using SiteHub.Domain.Tenancy.Sites;

namespace SiteHub.Application.Features.Sites;

/// <summary>
/// Yeni Site (apartman/kompleks) oluşturur.
///
/// <para><b>Yetki:</b> Organization sahibi veya Sistem Yöneticisi (Faz F MVP: sadece authenticated).</para>
///
/// <para><b>Input:</b> <paramref name="OrganizationId"/> URL'den gelir (nested REST:
/// <c>POST /api/organizations/{orgId}/sites</c>). Diğer alanlar body'de.</para>
/// </summary>
public sealed record CreateSiteCommand(
    Guid OrganizationId,
    string Name,
    Guid ProvinceId,
    string Address,
    string? CommercialTitle = null,
    Guid? DistrictId = null,
    string? Iban = null,
    string? TaxId = null)
    : IRequest<CreateSiteResult>;

public sealed record CreateSiteResult(
    bool IsSuccess,
    Guid? SiteId = null,
    long? Code = null,
    CreateSiteFailureCode FailureCode = CreateSiteFailureCode.None,
    string? ErrorMessage = null)
{
    public static CreateSiteResult Success(Guid id, long code) => new(true, id, code);

    public static CreateSiteResult Failure(CreateSiteFailureCode code, string? message = null)
        => new(false, FailureCode: code, ErrorMessage: message);
}

public enum CreateSiteFailureCode
{
    None = 0,

    /// <summary>Parent Organization bulunamadı.</summary>
    OrganizationNotFound = 1,

    /// <summary>Aynı Organization içinde bu isimde Site zaten var.</summary>
    NameAlreadyExists = 2,

    /// <summary>İl (Province) bulunamadı.</summary>
    ProvinceNotFound = 3,

    /// <summary>İlçe (District) bulunamadı ya da verilen il ile uyuşmuyor.</summary>
    DistrictNotFound = 4,

    /// <summary>VKN format geçersiz.</summary>
    InvalidTaxId = 5,

    /// <summary>IBAN format geçersiz.</summary>
    InvalidIban = 6,

    /// <summary>Ad/adres gibi alan boş veya domain invariant ihlali.</summary>
    ValidationError = 7,
}

public sealed class CreateSiteHandler
    : IRequestHandler<CreateSiteCommand, CreateSiteResult>
{
    private readonly ISiteHubDbContext _db;
    private readonly ICodeGenerator _codeGenerator;
    private readonly ILogger<CreateSiteHandler> _logger;

    public CreateSiteHandler(
        ISiteHubDbContext db,
        ICodeGenerator codeGenerator,
        ILogger<CreateSiteHandler> logger)
    {
        _db = db;
        _codeGenerator = codeGenerator;
        _logger = logger;
    }

    public async Task<CreateSiteResult> Handle(
        CreateSiteCommand cmd, CancellationToken ct)
    {
        var orgId = OrganizationId.FromGuid(cmd.OrganizationId);
        var provinceId = ProvinceId.FromGuid(cmd.ProvinceId);
        var districtId = cmd.DistrictId.HasValue
            ? DistrictId.FromGuid(cmd.DistrictId.Value)
            : (DistrictId?)null;

        // 1. Parent Organization var mı ve aktif mi?
        var orgExists = await _db.Organizations.AnyAsync(o => o.Id == orgId, ct);
        if (!orgExists)
        {
            return CreateSiteResult.Failure(
                CreateSiteFailureCode.OrganizationNotFound,
                "Parent organizasyon bulunamadı.");
        }

        // 2. Province var mı?
        var provinceExists = await _db.Provinces.AnyAsync(p => p.Id == provinceId, ct);
        if (!provinceExists)
        {
            return CreateSiteResult.Failure(
                CreateSiteFailureCode.ProvinceNotFound,
                "Seçilen il bulunamadı.");
        }

        // 3. District (opsiyonel) varsa var mı + il ile uyuşuyor mu?
        if (districtId.HasValue)
        {
            var district = await _db.Districts
                .AsNoTracking()
                .Where(d => d.Id == districtId.Value)
                .Select(d => new { d.Id, d.ProvinceId })
                .FirstOrDefaultAsync(ct);

            if (district is null)
            {
                return CreateSiteResult.Failure(
                    CreateSiteFailureCode.DistrictNotFound,
                    "Seçilen ilçe bulunamadı.");
            }
            if (district.ProvinceId != provinceId)
            {
                return CreateSiteResult.Failure(
                    CreateSiteFailureCode.DistrictNotFound,
                    "Seçilen ilçe, il ile uyuşmuyor.");
            }
        }

        // 4. İsim unique kontrolü (aynı org içinde, aktif kayıtlar)
        var name = (cmd.Name ?? string.Empty).Trim();
        if (!string.IsNullOrEmpty(name))
        {
            var nameExists = await _db.Sites
                .AnyAsync(s => s.OrganizationId == orgId && s.Name == name, ct);

            if (nameExists)
            {
                return CreateSiteResult.Failure(
                    CreateSiteFailureCode.NameAlreadyExists,
                    $"Bu organizasyon içinde '{name}' adında başka bir Site zaten var.");
            }
        }

        // 5. TaxId parse (eğer verilmişse)
        NationalId? taxId = null;
        if (!string.IsNullOrWhiteSpace(cmd.TaxId))
        {
            try
            {
                taxId = NationalId.Parse(cmd.TaxId);
                if (taxId.Type != NationalIdType.VKN)
                {
                    return CreateSiteResult.Failure(
                        CreateSiteFailureCode.InvalidTaxId,
                        "Site vergi numarası VKN olmalıdır (10 hane), TCKN kabul edilmez.");
                }
            }
            catch (Exception ex) when (ex is ArgumentException or FormatException)
            {
                return CreateSiteResult.Failure(
                    CreateSiteFailureCode.InvalidTaxId,
                    "VKN formatı geçersiz.");
            }
        }

        // 6. Kod üret (6 haneli Feistel — Organization ile aynı aralık)
        var code = await _codeGenerator.GenerateAsync<Site>(ct);

        // 7. Entity oluştur — factory iç validation'ları yapar
        Site site;
        try
        {
            site = Site.Create(
                code: code,
                organizationId: orgId,
                name: name,
                provinceId: provinceId,
                address: cmd.Address ?? string.Empty,
                commercialTitle: cmd.CommercialTitle,
                districtId: districtId,
                iban: cmd.Iban,
                taxId: taxId);
        }
        catch (ArgumentException ex) when (ex.Message.Contains("IBAN", StringComparison.Ordinal))
        {
            return CreateSiteResult.Failure(
                CreateSiteFailureCode.InvalidIban, ex.Message);
        }
        catch (ArgumentException ex)
        {
            return CreateSiteResult.Failure(
                CreateSiteFailureCode.ValidationError, ex.Message);
        }

        _db.Sites.Add(site);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Site oluşturuldu: id={SiteId}, code={Code}, orgId={OrgId}, name={Name}.",
            site.Id, site.Code, site.OrganizationId, site.Name);

        return CreateSiteResult.Success(site.Id.Value, site.Code);
    }
}
