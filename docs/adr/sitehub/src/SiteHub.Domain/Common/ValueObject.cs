namespace SiteHub.Domain.Common;

/// <summary>
/// Value Object için temel sınıf.
///
/// Value Object: kimliği olmayan, yalnızca değerlerine göre tanımlanan nesne.
/// İki value object'in tüm değerleri aynıysa ONLAR AYNIDIR.
///
/// Örnek: Para (100 TL), Adres (sokak + şehir + posta kodu), TCKN.
/// İki farklı 100 TL "aynı 100 TL"dir; ayrı kimlikleri yoktur.
///
/// Değişmez (immutable) olmalıdır: oluşturulduktan sonra değişmez.
/// </summary>
public abstract class ValueObject : IEquatable<ValueObject>
{
    /// <summary>
    /// Eşitlik karşılaştırması için kullanılacak bileşenler.
    /// Alt sınıflar bunu implement eder.
    /// </summary>
    protected abstract IEnumerable<object?> GetEqualityComponents();

    public bool Equals(ValueObject? other)
    {
        if (other is null) return false;
        if (GetType() != other.GetType()) return false;
        return GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());
    }

    public override bool Equals(object? obj) => Equals(obj as ValueObject);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var component in GetEqualityComponents())
        {
            hash.Add(component);
        }
        return hash.ToHashCode();
    }

    public static bool operator ==(ValueObject? left, ValueObject? right)
        => Equals(left, right);

    public static bool operator !=(ValueObject? left, ValueObject? right)
        => !Equals(left, right);
}
