# Faz F-1 Planı — Site Domain Entity

**Durum:** Onaylı, implementasyona hazır
**Oluşturma:** 2026-04-22
**Tahmini süre:** 45-60 dakika
**Önkoşul:** Faz E-Pre Gün 2 bitmiş (RLS altyapısı hazır)

---

## 1. Hedef

`Site` aggregate root yaratmak — Organization altında apartman/site/kompleks birimini temsil eder.
Domain invariant'larıyla birlikte, unit test'li. **Bu adımda sadece Domain katmanı** — Infrastructure
(EF config, migration) ve Application (CRUD commands) sonraki alt parçalar.

## 2. Kapsam

### İçerir (F.1)
- `Site` aggregate root (Domain projesi)
- `SiteId` strongly-typed ID
- `Site.Create()` factory + diğer factory'ler gerekirse
- Mutasyon metodları (Rename, ChangeAddress, SetIban, Activate, Deactivate, SoftDelete)
- Invariant'lar (iş kuralları enforcement)
- `SearchText` normalization (Turkish-aware)
- Unit testler (~20-30 test)

### İçermez (F.2+ sonraki adımlar)
- EF Core `SiteConfiguration.cs` — F.2
- `tenancy.sites` migration — F.2
- CRUD command/query handler'ları — F.3
- `SiteEndpoints.cs` — F.3
- `HttpTenantContext` Site → Org resolver — F.4 (A.4.b entegre)
- RLS policy — F.5
- UI (MudBlazor) — F.6

## 3. Entity Tasarımı

### 3.1 Konum
`src/SiteHub.Domain/Tenancy/Sites/Site.cs`  
`src/SiteHub.Domain/Tenancy/Sites/SiteId.cs`

### 3.2 Property'ler

```csharp
public sealed class Site : AuditableAggregateRoot<SiteId>
{
    // Tenant ilişkisi
    public OrganizationId OrganizationId { get; private set; }

    // Kod
    public long Code { get; private set; }           // 6 hane, 100001-999999 (Organization ile aynı aralık, ayrı unique)

    // Kimlik
    public string Name { get; private set; }         // "Yıldız Sitesi", max 200
    public string? CommercialTitle { get; private set; }  // "Yıldız Sitesi Yönetim Koop.", max 500, opsiyonel

    // Adres (Hibrit: serbest metin + geography FK)
    public string Address { get; private set; }      // Serbest metin, max 1000
    public ProvinceId ProvinceId { get; private set; }   // geography.provinces, zorunlu
    public DistrictId? DistrictId { get; private set; }  // geography.districts, opsiyonel

    // Finans
    public string? Iban { get; private set; }        // TR + 24 hane, factory'de validate, opsiyonel
    public NationalId? TaxId { get; private set; }   // VKN only, opsiyonel

    // Durum
    public bool IsActive { get; private set; }

    // Arama
    public string SearchText { get; private set; }   // RecomputeSearchText() ile güncellenir
}
```

### 3.3 Factory

```csharp
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
    // Validation
    ArgumentNullException.ThrowIfNull(organizationId);
    ArgumentException.ThrowIfNullOrWhiteSpace(name);
    ArgumentException.ThrowIfNullOrWhiteSpace(address);
    ArgumentNullException.ThrowIfNull(provinceId);

    // Code aralığı
    if (code < 100_001 || code > 999_999)
        throw new ArgumentOutOfRangeException(nameof(code),
            "Site kodu 6 haneli olmalıdır (100001-999999).");

    // String limitleri
    if (name.Length > 200)
        throw new ArgumentException("Site adı 200 karakteri aşamaz.", nameof(name));
    if (commercialTitle is not null && commercialTitle.Length > 500)
        throw new ArgumentException("Ticari unvan 500 karakteri aşamaz.", nameof(commercialTitle));
    if (address.Length > 1000)
        throw new ArgumentException("Adres 1000 karakteri aşamaz.", nameof(address));

    // IBAN validation (eğer set edilmişse)
    if (iban is not null && !IsValidIban(iban))
        throw new ArgumentException("Geçerli bir TR IBAN olmalıdır.", nameof(iban));

    // TaxId VKN olmalı
    if (taxId is not null && taxId.Type != NationalIdType.VKN)
        throw new ArgumentException("Site vergi numarası VKN olmalıdır.", nameof(taxId));

    var site = new Site(
        SiteId.New(), code, organizationId, name.Trim(),
        commercialTitle?.Trim(), address.Trim(),
        provinceId, districtId, iban, taxId);
    site.RecomputeSearchText();
    return site;
}
```

