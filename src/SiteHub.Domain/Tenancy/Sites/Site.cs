using SiteHub.Domain.Common;
using SiteHub.Domain.Geography;
using SiteHub.Domain.Identity;
using SiteHub.Domain.Tenancy.Organizations;

namespace SiteHub.Domain.Tenancy.Sites;

/// <summary>
/// Site (Apartman / Kompleks / Yönetilen Bina Topluluğu) — tenancy hiyerarşisinin
/// İKİNCİ katmanı. Organization altında yer alır.
///
/// İş modelinde:
/// - Bir Organization (yönetim firması) birden çok Site yönetir
/// - Her Site bağımsız finansal yapıya sahip olabilir (kendi IBAN'ı, kendi tüzel kişiliği)
/// - Site içinde Unit'ler (daireler/bürolar) ve Residency'ler (oturan kişiler) yer alır
///   (Faz G'de eklenir)
///
/// ADRES STRATEJİSİ (hibrit):
/// - Address: serbest metin (1000 karakter, Mahalle/Sokak/No vb. — Türkiye standardı değişken)
/// - ProvinceId: geography.provinces FK ZORUNLU (il bazlı raporlama için)
/// - DistrictId: geography.districts FK opsiyonel
///
/// TÜRETTİĞİ BASE: SearchableAggregateRoot
/// - Audit alanları (CreatedAt/By, UpdatedAt/By) — interceptor doldurur
/// - Soft-delete (DeletedAt/By/Reason) — SoftDelete(reason, now) metodu base'den gelir
/// - SearchText — partial search için normalize edilmiş metin
///
/// CODE ARALIĞI: 100001-999999 (Organization ile aynı aralık, AYRI unique).
/// Kullanıcı "Firma 100001" ve "Site 100001" ayrı entity görür, URL path'lerinde bağlam belli.
///
/// ÖNEMLI: State değiştiren her metotta RecomputeSearchText() çağrılmalı.
/// </summary>
public sealed class Site : SearchableAggregateRoot<SiteId>
{
    // ─── Properties ──────────────────────────────────────────────────────

    /// <summary>Parent Organization (yönetim firması) — ZORUNLU.</summary>
    public OrganizationId OrganizationId { get; private set; }

    /// <summary>
    /// 6 haneli insan-okunabilir kod (100001-999999).
    /// Organization ile aynı aralık, ayrı unique. Feistel cipher ile sequence'ten üretilir.
    /// </summary>
    public long Code { get; private set; }

    /// <summary>Kısa ad — "Yıldız Sitesi" (max 200).</summary>
    public string Name { get; private set; } = default!;

    /// <summary>Resmi unvan — "Yıldız Sitesi Yönetim Kooperatifi" (max 500, opsiyonel).</summary>
    public string? CommercialTitle { get; private set; }

    /// <summary>Serbest metin adres — Mahalle/Sokak/No (max 1000, ZORUNLU).</summary>
    public string Address { get; private set; } = default!;

    /// <summary>İl (geography.provinces FK) — ZORUNLU.</summary>
    public ProvinceId ProvinceId { get; private set; }

    /// <summary>İlçe (geography.districts FK) — opsiyonel.</summary>
    public DistrictId? DistrictId { get; private set; }

    /// <summary>Site'nin kendi tahsilat IBAN'ı (TR + 24 hane, opsiyonel).</summary>
    public string? Iban { get; private set; }

    /// <summary>Site'nin kendi VKN'si (tüzel kişiliği varsa, opsiyonel).</summary>
    public NationalId? TaxId { get; private set; }

    public bool IsActive { get; private set; }

    // EF Core için parametresiz ctor
    private Site() : base() { }

    private Site(
        SiteId id,
        long code,
        OrganizationId organizationId,
        string name,
        string? commercialTitle,
        string address,
        ProvinceId provinceId,
        DistrictId? districtId,
        string? iban,
        NationalId? taxId)
        : base(id)
    {
        Code = code;
        OrganizationId = organizationId;
        Name = name;
        CommercialTitle = commercialTitle;
        Address = address;
        ProvinceId = provinceId;
        DistrictId = districtId;
        Iban = iban;
        TaxId = taxId;
        IsActive = true;
    }

    // ─── Factory ────────────────────────────────────────────────────────

