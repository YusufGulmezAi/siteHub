using SiteHub.Domain.Common;
using SiteHub.Domain.Geography;

namespace SiteHub.Domain.Identity;

/// <summary>Person için strongly-typed ID.</summary>
public readonly record struct PersonId(Guid Value) : ITypedId<PersonId>
{
    public static PersonId New() => new(Guid.CreateVersion7());
    public static PersonId FromGuid(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

/// <summary>
/// Kişi tipi: gerçek kişi (TCKN/YKN) veya tüzel kişi (VKN).
/// </summary>
public enum PersonType
{
    /// <summary>Gerçek kişi — TCKN veya YKN sahibi.</summary>
    Individual = 1,

    /// <summary>Tüzel kişi — VKN sahibi (şirket, dernek, vakıf vb.).</summary>
    Corporate = 2
}

/// <summary>
/// Sistemdeki her gerçek veya tüzel kişi kaydı (ADR-0011 §1).
///
/// Person, "bu insanın kimliği" bilgisidir — giriş yapıp yapmadığı bağlamsızdır.
/// Giriş yapabilmek için Person'a bir LoginAccount bağlanır (1:0..1).
///
/// KULLANIM ALANLARI:
/// - Organization personeli (User)
/// - Site personeli (yönetici, güvenlik, bahçıvan)
/// - Hissedar (Shareholder)
/// - Kiracı (Tenant)
/// - Yönetim kurulu üyesi
///
/// SİLME POLİTİKASI:
/// Person FİZİKSEL silinmez. IsActive=false ile pasifleştirilir. Sebep:
/// - Audit log'daki denormalize user_name'i anlamsız hale getirir
/// - Hissedar/kiracı tarihçesini bozar
/// - KVKK "silme hakkı" için v2'de özel "pseudonymize" operasyonu olacak
///
/// NationalId sistem geneli UNIQUE — aynı TCKN/VKN ile iki Person yaratılamaz.
/// </summary>
public sealed class Person : SearchableAggregateRoot<PersonId>
{
    public NationalId NationalId { get; private set; } = default!;
    public PersonType PersonType { get; private set; }
    public string FullName { get; private set; } = default!;
    public string MobilePhone { get; private set; } = default!;   // E.164 (+905xxxxxxxxx)
    public string? Email { get; private set; }                    // İletişim — login email'den farklı
    public string? KepAddress { get; private set; }               // Kayıtlı Elektronik Posta (tüzel için önerilir)
    public string? ProfilePhotoUrl { get; private set; }
    public AddressId? NotificationAddressId { get; private set; } // Tebligat adresi (opsiyonel)
    public bool IsActive { get; private set; }

    private Person() : base() { }

    private Person(
        PersonId id,
        NationalId nationalId,
        PersonType personType,
        string fullName,
        string mobilePhone,
        string? email,
        string? kepAddress,
        AddressId? notificationAddressId)
        : base(id)
    {
        NationalId = nationalId;
        PersonType = personType;
        FullName = fullName;
        MobilePhone = mobilePhone;
        Email = email;
        KepAddress = kepAddress;
        NotificationAddressId = notificationAddressId;
        IsActive = true;
        RefreshSearchText();
    }

    // ─── Factory ─────────────────────────────────────────────────────────

    public static Person Create(
        NationalId nationalId,
        string fullName,
        string mobilePhone,
        string? email = null,
        string? kepAddress = null,
        AddressId? notificationAddressId = null)
    {
        ValidateFullName(fullName);
        ValidateMobilePhone(mobilePhone);
        ValidateEmailIfProvided(email);
        ValidateKepAddressIfProvided(kepAddress);

        // NationalId tipinden PersonType türetilir
        var personType = nationalId.Type switch
        {
            NationalIdType.TCKN => PersonType.Individual,
            NationalIdType.YKN  => PersonType.Individual,
            NationalIdType.VKN  => PersonType.Corporate,
            _ => throw new BusinessRuleViolationException(
                    $"Desteklenmeyen NationalId tipi: {nationalId.Type}")
        };

        return new Person(
            PersonId.New(),
            nationalId,
            personType,
            fullName.Trim(),
            mobilePhone.Trim(),
            email?.Trim(),
            kepAddress?.Trim(),
            notificationAddressId);
    }

    // ─── Mutations ───────────────────────────────────────────────────────

    public void Rename(string newFullName)
    {
        ValidateFullName(newFullName);
        FullName = newFullName.Trim();
        RefreshSearchText();
    }

    public void UpdateContact(string mobilePhone, string? email, string? kepAddress)
    {
        ValidateMobilePhone(mobilePhone);
        ValidateEmailIfProvided(email);
        ValidateKepAddressIfProvided(kepAddress);

        MobilePhone = mobilePhone.Trim();
        Email = email?.Trim();
        KepAddress = kepAddress?.Trim();
        RefreshSearchText();
    }

    public void SetNotificationAddress(AddressId? addressId) => NotificationAddressId = addressId;

    public void SetProfilePhoto(string? url) => ProfilePhotoUrl = url;

    public void Activate()
    {
        if (IsActive) return;
        IsActive = true;
    }

    public void Deactivate()
    {
        if (!IsActive) return;
        IsActive = false;
    }

    // ─── Validation ──────────────────────────────────────────────────────

    private static void ValidateFullName(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            throw new BusinessRuleViolationException("Ad-soyad (ya da unvan) zorunludur.");
        if (fullName.Length > 300)
            throw new BusinessRuleViolationException("Ad-soyad en fazla 300 karakter olabilir.");
    }

    private static void ValidateMobilePhone(string mobilePhone)
    {
        if (string.IsNullOrWhiteSpace(mobilePhone))
            throw new BusinessRuleViolationException("Cep telefonu zorunludur.");

        var trimmed = mobilePhone.Trim();
        // E.164 gevşek kontrol: +90... ile başlar, toplam 13 karakter (+90 + 10 hane)
        // Detaylı validasyon FluentValidation katmanında
        if (trimmed.Length > 20)
            throw new BusinessRuleViolationException("Cep telefonu en fazla 20 karakter olabilir.");
    }

    private static void ValidateEmailIfProvided(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return;
        if (email.Length > 320)
            throw new BusinessRuleViolationException("E-posta en fazla 320 karakter olabilir.");
        if (!email.Contains('@'))
            throw new BusinessRuleViolationException("Geçerli e-posta formatı gerekli.");
    }

    private static void ValidateKepAddressIfProvided(string? kep)
    {
        if (string.IsNullOrWhiteSpace(kep)) return;
        if (kep.Length > 320)
            throw new BusinessRuleViolationException("KEP adresi en fazla 320 karakter olabilir.");
        if (!kep.Contains('@'))
            throw new BusinessRuleViolationException("Geçerli KEP formatı gerekli.");
    }

    // ─── SearchableAggregateRoot search text üretimi ─────────────────────
    // UpdateSearchText(params string?[]) base class'ta tanımlı. Buradan çağırırız.

    private void RefreshSearchText() =>
        UpdateSearchText(
            FullName,
            NationalId?.Value,
            MobilePhone,
            Email,
            KepAddress);
}
