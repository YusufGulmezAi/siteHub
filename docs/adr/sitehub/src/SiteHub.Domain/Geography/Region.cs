using SiteHub.Domain.Common;

namespace SiteHub.Domain.Geography;

public readonly record struct RegionId(Guid Value) : ITypedId<RegionId>
{
    public static RegionId New() => new(Guid.CreateVersion7());
    public static RegionId FromGuid(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

/// <summary>
/// Bölge — ülke altındaki coğrafi bölge.
///
/// Türkiye için seed: 7 coğrafi bölge (Marmara, Ege, Akdeniz, İç Anadolu,
/// Karadeniz, Doğu Anadolu, Güneydoğu Anadolu) + 1 özel bölge ("Türkiye Dışı"
/// — yurt dışı adresler için placeholder).
///
/// Kullanım: Cascading dropdown'da ülke seçilince bu dropdown doldurulur.
/// </summary>
public sealed class Region : Entity<RegionId>
{
    public CountryId CountryId { get; private set; }
    public string Name { get; private set; } = default!;   // "Marmara", "Ege"
    public string Code { get; private set; } = default!;   // "MARMARA", "EGE" — unique per country
    public int DisplayOrder { get; private set; }

    private Region() : base() { }

    private Region(RegionId id, CountryId countryId, string name, string code, int displayOrder)
        : base(id)
    {
        CountryId = countryId;
        Name = name;
        Code = code;
        DisplayOrder = displayOrder;
    }

    public static Region Create(CountryId countryId, string name, string code, int displayOrder)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new BusinessRuleViolationException("Bölge adı boş olamaz.");
        if (string.IsNullOrWhiteSpace(code))
            throw new BusinessRuleViolationException("Bölge kodu boş olamaz.");

        return new Region(RegionId.New(), countryId, name, code.ToUpperInvariant(), displayOrder);
    }
}