    /// <summary>
    /// Yeni Site oluşturur. Parent Organization zorunlu. VKN ve IBAN opsiyonel
    /// ama set edildiyse formatı geçerli olmalı.
    /// </summary>
    public static Site Create(
        long code,
        OrganizationId organizationId,
        string name,
        ProvinceId provinceId,
        string address,
        string? commercialTitle = null,
        DistrictId? districtId = null,
        string? iban = null,
        NationalId? taxId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(address);

        if (organizationId == default)
            throw new ArgumentException("Parent Organization ZORUNLU.", nameof(organizationId));
        if (provinceId == default)
            throw new ArgumentException("İl (ProvinceId) ZORUNLU.", nameof(provinceId));

        if (code < 100_001 || code > 999_999)
            throw new ArgumentOutOfRangeException(nameof(code),
                "Site kodu 6 haneli olmalıdır (100001-999999).");

        if (name.Length > 200)
            throw new ArgumentException("Site adı 200 karakteri aşamaz.", nameof(name));
        if (commercialTitle is not null && commercialTitle.Length > 500)
            throw new ArgumentException("Ticari unvan 500 karakteri aşamaz.", nameof(commercialTitle));
        if (address.Length > 1000)
            throw new ArgumentException("Adres 1000 karakteri aşamaz.", nameof(address));

        // IBAN validation (eğer set edilmişse)
        var normalizedIban = NormalizeIban(iban);
        if (normalizedIban is not null && !IsValidIban(normalizedIban))
            throw new ArgumentException("Geçerli bir TR IBAN olmalıdır (TR + 24 rakam).", nameof(iban));

        // TaxId VKN olmalı
        if (taxId is not null && taxId.Type != NationalIdType.VKN)
            throw new ArgumentException("Site vergi numarası VKN olmalıdır.", nameof(taxId));

        var commercialTitleTrimmed = string.IsNullOrWhiteSpace(commercialTitle) ? null : commercialTitle.Trim();

        var site = new Site(
            SiteId.New(),
            code,
            organizationId,
            name.Trim(),
            commercialTitleTrimmed,
            address.Trim(),
            provinceId,
            districtId,
            normalizedIban,
            taxId);
        site.RecomputeSearchText();
        return site;
    }

    // ─── Davranışlar ────────────────────────────────────────────────────

    public void Rename(string newName, string? newCommercialTitle)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newName);
        if (newName.Length > 200)
            throw new ArgumentException("Site adı 200 karakteri aşamaz.", nameof(newName));
        if (newCommercialTitle is not null && newCommercialTitle.Length > 500)
            throw new ArgumentException("Ticari unvan 500 karakteri aşamaz.", nameof(newCommercialTitle));

        Name = newName.Trim();
        CommercialTitle = string.IsNullOrWhiteSpace(newCommercialTitle) ? null : newCommercialTitle.Trim();
        RecomputeSearchText();
    }

    public void ChangeAddress(string newAddress, ProvinceId provinceId, DistrictId? districtId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newAddress);
        if (newAddress.Length > 1000)
            throw new ArgumentException("Adres 1000 karakteri aşamaz.", nameof(newAddress));
        if (provinceId == default)
            throw new ArgumentException("İl (ProvinceId) ZORUNLU.", nameof(provinceId));

        Address = newAddress.Trim();
        ProvinceId = provinceId;
        DistrictId = districtId;
        RecomputeSearchText();
    }

    public void SetIban(string iban)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(iban);
        var normalized = NormalizeIban(iban);
        if (normalized is null || !IsValidIban(normalized))
            throw new ArgumentException("Geçerli bir TR IBAN olmalıdır (TR + 24 rakam).", nameof(iban));

        Iban = normalized;
        RecomputeSearchText();
    }

    /// <summary>
    /// IBAN'ı temizler. <paramref name="reason"/> audit için ZORUNLU.
    /// </summary>
    public void ClearIban(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        // Audit: interceptor mevcut durumu snapshot'lar (domain event gerekmez)
        Iban = null;
        RecomputeSearchText();
    }

    public void SetTaxId(NationalId taxId)
    {
        ArgumentNullException.ThrowIfNull(taxId);
        if (taxId.Type != NationalIdType.VKN)
            throw new ArgumentException("Site vergi numarası VKN olmalıdır.", nameof(taxId));

        TaxId = taxId;
        RecomputeSearchText();
    }

    public void ClearTaxId()
    {
        TaxId = null;
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

    // ─── Helpers ────────────────────────────────────────────────────────

    /// <summary>
    /// IBAN'ı normalize eder: boşlukları kaldırır, büyük harfe çevirir.
    /// null/whitespace girdi → null döner.
    /// </summary>
    private static string? NormalizeIban(string? iban)
    {
        if (string.IsNullOrWhiteSpace(iban)) return null;
        return iban.Replace(" ", "").ToUpperInvariant();
    }

    /// <summary>
    /// TR IBAN formatı kontrolü: "TR" + 24 rakam = 26 karakter.
    /// MVP seviyesi — TÜBİTAK tam checksum Faz H'de (IbanValue object refactor).
    /// </summary>
    private static bool IsValidIban(string normalizedIban)
    {
        if (string.IsNullOrEmpty(normalizedIban)) return false;
        if (normalizedIban.Length != 26) return false;
        if (!normalizedIban.StartsWith("TR", StringComparison.Ordinal)) return false;
        return normalizedIban.AsSpan(2).ToString().All(char.IsDigit);
    }

    /// <summary>
    /// SearchText'i yeniden hesapla. Her görünür alan değişikliğinde çağrılır.
    /// Arama yapılırken: Code, Name, CommercialTitle, Address, TaxId, IBAN'da
    /// kullanıcı parça yazabilir — hepsini tek aranabilir alana birleştiriyoruz.
    /// </summary>
    private void RecomputeSearchText()
    {
        UpdateSearchText(
            Code.ToString(),
            Name,
            CommercialTitle,
            Address,
            TaxId?.Value,
            Iban);
    }
}
