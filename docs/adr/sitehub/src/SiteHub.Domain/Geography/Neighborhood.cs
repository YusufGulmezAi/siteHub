using SiteHub.Domain.Common;

namespace SiteHub.Domain.Geography;

public readonly record struct NeighborhoodId(Guid Value) : ITypedId<NeighborhoodId>
{
    public static NeighborhoodId New() => new(Guid.CreateVersion7());
    public static NeighborhoodId FromGuid(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

/// <summary>
/// Mahalle — adres hiyerarşisinin en alt seviyesi. Türkiye'de ~4125 kayıt (MVP seed).
///
/// PostalCode: her mahalleye atanmış default posta kodu (varsa). Address kayıtlarında
/// override edilebilir (bir mahallede birden fazla posta kodu olabilir).
///
/// ExternalId: CSV'deki SEMT_ID (örn. "BİLİNMİYOR" için 99).
/// </summary>
public sealed class Neighborhood : Entity<NeighborhoodId>
{
    public DistrictId DistrictId { get; private set; }
    public int ExternalId { get; private set; }
    public string Name { get; private set; } = default!;
    public string? PostalCode { get; private set; }   // 5 haneli Türk posta kodu — bazı köylerde yok

    private Neighborhood() : base() { }

    private Neighborhood(NeighborhoodId id, DistrictId districtId, int externalId, string name, string? postalCode)
        : base(id)
    {
        DistrictId = districtId;
        ExternalId = externalId;
        Name = name;
        PostalCode = postalCode;
    }

    public static Neighborhood Create(
        DistrictId districtId, int externalId, string name, string? postalCode)
    {
        if (externalId <= 0)
            throw new BusinessRuleViolationException("Mahalle numarası pozitif olmalı.");
        if (string.IsNullOrWhiteSpace(name))
            throw new BusinessRuleViolationException("Mahalle adı boş olamaz.");

        if (!string.IsNullOrWhiteSpace(postalCode))
        {
            postalCode = postalCode.Trim();
            if (postalCode.Length != 5 || !postalCode.All(char.IsDigit))
                throw new BusinessRuleViolationException(
                    "Posta kodu 5 rakam olmalı. Bilinmiyorsa boş bırakın.");
        }
        else
        {
            postalCode = null;
        }

        return new Neighborhood(NeighborhoodId.New(), districtId, externalId, name, postalCode);
    }
}
