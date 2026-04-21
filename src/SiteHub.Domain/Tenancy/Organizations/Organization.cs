using SiteHub.Domain.Common;
using SiteHub.Domain.Identity;

namespace SiteHub.Domain.Tenancy.Organizations;

/// <summary>
/// Organization (Kiracı / Yönetim Firması) — tenancy hiyerarşisinin TEPESİ.
///
/// İş modelinde:
/// - SiteHub'ın MÜŞTERİSİ — sana ödeme yapan yönetim firması
/// - Birden çok Site'yi yönetir
/// - Kendi personeli var (User × Membership × ContextType=Organization)
/// - Kendi abonelik, faturalama bilgisi ileride
/// - VKN (Vergi Kimlik Numarası) zorunlu olabilir — tüzel kişilik
///
/// TÜRETTİĞİ BASE: SearchableAggregateRoot
/// - Audit alanları (CreatedAt/By, UpdatedAt/By) — interceptor doldurur
/// - Soft-delete (DeletedAt/By/Reason) — SoftDelete(reason) metodu
/// - SearchText — partial search için normalize edilmiş metin
///
/// ÖNEMLI: State değiştiren her metotta UpdateSearchText() çağrılmalı
/// (Create, Rename, UpdateContact). Yoksa arama eski veri gösterir.
/// </summary>
public sealed class Organization : SearchableAggregateRoot<OrganizationId>
{
    // ─── Properties ──────────────────────────────────────────────────────

    /// <summary>
    /// 6 haneli insan-okunabilir kod (100001-999999).
    /// Feistel cipher ile sequence'ten üretilir — tahmin edilemez, unique.
    /// UI'da "Firma #123456 — ABC Yönetim" şeklinde gösterilir.
    /// </summary>
    public long Code { get; private set; }

    public string Name { get; private set; } = default!;
    public string CommercialTitle { get; private set; } = default!;
    public NationalId TaxId { get; private set; } = default!;
    public string? Address { get; private set; }
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public bool IsActive { get; private set; }

    // EF Core için parametresiz ctor
    private Organization() : base() { }

    private Organization(OrganizationId id, long code, string name, string commercialTitle, NationalId taxId)
        : base(id)
    {
        Code = code;
        Name = name;
        CommercialTitle = commercialTitle;
        TaxId = taxId;
        IsActive = true;
    }

    // ─── Factory ────────────────────────────────────────────────────────

    /// <summary>
    /// Yeni organizasyon oluşturur. VKN zorunlu (Faz E kararı).
    /// </summary>
    /// <param name="code">6 haneli Feistel kod — <see cref="ICodeGenerator"/>'dan alınır.</param>
    /// <param name="name">Kısa ad (listeler, başlıklar).</param>
    /// <param name="commercialTitle">Resmi ticari unvan (fatura, belgeler).</param>
    /// <param name="taxId">VKN (zorunlu — ADR-0012 tüzel kişi).</param>
    public static Organization Create(
        long code,
        string name,
        string commercialTitle,
        NationalId taxId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(commercialTitle);
        ArgumentNullException.ThrowIfNull(taxId);

        if (code < 100_001 || code > 999_999)
            throw new ArgumentOutOfRangeException(nameof(code),
                "Organizasyon kodu 6 haneli olmalıdır (100001-999999).");

        if (name.Length > 200)
            throw new ArgumentException("Organizasyon adı 200 karakteri aşamaz.", nameof(name));
        if (commercialTitle.Length > 500)
            throw new ArgumentException("Ticari unvan 500 karakteri aşamaz.", nameof(commercialTitle));

        if (taxId.Type != NationalIdType.VKN)
            throw new ArgumentException("Organizasyon kimlik numarası VKN olmalıdır.", nameof(taxId));

        var org = new Organization(OrganizationId.New(), code, name.Trim(), commercialTitle.Trim(), taxId);
        org.RecomputeSearchText();
        return org;
    }

    // ─── Davranışlar ────────────────────────────────────────────────────

    public void Rename(string newName, string newCommercialTitle)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newName);
        ArgumentException.ThrowIfNullOrWhiteSpace(newCommercialTitle);

        if (newName.Length > 200)
            throw new ArgumentException("Organizasyon adı 200 karakteri aşamaz.", nameof(newName));
        if (newCommercialTitle.Length > 500)
            throw new ArgumentException("Ticari unvan 500 karakteri aşamaz.", nameof(newCommercialTitle));

        Name = newName.Trim();
        CommercialTitle = newCommercialTitle.Trim();
        RecomputeSearchText();
    }

    public void UpdateContact(string? address, string? phone, string? email)
    {
        if (address is not null && address.Length > 1000)
            throw new ArgumentException("Adres 1000 karakteri aşamaz.", nameof(address));
        if (phone is not null && phone.Length > 30)
            throw new ArgumentException("Telefon 30 karakteri aşamaz.", nameof(phone));
        if (email is not null && email.Length > 320)
            throw new ArgumentException("E-posta 320 karakteri aşamaz.", nameof(email));

        Address = string.IsNullOrWhiteSpace(address) ? null : address.Trim();
        Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        RecomputeSearchText();
    }

    public void ChangeTaxId(NationalId newTaxId)
    {
        ArgumentNullException.ThrowIfNull(newTaxId);
        if (newTaxId.Type != NationalIdType.VKN)
            throw new ArgumentException("VKN olmalıdır.", nameof(newTaxId));

        TaxId = newTaxId;
        RecomputeSearchText();
    }

    public void Deactivate()
    {
        if (!IsActive) return;
        IsActive = false;
    }

    public void Activate()
    {
        if (IsActive) return;
        IsActive = true;
    }

    /// <summary>
    /// Aranabilir metni yeniden hesapla. Her "görünür alan" değişikliğinde çağrılır.
    /// Arama yapılırken: Code, Name, CommercialTitle, TaxId, Phone, Email'de kullanıcı
    /// parça yazabilir — hepsini tek aranabilir alana birleştiriyoruz.
    /// </summary>
    private void RecomputeSearchText()
    {
        UpdateSearchText(
            Code.ToString(),
            Name,
            CommercialTitle,
            TaxId.Value,
            Address,
            Phone,
            Email);
    }
}
