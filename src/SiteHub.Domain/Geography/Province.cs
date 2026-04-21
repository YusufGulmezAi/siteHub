using SiteHub.Domain.Common;

namespace SiteHub.Domain.Geography;

public readonly record struct ProvinceId(Guid Value) : ITypedId<ProvinceId>
{
    public static ProvinceId New() => new(Guid.CreateVersion7());
    public static ProvinceId FromGuid(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

/// <summary>
/// İl — Türkiye için 81 il + Türkiye Dışı için placeholder.
///
/// ExternalId: kamu veri setlerindeki il numarası (1-81). Import/export için.
/// </summary>
public sealed class Province : Entity<ProvinceId>
{
    public RegionId RegionId { get; private set; }
    public int ExternalId { get; private set; }          // 1-81 (İstanbul=34, Ankara=6 vb.)
    public string Name { get; private set; } = default!; // "İstanbul", "Ankara"
    public string PlateCode { get; private set; } = default!; // "34", "06" (plaka kodu)

    private Province() : base() { }

    private Province(ProvinceId id, RegionId regionId, int externalId, string name, string plateCode)
        : base(id)
    {
        RegionId = regionId;
        ExternalId = externalId;
        Name = name;
        PlateCode = plateCode;
    }

    public static Province Create(RegionId regionId, int externalId, string name, string plateCode)
    {
        if (externalId <= 0)
            throw new BusinessRuleViolationException("İl numarası pozitif olmalı.");
        if (string.IsNullOrWhiteSpace(name))
            throw new BusinessRuleViolationException("İl adı boş olamaz.");
        if (string.IsNullOrWhiteSpace(plateCode))
            throw new BusinessRuleViolationException("Plaka kodu boş olamaz.");

        return new Province(ProvinceId.New(), regionId, externalId, name, plateCode);
    }
}