### 3.4 Mutasyonlar

```csharp
public void Rename(string newName, string? newCommercialTitle);
public void ChangeAddress(string newAddress, ProvinceId provinceId, DistrictId? districtId);
public void SetIban(string iban);        // IBAN validation
public void ClearIban(string reason);    // Neden opsiyonel, audit için
public void SetTaxId(NationalId taxId);  // VKN kontrolü
public void ClearTaxId();
public void Activate();                  // IsActive=true
public void Deactivate(string reason);   // IsActive=false, reason audit
public void SoftDelete(Guid? userId, string? userName, string reason);
```

### 3.5 SearchText Normalization

Organization'daki pattern'i takip:
- `name`, `commercialTitle`, `code`, `taxId?.Value`, `iban?`, `address` alanlarını **Turkish-normalize** et
- Lowercase, diakritik temizle (ı/i, ş/s, ğ/g, ö/o, ü/u, ç/c)
- Space ile birleştir
- `TurkishTextNormalizer` (Shared'de var) kullan

### 3.6 IsValidIban Helper

```csharp
private static bool IsValidIban(string iban)
{
    // TR + 24 digit + checksum (IBAN modulo 97 = 1)
    // MVP: TR + 24 karakter uzunluk + rakam kontrolü
    // İleride: tam checksum (TUBITAK standart)
    if (string.IsNullOrEmpty(iban)) return false;
    iban = iban.Replace(" ", "").ToUpperInvariant();
    if (iban.Length != 26) return false;
    if (!iban.StartsWith("TR")) return false;
    return iban[2..].All(char.IsDigit);
    // NOT: Tam checksum validation Faz H'de (IbanValue object'e refactor ederken)
}
```

## 4. İş Kuralları (Invariant'lar)

1. **Parent Organization zorunlu** — OrganizationId null olamaz (factory kontrol eder)
2. **Code 6 haneli** — 100001-999999
3. **Name zorunlu** — whitespace olamaz, max 200
4. **Address zorunlu** — whitespace olamaz, max 1000
5. **ProvinceId zorunlu** — null olamaz
6. **IBAN geçerli TR IBAN olmalı** (eğer set edilmişse)
7. **TaxId sadece VKN olabilir** — TCKN/YKN geçmez
8. **DeletedAt set edildikten sonra** — mutation metotları throw eder (base class'tan)
9. **IsActive=false iken** — Rename/ChangeAddress geçer ama Activate çağrılmadıkça işlem flow'u block olmalı (ileride business layer'da)

## 5. Unit Test Planı

### Konum
`tests/SiteHub.Domain.Tests/Tenancy/Sites/SiteTests.cs`

### Test grupları

**Grup 1: Create (valid/invalid inputs)**
- `Create_WithMinimalValidInputs_Succeeds`
- `Create_WithAllOptionalFields_Succeeds`
- `Create_WithCodeBelowRange_ThrowsArgumentOutOfRange`
- `Create_WithCodeAboveRange_ThrowsArgumentOutOfRange`
- `Create_WithNullOrganizationId_ThrowsArgumentNull`
- `Create_WithEmptyName_ThrowsArgument`
- `Create_WithTooLongName_ThrowsArgument`
- `Create_WithEmptyAddress_ThrowsArgument`
- `Create_WithNullProvinceId_ThrowsArgumentNull`
- `Create_WithInvalidIban_ThrowsArgument`
- `Create_WithTcknAsTaxId_ThrowsArgument`
- `Create_TrimsNameAndAddress`
- `Create_SetsIsActiveTrue`
- `Create_ComputesSearchText`

**Grup 2: Rename**
- `Rename_UpdatesNameAndCommercialTitle`
- `Rename_RecomputesSearchText`
- `Rename_WithEmptyName_Throws`

**Grup 3: ChangeAddress**
- `ChangeAddress_UpdatesAllFields`
- `ChangeAddress_RecomputesSearchText`

**Grup 4: IBAN**
- `SetIban_ValidTrIban_Updates`
- `SetIban_InvalidIban_Throws`
- `ClearIban_RequiresReason`

**Grup 5: Durum**
- `Deactivate_SetsIsActiveFalse`
- `Activate_SetsIsActiveTrue`
- `SoftDelete_SetsDeletedFields`

**Hedef:** ~25 test

## 6. Yardımcı Bilgiler

### 6.1 Mevcut Pattern Referansları
- `Organization.cs` (neredeyse aynı pattern) — `src/SiteHub.Domain/Tenancy/Organizations/`
- `OrganizationTests.cs` — `tests/SiteHub.Domain.Tests/Tenancy/Organizations/`

### 6.2 Yeni Strongly-Typed ID'ler (muhtemelen bazıları yok)
- `SiteId` — yazılacak
- `ProvinceId` — muhtemelen var (geography.Provinces entity)
- `DistrictId` — muhtemelen var (geography.Districts entity)

Kontrol gerek. Yoksa eklenecek.

### 6.3 Audit Base Class
- `AuditableAggregateRoot<TId>` — otomatik `CreatedAt`, `UpdatedAt`, `DeletedAt` handling
- Organization da kullanıyor, aynı yol

## 7. Adımlama (Uygulama Sırası)

1. `ProvinceId`/`DistrictId` strongly-typed ID'leri mevcut mu kontrol (yoksa ekle, ama geography varsa kesin vardır)
2. `SiteId.cs` yaz
3. `Site.cs` yaz (factory + mutations + SearchText)
4. `IsValidIban` helper (`Site` içinde private static)
5. `SiteTests.cs` yaz (25 test)
6. `dotnet test` — hepsi yeşil
7. Commit + push

## 8. Commit Mesajı Taslağı

```
Faz F.1: Site domain entity + factory + unit tests

Organization altinda apartman/site/kompleks birimi.

Domain:
- SiteId strongly-typed ID
- Site aggregate root (AuditableAggregateRoot'tan turuyor)
- Factory: Site.Create(code, orgId, name, provinceId, address, ...)
- Mutations: Rename, ChangeAddress, SetIban/ClearIban, SetTaxId/ClearTaxId,
  Activate, Deactivate, SoftDelete
- Invariant'lar: parent org zorunlu, code 100001-999999, VKN-only taxId,
  TR IBAN format (tam checksum Faz H'de IbanValue object ile gelecek)
- SearchText Turkish normalization

Adres stratejisi (ADR-tbd veya plan'da): Hibrit
- Address: serbest metin (zorunlu)
- ProvinceId: geography FK zorunlu
- DistrictId: opsiyonel

Code aralığı: Organization ile aynı (100001-999999), ayrı unique.

Tests: ~25 unit test (domain invariant'lar + mutations).

Sonraki adim: F.2 (EF config + migration)
```

---

## 9. Açık Karar (İleride)

### Code Üretimi
Organization'da `CreateOrganizationCommand` Feistel cipher ile code üretiyor.
Site code'u da benzer olmalı. **Detay F.3'te** — CRUD command yazılırken karar.

### Site Transfer (Faz K+)
Bir site'nin başka Organization'a devri bir **business event**. Şimdi kapsam dışı.
Gerçekleştiğinde yeni `Site` satırı + eskisi `SoftDelete` + audit event olacak.
Ayrı ADR konusu, Faz K (Finans transfer) sonrasında gündeme gelir.
