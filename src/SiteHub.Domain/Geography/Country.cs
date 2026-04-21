using SiteHub.Domain.Common;

namespace SiteHub.Domain.Geography;

/// <summary>Country için strongly-typed ID.</summary>
public readonly record struct CountryId(Guid Value) : ITypedId<CountryId>
{
    public static CountryId New() => new(Guid.CreateVersion7());
    public static CountryId FromGuid(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

/// <summary>
/// Ülke kaydı — adres hiyerarşisinin tepesi.
///
/// Seed: 2 kayıt (Türkiye + Diğer/Yurt Dışı). SystemAdmin yeni ülke ekleyebilir.
///
/// Referans veri — runtime mutasyonları nadir, genelde salt-okunur.
/// Audit ve soft-delete yok (seed data).
/// </summary>
public sealed class Country : Entity<CountryId>
{
    public const string TurkeyIsoCode = "TR";

    public string IsoCode { get; private set; } = default!;  // "TR", "DE", "US" — 2 karakter
    public string Name { get; private set; } = default!;     // "Türkiye", "Almanya"
    public string? PhonePrefix { get; private set; }         // "+90", "+49"
    public int DisplayOrder { get; private set; }            // Türkiye en üstte (1), diğerleri alfabetik
    public bool IsActive { get; private set; }

    private Country() : base() { }

    private Country(CountryId id, string isoCode, string name, string? phonePrefix, int displayOrder)
        : base(id)
    {
        IsoCode = isoCode;
        Name = name;
        PhonePrefix = phonePrefix;
        DisplayOrder = displayOrder;
        IsActive = true;
    }

    public static Country Create(string isoCode, string name, string? phonePrefix, int displayOrder)
    {
        if (string.IsNullOrWhiteSpace(isoCode) || isoCode.Length != 2)
            throw new BusinessRuleViolationException("ISO kodu 2 karakter olmalı (örn. TR, DE).");

        if (string.IsNullOrWhiteSpace(name))
            throw new BusinessRuleViolationException("Ülke adı boş olamaz.");

        return new Country(CountryId.New(), isoCode.ToUpperInvariant(), name, phonePrefix, displayOrder);
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
}
