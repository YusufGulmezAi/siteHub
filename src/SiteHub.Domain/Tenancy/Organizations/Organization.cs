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
    public string Name { get; private set; } = default!;
    public string CommercialTitle { get; private set; } = default!;
    public NationalId? TaxId { get; private set; }
    public string? Address { get; private set; }
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public bool IsActive { get; private set; }

    // EF Core için parametresiz ctor
    private Organization() : base() { }

    private Organization(OrganizationId id, string name, string commercialTitle, NationalId? taxId)
        : base(id)
    {
        Name = name;
        CommercialTitle = commercialTitle;
        TaxId = taxId;
        IsActive = true;
    }

    // ─── Factory ────────────────────────────────────────────────────────

    public static Organization Create(
        string name,
        string commercialTitle,
        NationalId? taxId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(commercialTitle);

        if (name.Length > 200)
            throw new ArgumentException("Organizasyon adı 200 karakteri aşamaz.", nameof(name));
        if (commercialTitle.Length > 500)
            throw new ArgumentException("Ticari unvan 500 karakteri aşamaz.", nameof(commercialTitle));

        if (taxId is not null && taxId.Type != NationalIdType.VKN)
            throw new ArgumentException("Organizasyon kimlik numarası VKN olmalıdır.", nameof(taxId));

        var org = new Organization(OrganizationId.New(), name.Trim(), commercialTitle.Trim(), taxId);
        org.RecomputeSearchText();
        return org;
    }

    // ─── Davranışlar ────────────────────────────────────────────────────

    public void Rename(string newName, string newCommercialTitle)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newName);
        ArgumentException.ThrowIfNullOrWhiteSpace(newCommercialTitle);

        Name = newName.Trim();
        CommercialTitle = newCommercialTitle.Trim();
        RecomputeSearchText();
    }

    public void UpdateContact(string? address, string? phone, string? email)
    {
        Address = address?.Trim();
        Phone = phone?.Trim();
        Email = email?.Trim();
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
    /// Arama yapılırken: Name, CommercialTitle, TaxId, Phone, Email'de kullanıcı
    /// parça yazabilir — hepsini tek aranabilir alana birleştiriyoruz.
    /// </summary>
    private void RecomputeSearchText()
    {
        UpdateSearchText(
            Name,
            CommercialTitle,
            TaxId?.Value,
            Address,
            Phone,
            Email);
    }
}
