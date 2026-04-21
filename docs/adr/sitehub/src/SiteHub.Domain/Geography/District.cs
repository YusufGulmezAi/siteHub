using SiteHub.Domain.Common;

namespace SiteHub.Domain.Geography;

public readonly record struct DistrictId(Guid Value) : ITypedId<DistrictId>
{
    public static DistrictId New() => new(Guid.CreateVersion7());
    public static DistrictId FromGuid(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

/// <summary>
/// İlçe — il altındaki idari bölüm. Türkiye'de 958 ilçe (MVP seed).
///
/// ExternalId: kamu veri setinden (CSV'de ILCE_ID) sabit id. Import/export için.
/// </summary>
public sealed class District : Entity<DistrictId>
{
    public ProvinceId ProvinceId { get; private set; }
    public int ExternalId { get; private set; }
    public string Name { get; private set; } = default!;

    private District() : base() { }

    private District(DistrictId id, ProvinceId provinceId, int externalId, string name)
        : base(id)
    {
        ProvinceId = provinceId;
        ExternalId = externalId;
        Name = name;
    }

    public static District Create(ProvinceId provinceId, int externalId, string name)
    {
        if (externalId <= 0)
            throw new BusinessRuleViolationException("İlçe numarası pozitif olmalı.");
        if (string.IsNullOrWhiteSpace(name))
            throw new BusinessRuleViolationException("İlçe adı boş olamaz.");

        return new District(DistrictId.New(), provinceId, externalId, name);
    }
}
