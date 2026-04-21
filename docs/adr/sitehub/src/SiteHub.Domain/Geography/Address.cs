using SiteHub.Domain.Common;
using SiteHub.Domain.Geography;

namespace SiteHub.Domain.Geography;

/// <summary>Address için strongly-typed ID.</summary>
public readonly record struct AddressId(Guid Value) : ITypedId<AddressId>
{
    public static AddressId New() => new(Guid.CreateVersion7());
    public static AddressId FromGuid(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

/// <summary>
/// Adres kaydı — tüm entity'ler (Organization, Site, Person, Branch) için ortak
/// adres tablosu (ADR-0012 §12).
///
/// NEDEN AYRI ENTITY, VALUE OBJECT DEĞİL?
/// ──────────────────────────────────────
/// Adres bir value object gibi görünse de:
/// - Bir org'un ofisi taşındığında adres güncellenir, referanslar etkilenmez
/// - Tarihçe ve audit için ayrı yaşam döngüsü lazım
/// - Farklı entity'ler aynı adrese bağlanabilir (örn. aynı binadaki iki şube)
///
/// Hiyerarşi: Neighborhood → District → Province → Region → Country
/// Bu entity sadece NeighborhoodId tutar; üst hiyerarşi join ile alınır.
/// </summary>
public sealed class Address : Entity<AddressId>, IAuditable, ISoftDeletable
{
    // ─── Core ────────────────────────────────────────────────────────────
    public NeighborhoodId NeighborhoodId { get; private set; }
    public string AddressLine1 { get; private set; } = default!;   // Açık adres 1
    public string? AddressLine2 { get; private set; }               // Açık adres 2 (opsiyonel)
    public string? PostalCode { get; private set; }                 // Neighborhood'dan default, override edilebilir

    // ─── Audit (IAuditable — interceptor doldurur) ───────────────────────
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? CreatedById { get; private set; }
    public string? CreatedByName { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public Guid? UpdatedById { get; private set; }
    public string? UpdatedByName { get; private set; }

    // ─── Soft-delete (ISoftDeletable) ────────────────────────────────────
    public DateTimeOffset? DeletedAt { get; private set; }
    public Guid? DeletedById { get; private set; }
    public string? DeletedByName { get; private set; }
    public string? DeleteReason { get; private set; }
    public bool IsDeleted => DeletedAt.HasValue;

    private Address() : base() { }

    private Address(AddressId id, NeighborhoodId neighborhoodId, string addressLine1,
                    string? addressLine2, string? postalCode)
        : base(id)
    {
        NeighborhoodId = neighborhoodId;
        AddressLine1 = addressLine1;
        AddressLine2 = addressLine2;
        PostalCode = postalCode;
    }

    /// <summary>
    /// Yeni adres oluşturur.
    /// </summary>
    /// <param name="neighborhoodId">Bağlı mahalle ID.</param>
    /// <param name="addressLine1">Açık adres 1 (zorunlu, max 500).</param>
    /// <param name="addressLine2">Açık adres 2 (opsiyonel, max 500).</param>
    /// <param name="postalCode">Posta kodu (opsiyonel, 5 rakam). Boş ise mahalleninki kullanılır.</param>
    public static Address Create(
        NeighborhoodId neighborhoodId,
        string addressLine1,
        string? addressLine2 = null,
        string? postalCode = null)
    {
        if (string.IsNullOrWhiteSpace(addressLine1))
            throw new BusinessRuleViolationException("Açık adres 1 zorunludur.");

        if (addressLine1.Length > 500)
            throw new BusinessRuleViolationException("Açık adres 1 en fazla 500 karakter olabilir.");

        if (addressLine2 is not null && addressLine2.Length > 500)
            throw new BusinessRuleViolationException("Açık adres 2 en fazla 500 karakter olabilir.");

        if (!string.IsNullOrWhiteSpace(postalCode))
        {
            postalCode = postalCode.Trim();
            if (postalCode.Length != 5 || !postalCode.All(char.IsDigit))
                throw new BusinessRuleViolationException(
                    "Posta kodu 5 rakam olmalı. Bilinmiyorsa boş bırakın (mahallenin posta kodu kullanılır).");
        }
        else
        {
            postalCode = null;
        }

        return new Address(
            AddressId.New(),
            neighborhoodId,
            addressLine1.Trim(),
            string.IsNullOrWhiteSpace(addressLine2) ? null : addressLine2.Trim(),
            postalCode);
    }

    /// <summary>
    /// Adres bilgilerini günceller. Mahalle değişikliği de yapılabilir (taşınma senaryosu).
    /// </summary>
    public void Update(
        NeighborhoodId neighborhoodId,
        string addressLine1,
        string? addressLine2,
        string? postalCode)
    {
        if (string.IsNullOrWhiteSpace(addressLine1))
            throw new BusinessRuleViolationException("Açık adres 1 zorunludur.");

        if (addressLine1.Length > 500)
            throw new BusinessRuleViolationException("Açık adres 1 en fazla 500 karakter olabilir.");

        if (addressLine2 is not null && addressLine2.Length > 500)
            throw new BusinessRuleViolationException("Açık adres 2 en fazla 500 karakter olabilir.");

        if (!string.IsNullOrWhiteSpace(postalCode))
        {
            postalCode = postalCode.Trim();
            if (postalCode.Length != 5 || !postalCode.All(char.IsDigit))
                throw new BusinessRuleViolationException("Posta kodu 5 rakam olmalı.");
        }
        else
        {
            postalCode = null;
        }

        NeighborhoodId = neighborhoodId;
        AddressLine1 = addressLine1.Trim();
        AddressLine2 = string.IsNullOrWhiteSpace(addressLine2) ? null : addressLine2.Trim();
        PostalCode = postalCode;
    }

    /// <summary>
    /// Soft-delete işaretler. DeletedAt + DeleteReason entity tarafından set edilir;
    /// DeletedById + DeletedByName AuditSaveChangesInterceptor tarafından doldurulur.
    /// </summary>
    public void SoftDelete(string reason)
    {
        if (IsDeleted)
            throw new InvalidStateException("Adres zaten silinmiş.");

        if (string.IsNullOrWhiteSpace(reason))
            throw new BusinessRuleViolationException("Silme sebebi zorunludur.");

        DeletedAt = DateTimeOffset.UtcNow;
        DeleteReason = reason.Trim();
        // DeletedById / DeletedByName → interceptor
    }

    /// <summary>Soft-delete'i geri al.</summary>
    public void Restore()
    {
        if (!IsDeleted)
            throw new InvalidStateException("Adres zaten aktif.");

        DeletedAt = null;
        DeletedById = null;
        DeletedByName = null;
        DeleteReason = null;
    }
}
