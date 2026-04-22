using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SiteHub.Application.Abstractions.CodeGeneration;
using SiteHub.Application.Abstractions.Persistence;
using SiteHub.Domain.Identity;
using SiteHub.Domain.Tenancy.Organizations;

namespace SiteHub.Application.Features.Organizations;

/// <summary>
/// Yeni organizasyon (yönetim firması / kiracı) oluşturur.
///
/// <para><b>Yetki:</b> Sadece Sistem Yöneticisi (permission: <c>firm.create</c>).
/// Kontrol endpoint/pipeline katmanında yapılır.</para>
///
/// <para>VKN zorunludur — tüzel kişi kaydı. Aktif kayıtlar arasında unique.</para>
/// </summary>
public sealed record CreateOrganizationCommand(
    string Name,
    string CommercialTitle,
    string TaxId,             // 10 haneli VKN, string olarak alınıp parse edilir
    string? Address,
    string? Phone,
    string? Email)
    : IRequest<CreateOrganizationResult>;

public sealed record CreateOrganizationResult(
    bool IsSuccess,
    Guid? OrganizationId = null,
    long? Code = null,
    CreateOrganizationFailureCode FailureCode = CreateOrganizationFailureCode.None,
    string? ErrorMessage = null)
{
    public static CreateOrganizationResult Success(Guid id, long code) => new(true, id, code);

    public static CreateOrganizationResult Failure(CreateOrganizationFailureCode code, string? message = null)
        => new(false, FailureCode: code, ErrorMessage: message);
}

public enum CreateOrganizationFailureCode
{
    None = 0,

    /// <summary>VKN format geçersiz (10 hane değil, checksum fail).</summary>
    InvalidTaxId = 1,

    /// <summary>Bu VKN'li başka aktif firma zaten var.</summary>
    TaxIdAlreadyExists = 2,

    /// <summary>Ad / unvan gibi alan boş.</summary>
    ValidationError = 3
}

public sealed class CreateOrganizationHandler
    : IRequestHandler<CreateOrganizationCommand, CreateOrganizationResult>
{
    private readonly ISiteHubDbContext _db;
    private readonly ICodeGenerator _codeGenerator;
    private readonly ILogger<CreateOrganizationHandler> _logger;

    public CreateOrganizationHandler(
        ISiteHubDbContext db,
        ICodeGenerator codeGenerator,
        ILogger<CreateOrganizationHandler> logger)
    {
        _db = db;
        _codeGenerator = codeGenerator;
        _logger = logger;
    }

    public async Task<CreateOrganizationResult> Handle(
        CreateOrganizationCommand cmd, CancellationToken ct)
    {
        // 1. VKN validasyon (checksum OLMADAN — ilerde banka entegrasyonunda
        //    Gelir İdaresi servisi açılınca tam doğrulama gelir)
        NationalId taxId;
        try
        {
            taxId = NationalId.CreateVknRelaxed(cmd.TaxId);
        }
        catch (InvalidNationalIdException ex)
        {
            return CreateOrganizationResult.Failure(
                CreateOrganizationFailureCode.InvalidTaxId, ex.Message);
        }

        // 2. VKN unique kontrolü — çakışma varsa firma adını da dönelim
        //    (UI kullanıcıya "X firmasında kayıtlı" mesajı gösterir)
        var conflicting = await _db.Organizations
            .Where(o => o.TaxId == taxId)
            .Select(o => new { o.Name })
            .FirstOrDefaultAsync(ct);

        if (conflicting is not null)
        {
            return CreateOrganizationResult.Failure(
                CreateOrganizationFailureCode.TaxIdAlreadyExists,
                $"Bu VKN '{conflicting.Name}' firmasında kayıtlı.");
        }

        // 3. Kod üret (6 haneli Feistel)
        var code = await _codeGenerator.GenerateAsync<Organization>(ct);

        // 4. Entity oluştur + kaydet
        Organization org;
        try
        {
            org = Organization.Create(
                code: code,
                name: cmd.Name,
                commercialTitle: cmd.CommercialTitle,
                taxId: taxId);

            org.UpdateContact(cmd.Address, cmd.Phone, cmd.Email);
        }
        catch (ArgumentException ex)
        {
            return CreateOrganizationResult.Failure(
                CreateOrganizationFailureCode.ValidationError, ex.Message);
        }

        _db.Organizations.Add(org);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Organizasyon oluşturuldu: id={OrgId}, code={Code}, name={Name}, taxId={TaxId}.",
            org.Id, org.Code, org.Name, org.TaxId.Value);

        return CreateOrganizationResult.Success(org.Id.Value, org.Code);
    }
}
