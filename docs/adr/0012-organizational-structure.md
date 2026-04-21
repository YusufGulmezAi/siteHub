# ADR-0012: Organizasyonel Yapı ve Yönetilen Varlıklar

**Durum:** Taslak (onay bekliyor)
**Tarih:** 2026-04-19
**İlgili:** ADR-0005 (Bağlam geçişi), ADR-0010 (Arama), ADR-0011 (Kimlik), ADR-0013 (Onay zinciri)

## Bağlam

SiteHub'da tenancy hiyerarşisi ve yönetilen varlıkların yapısı bu ADR'da tanımlanır. Bu ADR 13 aggregate'i kapsar — iş modelinin temeli.

## Karar Özeti

| Entity | Kapsam | Kod / ID | Not |
|---|---|---|---|
| **Organization** | Yönetim + Servis (tek tablo) | 6 hane (Feistel) | SiteHub müşterisi veya servis sağlayıcı; `SystemSuspended` bayrağı servis firması askıya almak için |
| **Branch** | Organizasyonun şubesi | GUID | "İstanbul Ofisi" vb. |
| **BankCustomerProfile** | Org/Site'nin banka müşteri kaydı | GUID | İki seviyeli hiyerarşi; onay zinciri YOK |
| **BankAccountLine** | Profile altındaki alt hesap | GUID | IBAN doğrulama + otomatik banka tespiti |
| **AccountType** | Hesap tipi seed (11 + eklenebilir) | GUID | SystemAdmin genişletir |
| **Site** | Yönetilen site/apartman | 6 hane (Feistel) | VKN zorunlu; yönetim transferi v2; proje/inşaat firmaları, kurumsal tahsilat, ekstre metodları |
| **Plot (Ada)** | Sitenin ada kaydı | GUID | Hesaplananlar v2 |
| **Parcel (Parsel)** | Ada altındaki parsel | GUID | Hesaplananlar v2 |
| **Building (Yapı/Bina)** | Parsel üstündeki bina | GUID | `BuildingVariant` eklendi (C2 tipi); `(parcel, name)` unique |
| **Unit (BB)** | Bağımsız bölüm | 7 hane (Feistel) | `NetAreaM2` eklendi; `(building, entrance, number)` unique |
| **UnitPeriod (Dönem)** | BB'nin zamansal kesiti | 9 hane (Feistel) | Banka tahsilat referansı |
| **Shareholder (Hissedar)** | Dönemdeki malik payı | GUID | Hisse oranı 0.00001-100.00; otomatik LoginAccount; para iade IBAN opsiyonel (onaylı) |
| **Tenant (Kiracı)** | Dönemdeki kiracı | GUID | Tek kiracı kuralı; otomatik LoginAccount; para iade IBAN opsiyonel (onaylı) |
| **ServiceContract** | Site ↔ Servis Org sözleşmesi | GUID | `AccessEnabled` anahtar; 3-seviyeli engelleme |
| **ServiceOrganizationBlock** | Yönetim org'un servis firmasını bloklaması | GUID | §9.7 |
| **DocumentLibrary** | Belgeler (polimorfik owner + dönem bazlı filtreleme) | GUID | Her entity'ye takılabilir; IsPermanent kalıcı BB belgesi, IsSensitive hassas; soft-delete only (hard delete sadece SystemAdmin) |

**Kod üretim stratejisi (ortak):** Sequence + Feistel Cipher obfuscation — deterministic, **all-time unique** (matematiksel bijection garanti), retry/DB query gerekmez. Detay: §11.

---

## 1. Organization

### 1.1. Kavram

İki farklı rol oynar:
- **Yönetim Organizasyonu** (Management): SiteHub müşterisi. Siteleri yönetir.
- **Servis Organizasyonu** (Service): Sayaç okuma, mali müşavir, avukat, güvenlik firması. Yönetim organizasyonlarıyla sözleşme imzalar, onların sitelerine hizmet verir.

**Aynı tabloda**, `OrganizationType` enum'u ile ayrılır.

### 1.2. Alanlar

| Alan | Tip | Zorunlu | Not |
|---|---|---|---|
| Id | Guid (v7) | ✓ | |
| Code | int | ✓ | 6 hane, 100001-999999, Feistel (§11), all-time unique |
| OrganizationType | enum | ✓ | Management, Service |
| CommercialTitle | string(500) | ✓ | Ticari ünvan |
| ShortTitle | string(100) | ✓ | Kısa ünvan |
| Slug | string(200) | ✓ | Sistem üretir, unique |
| NationalId | VO | ✓ | VKN/TCKN/YKN |
| TaxOffice | string(200) | ✗ | Vergi dairesi |
| NotificationAddressId | FK → addresses | ✓ | Bölge + İl + İlçe + Mahalle + Açık Adres 1 (2 opsiyonel) |
| Phone | string(20) | ✗ | E.164 format |
| Email | string(320) | ✓ | |
| KepAddress | string(320) | ✗ | |
| WebsiteUrl | string(500) | ✗ | |
| LogoUrl | string | ✗ | MinIO |
| TradeRegistryNo | string(50) | ✗ | Ticaret sicil no |
| MersisNo | string(20) | ✗ | MERSIS numarası |
| IsActive | bool | ✓ | Default: false (onay zincirinden sonra true) |
| SystemSuspended | bool | ✓ | Default: false. Servis org için SystemAdmin tarafından askıya alma (§9.6) |
| ContractDate | date | ✓ | Sözleşme tarihi (Management için; Service için ilk sözleşmesi) |
| ServiceStartDate | date | ✓ | Hizmet başlangıç |
| ServiceEndDate | date | ✓ | Hizmet bitiş |
| GracePeriodDays | int | ✓ | 0-31, sözleşme imzalanmazsa fatura kesilmeme süresi |
| SubscriptionPlanId | FK | ✗ | Management için abonelik planı (v2) |
| Audit alanları | | ✓ | IAuditable (ADR-0006) |
| Soft-delete alanları | | | ISoftDeletable |
| SearchText | string(2000) | ✓ | TurkishNormalizer (ADR-0010) |

### 1.3. Slug Üretimi

```
"ABC Yönetim A.Ş." → "abc-yonetim-as"
"XYZ Sayaç Okuma" → "xyz-sayac-okuma"
```

Algoritma:
1. Turkish ToLower (İ→i, I→ı)
2. Diacritic temizleme (ş→s, ğ→g, ç→c, ü→u, ö→o, ı→i)
3. Alfanumerik olmayan karakterler → `-`
4. Ardışık `-` → tek `-`
5. Baş/son `-` temizlenir
6. **Unique kontrolü** — varsa `-2`, `-3` eklenir (`abc-yonetim-as`, `abc-yonetim-as-2`, `abc-yonetim-as-3`)

### 1.4. Relationships

```
Organization (1) ─── (0..n) Branch
             (1) ─── (0..n) BankAccount     (Management için)
             (1) ─── (0..n) Site             (Management için)
             (1) ─── (0..n) ServiceContract  (Service org tarafı)
             (1) ─── (0..n) Document
             (1) ─── (0..n) Membership       (org personeli — ADR-0011)
             (1) ─── (0..1) Country
```

### 1.5. Validation Kuralları

- `NationalId.Type == VKN` gerekli değil — `Management` org gerçek kişi de olabilir (küçük ölçekli yönetim firması)
- `CommercialTitle` min 5 karakter
- `ShortTitle` 3-100 karakter
- `Email` → standart e-posta format
- `Phone` → E.164 (+905XXXXXXXXX için regex)
- `GracePeriodDays` [0, 31]
- `ServiceEndDate > ServiceStartDate >= ContractDate`
- `Code` domain-invariant (factory'de üretilir, manuel set edilmez)

### 1.6. Domain Davranışları

```csharp
public sealed class Organization : SearchableAggregateRoot<OrganizationId>
{
    public static Organization Create(
        OrganizationType type,
        string commercialTitle,
        string shortTitle,
        NationalId nationalId,
        AddressId notificationAddressId,
        string email,
        DateTimeOffset contractDate,
        DateTimeOffset serviceStart,
        DateTimeOffset serviceEnd,
        int gracePeriodDays,
        ICodeGenerator codeGenerator,
        ISlugGenerator slugGenerator);

    public void UpdateContact(string? phone, string email, string? kepAddress, string? website);
    public void UpdateTaxInfo(string? taxOffice, string? tradeRegistryNo, string? mersisNo);
    public void UpdateLogo(string logoUrl);
    public void UpdateContractPeriod(DateTimeOffset newEnd, string reason);

    public void Activate();   // İlk aktivasyon + sonraki aktifleştirmeler
    public void Deactivate(string reason);

    // Org, BB olmadığı için Soft-Delete + Restore ortak (AuditableAggregateRoot)
}
```

---

## 2. Branch (Şube)

### 2.1. Kavram

Bir organizasyonun şubesi — örn. "ABC Yönetim - İstanbul Ofisi", "ABC Yönetim - Ankara Ofisi". Personel atamaları şube seviyesinde yapılabilir.

### 2.2. Alanlar

| Alan | Tip | Zorunlu | Not |
|---|---|---|---|
| Id | Guid | ✓ | |
| OrganizationId | FK | ✓ | |
| Name | string(200) | ✓ | "İstanbul Ofisi" |
| NotificationAddressId | FK → addresses | ✓ | Şube adresi |
| Phone | string(20) | ✗ | |
| Email | string(320) | ✗ | |
| IsHeadOffice | bool | ✓ | Her org'da bir adet true |
| IsActive | bool | ✓ | Default true |
| Audit + SoftDelete | | | |
| SearchText | | | |

### 2.3. Kurallar

- Her Organization'ın **en az 1** Branch'ı vardır (otomatik: org oluşturulurken "Merkez" adında bir branch yaratılır, `IsHeadOffice = true`)
- Aynı org'un iki Branch'ı aynı `IsHeadOffice = true` olamaz
- Merkez şube silinemez (soft-delete dahil) — kullanıcı önce başka bir şubeyi merkez yapmalı
- Personel (Membership) şubeye atanabilir (opsiyonel — `ContextType=Branch`, ADR-0011)

---

## 3. Banka Kayıtları (İki Seviyeli Hiyerarşi)

### 3.1. Kavram

Gerçek hayatta Organization/Site **bir bankada müşteri olur**, o müşterilik altında **birden fazla hesap** açar. Bu yapıyı bire bir modelliyoruz:

```
Organization (veya Site)
  └── BankCustomerProfile (n tane — farklı bankalarda)
        └── BankAccountLine (n tane — aynı banka içinde farklı hesaplar)
```

**Örnek:**
- ABC Yönetim A.Ş. → Ziraat müşterisi + Garanti müşterisi + İş Bankası müşterisi (3 profile)
- Ziraat'te: TL vadesiz (aidat tahsilat) + USD vadeli (yatırım) (2 alt hesap)
- Garanti'de: TL POS bloke (tahsilat) (1 alt hesap)

**Önemli kural:** Bir banka hesabı **aynı anda** hem organizasyonun hem sitenin olamaz. Owner ya Organization ya Site (polimorfik).

### 3.2. BankCustomerProfile (Banka Müşteri Kaydı)

| Alan | Tip | Zorunlu | Not |
|---|---|---|---|
| Id | Guid (v7) | ✓ | |
| OwnerType | enum | ✓ | Organization, Site |
| OwnerId | Guid | ✓ | Polimorfik owner |
| BankId | FK → banks | ✓ | Ziraat, Garanti vb. |
| AccountHolderName | string(500) | ✓ | Bankadaki resmi ünvan |
| BankCustomerNumber | string(50) | ✗ | Bankanın verdiği müşteri no (varsa) |
| IsActive | bool | ✓ | Default true — onay zinciri YOK |
| Audit + SoftDelete | | | |

### 3.3. BankAccountLine (Alt Hesap)

| Alan | Tip | Zorunlu | Not |
|---|---|---|---|
| Id | Guid | ✓ | |
| BankCustomerProfileId | FK | ✓ | Üst profil |
| AccountNumber | string(50) | ✓ | Hesap numarası |
| SubAccountNumber | string(50) | ✗ | Ek hesap no |
| Iban | string(34) | ✓ | MOD-97 doğrulama zorunlu |
| Currency | enum | ✓ | TRY, USD, EUR, GBP, CHF... (ISO 4217) |
| AccountTypeId | FK → account_types | ✓ | Seed + SystemAdmin eklenebilir |
| BranchCode | string(10) | ✗ | Banka'dan bankaya şube bilgisi IBAN'da olmayabilir — ayrı tutulur |
| BranchName | string(200) | ✗ | "Ataşehir Şubesi" |
| IsActive | bool | ✓ | Default true |
| Purpose | string(200) | ✗ | Serbest metin: "Aidat tahsilatı" etiketi (v2'de hesap amacı modülü) |
| Audit + SoftDelete | | | |

### 3.4. IBAN Doğrulama ve Otomatik Banka Tespiti

**IBAN validation (zorunlu):**
- Format: TR + 2 check digit + 5 banka EFT kodu + ... = 26 karakter
- MOD-97 algoritması checksum kontrolü
- FluentValidation rule: `TurkishIbanAttribute`

**Otomatik banka tespiti (UX):**
- IBAN'ın 5-8. karakterleri = banka EFT kodu
- Kullanıcı IBAN yapıştırınca → BankId dropdown otomatik seçilir
- Tespit başarısızsa kullanıcı manuel seçer
- **Şube bilgisi** otomatik doldurulmaz — kullanıcı manuel girer (IBAN'da şube bilgisi garantili değil, bankadan bankaya değişir)

Kod tarafı:
```csharp
public sealed class IbanBankResolver
{
    public BankId? ResolveBank(string iban)
    {
        if (!IsValid(iban)) return null;
        var eftCode = iban.Substring(4, 4);  // TR + 2 check + 4 banka kodu
        return _bankRepository.FindByEftCodeAsync(eftCode)?.Id;
    }

    public bool IsValid(string iban) { /* MOD-97 */ }
}
```

### 3.5. AccountType Seed Tablosu

`account_types` tablosu — SystemAdmin genişletebilir.

**İlk seed:**

| Key | DisplayName | Açıklama |
|---|---|---|
| `demand_deposit` | Vadesiz Mevduat | |
| `time_deposit` | Vadeli Mevduat | |
| `pos_normal` | POS (Normal Tahsilat) | |
| `pos_blocked` | POS Bloke | |
| `corporate_collection_normal` | Kurumsal Tahsilat (Normal) | |
| `corporate_collection_blocked` | Kurumsal Tahsilat Bloke | |
| `credit_guarantee_blocked` | Kredi Teminat Bloke | |
| `foreign_exchange_deposit` | Döviz Tevdiat (DTH) | |
| `investment_fund` | Yatırım Fonu Hesabı | |
| `precious_metal` | Altın/Değerli Maden | |
| `automatic_debit` | Otomatik Borçlandırma Hesabı (OBH) | |
| `other` | Diğer | Sistem başlangıçta tutulur, SystemAdmin özel tipler ekler |

**SystemAdmin yeni tip ekleme:** UI'dan "Yeni Hesap Tipi" ekranı — key (snake_case), display name, açıklama. Her banka farklı ürün çıkarabilir (örn. "Emekli Hesabı", "Çocuk Hesabı") — sistem bunları tutabilir.

### 3.6. Onay Zinciri

**Banka hesapları için onay zinciri yoktur.** Yeni bir `BankCustomerProfile` veya `BankAccountLine` oluşturulduğunda:
- Direkt `IsActive = true` başlar
- Onay beklemez
- Ama audit log'da oluşturma kaydı tutulur (kim ne zaman ekledi)

**Gerekçe:** Banka müşteri kaydı genelde ön-sözleşme aşamasında yapılıyor (Organizasyon zaten banka ile anlaşmış, biz sadece sisteme yansıtıyoruz). Onay zinciri eklemek operasyonel olarak yavaşlatıyor. Gerekli olursa ileride değiştirilebilir.

**Yetki kontrolü** hâlâ var (ADR-0011):
- `organization.bank.manage` izni → org banka hesapları
- `site.bank.manage` izni → site banka hesapları

### 3.7. Kullanım Amacı (Hangi Hesap Nereye Bağlanır?)

**MVP'de:** `Purpose` string alanıyla etiketleme yapılır (serbest metin). Örn: "Aidat tahsilatı", "Maaş ödemeleri", "Genel giderler".

**v2'de:** `BankAccountPurpose` modülü — enum + çoklu atama:
- Bir hesap **birden fazla amaca** hizmet edebilir (örn: hem aidat tahsilatı hem maaş ödemesi)
- Amaçlar: TahsilatHesabı, ÖdemeHesabı, MaaşHesabı, KurumsalTahsilatHesabı, FaturaÖdemeHesabı vb.
- BB Dönem Code banka referansı ile eşleşme için "Aidat Tahsilat" amaçlı hesap seçimi

### 3.8. Domain Davranışları

```csharp
public sealed class BankCustomerProfile : AuditableAggregateRoot<BankCustomerProfileId>
{
    public static BankCustomerProfile Register(
        OwnerType ownerType, Guid ownerId,
        BankId bankId,
        string accountHolderName,
        string? bankCustomerNumber);

    public void UpdateHolderName(string newName);
    public void UpdateCustomerNumber(string? newNumber);
    public void Deactivate(string reason);
    public void Activate();

    // Alt hesap ekleme/çıkarma AGGREGATE BOUNDARY içinde
    public BankAccountLine AddAccountLine(
        string accountNumber, string iban, CurrencyCode currency,
        AccountTypeId accountTypeId, string? branchCode, string? branchName,
        string? purpose);
}

public sealed class BankAccountLine : Entity<BankAccountLineId>
{
    public BankCustomerProfileId CustomerProfileId { get; private set; }
    public string AccountNumber { get; private set; }
    public string Iban { get; private set; }
    // ... diğer alanlar

    public void UpdateIban(string newIban);  // yeniden validasyon
    public void Deactivate(string reason);
}
```

**Aggregate boundary:** BankCustomerProfile = aggregate root, BankAccountLine = aggregate içi entity. Tüm alt hesap operasyonları profile üzerinden geçer (DDD best practice).

---

## 4. Site (Yönetilen Yapı)

### 4.1. Alanlar

| Alan | Tip | Zorunlu | Not |
|---|---|---|---|
| Id | Guid | ✓ | |
| Code | int | ✓ | 6 hane, 100001-999999, Feistel (§11), all-time unique |
| FullTitle | string(500) | ✓ | Tam ünvan, **sistem-geneli unique** |
| ShortTitle | string(30) | ✓ | Max 30, **sistem-geneli unique** |
| NationalId (VKN) | VO | ✓ | Site'nin kendi VKN'si — zorunlu |
| SiteTypeId | FK → site_types | ✓ | Seed data + runtime eklenebilir |
| ManagingOrganizationId | FK → organizations | ✓ | Yönetici organizasyon (Management type) |
| NotificationAddressId | FK → addresses | ✓ | |
| Phone | string(20) | ✗ | Sabit telefon |
| Email | string(320) | ✓ | |
| BuildingPermitNo | string(100) | ✗ | Yapı ruhsat no |
| OccupancyDate | date | ✗ | İskan tarihi |
| ConstructionYear | int | ✗ | Yapım yılı, 1900-2100 |
| ContractDate | date | ✓ | |
| ServiceStartDate | date | ✓ | |
| ServiceEndDate | date | ✓ | |
| GracePeriodDays | int | ✓ | 0-31 |
| ManagementPlanDate | date | ✗ | Yönetim plan tarihi |
| HasCondominiumOwnership | bool | ✓ | Kat mülkiyeti var mı? (E/H) |
| CommonAreaM2 | decimal(10,2) | ✗ | Ortak alan m² |
| GreenAreaM2 | decimal(10,2) | ✓ | Yeşil alan m² |
| Poles6Meter | int | ✓ | 0-200, 6 mt aydınlatma direği |
| Poles9Meter | int | ✓ | 0-200, 9 mt aydınlatma direği |
| HasSwimmingPool | bool | ✓ | |
| HasParking | bool | ✓ | |
| ParkingVehicleCount | int | ✓ | 0-5000 (HasParking=false ise 0) |
| HasSecurityService | bool | ✓ | Güvenlik hizmet alımı |
| SmsNotificationEnabled | bool | ✓ | Default true. Site SMS bildirimlerini kullansın mı? (borç hatırlatma, OTP, bilgilendirme). Kapalıysa sadece e-posta |
| TreeCount | int | ✗ | 0-5000 |
| ProjectCompanyName | string(500) | ✗ | Site projesini yapan firma |
| ConstructionCompanyName | string(500) | ✗ | Siteyi inşa eden firma |
| HasCorporateCollectionSystem | bool | ✓ | Default false — kurumsal tahsilat sistemi var mı? |
| CorporateCollectionMethod | enum | ✗ | `Offline_Ftp`, `Online_Api` (HasCorporateCollectionSystem=true ise zorunlu) |
| BankStatementDeliveryMethod | enum | ✓ | `Manual`, `ExcelImport`, `Ftp`, `Api` — hesap ekstresi nasıl alınıyor |
| DebtAllocationMode | enum | ✓ | Default: ByShareholderPercent. Borçlandırma dağıtım kuralı — hisse oranına göre mi, tek hissedarlar borcu mu? (v2 Tahakkuk Motoru'nda kullanılır) |
| Audit + SoftDelete | | | |
| SearchText | | | |

**v2'ye bırakılanlar:**
- Sanal POS bilgileri (mağaza no, terminal no, her banka için ayrı) — v2
- Hesap ekstresi delivery mekanizmaları (FTP/API entegrasyonları) — alan tutulur, implementasyon v2

### 4.2. Hesaplanan Alanlar (Computed)

Bunlar DB'de kolon olarak **tutulur** (performans), ama yapı şeması değiştiğinde **domain event** ile otomatik güncellenir:

| Alan | Kaynak |
|---|---|
| TotalPlotCount | COUNT(Plots WHERE SiteId = X) |
| TotalParcelCount | COUNT(Parcels through Plots WHERE SiteId = X) |
| TotalBuildingCount | COUNT(Buildings WHERE SiteId = X) |
| TotalUnitCount | COUNT(Units WHERE SiteId = X) |
| UnitCountByType | JSONB: { "daire": 120, "dukkan": 8, "ofis": 4, ... } |
| TotalCaretakerApartmentCount | SUM(Buildings.CaretakerApartmentCount) |
| TotalLandscapeAreaM2 | SUM(Buildings.LandscapeAreaM2) |
| TotalGrossAreaM2 | SUM(Units.AreaM2) — brüt m² |
| TotalNetAreaM2 | SUM(Units.NetAreaM2) — net m² (Unit'e eklenecek, §5.4) |
| TotalLandShare | SUM(Units.LandShare) |
| ElevatorCount | SUM(Buildings.ElevatorCount) |
| HydrophoreCount | SUM(Buildings.HydrophoreCount) |

**Domain event zinciri (yapı şeması değiştiğinde):**
- `BuildingCreated/Updated/Deleted` → `Parcel` hesaplananları güncelle → `Plot` hesaplananları güncelle → `Site` hesaplananları güncelle
- `UnitCreated/Updated/Deleted` → aynı zincir
- Handler: `SiteStatsUpdater` (event handler)

**Performans notu:** Bu kolonlar **denormalize** — aslında her sorguda SUM ile hesaplanabilir. Ama site detay ekranları, dashboard'lar, listelerden sürekli erişildiği için kolon olarak tutulması performans avantajı sağlar. Trade-off: kod karmaşıklığı artar (domain event handler zinciri) ama query zamanı azalır.

### 4.3. Relationships

```
Site (n) ─── (1) Organization (ManagingOrganization)
     (1) ─── (1) SiteType
     (1) ─── (1) Address (notification)
     (1) ─── (0..n) BankAccount
     (1) ─── (0..n) Plot (Ada)
     (1) ─── (0..n) Document
     (1) ─── (0..n) ServiceContract (Site tarafı)
     (1) ─── (0..n) SitePhoto
     (1) ─── (0..n) Membership (Site personeli, Yönetim Kurulu)
```

### 4.4. Domain Davranışları

```csharp
public sealed class Site : SearchableAggregateRoot<SiteId>
{
    public static Site Create(
        string fullTitle,
        string shortTitle,
        NationalId taxId,            // VKN zorunlu
        SiteTypeId siteType,
        OrganizationId managingOrg,
        AddressId address,
        string email,
        DateTimeOffset contractDate,
        DateTimeOffset serviceStart,
        DateTimeOffset serviceEnd,
        int gracePeriodDays,
        SitePhysicalInfo physicalInfo,  // yeşil alan, direk, otopark vb.
        ICodeGenerator codeGenerator);

    public void UpdateContact(string? phone, string email);
    public void UpdatePhysicalInfo(SitePhysicalInfo info);
    public void UpdateConstructionInfo(string? permitNo, DateTime? occupancy, int? year);
    public void UpdateProjectAndConstruction(string? projectCompany, string? constructionCompany);
    public void UpdateCollectionSystem(bool hasCorp, CorporateCollectionMethod? method, BankStatementDeliveryMethod statementMethod);

    public void AssignSiteManager(LoginAccountId userId);
    public void RemoveSiteManager(LoginAccountId userId);

    // Personel ekleme (§4.5) — Application katmanında use case,
    // domain içinde değil. Site aggregate Membership'i doğrudan yönetmez;
    // CommandHandler Person + LoginAccount + Membership ile ilişki kurar.

    // TransferManagement — v2'ye ertelendi (§4.6)
    // ...
}
```

### 4.5. Site Çalışanları — Otomatik Sistem Kullanıcısı

**Temel kural:** Yönetim Organizasyonu seviyesinde kaydedilen **her site çalışanı** otomatik olarak SiteHub kullanıcısı olur. Rolsüz kullanıcı kaydı yok.

Kapsam: yönetim kurulu, denetim kurulu, muhasebe personeli, teknisyen, bahçıvan, temizlikçi — istisnasız hepsi.

**Gerekçe (iş vizyonu):**
- MVP'de her çalışan sisteme girer → kendi profili + çalıştığı site listesi
- v5'te İzin talep / bordro görüntüleme özellikleri eklenecek
- v6'da puantaj / bordro hesaplama gelecek
- Altyapı bugünden hazır — sonradan LoginAccount açma sürtüşmesi yaşanmasın

**İş akışı:**

1. Site Yöneticisi (veya Organizasyon seviyesinde yetkili kullanıcı) "Yeni Personel" ekranını açar
2. Form zorunlu alanları:
   - Ad, Soyad
   - TCKN/VKN/YKN (doğrulama ile)
   - Cep telefonu (E.164 format)
   - E-posta (login için — zorunlu)
   - Pozisyon (serbest metin: Muhasebeci, Bahçıvan, Yönetim Kurulu Başkanı, vb.)
   - **Rol (zorunlu — dinamik roller listesinden)**
   - İşe Başlama Tarihi
3. "Kaydet" → arka planda atomik olarak:
   - TCKN/VKN/YKN ile Person aranır; varsa bulunur, yoksa yaratılır
   - **LoginAccount yaratılır (opsiyon yok, her çalışan giriş yapar)**
   - Membership oluşturulur `(ContextType=Site, ContextId=currentSite, RoleId=seçilen rol)`
   - `SiteMemberAddedEvent` yayınlanır
4. Davet bildirimi çalışanın iletişim kanallarına gönderilir:
   - E-posta: "SiteHub'a hoş geldiniz. Şifrenizi belirleyin" linki
   - Site'de `SmsNotificationEnabled = true` ise SMS: davet mesajı

**Varsayılan rol ataması:**
- Varsayılan rol **yoktur** — personel eklerken rol **zorunlu seçilir**
- En düşük seviyeli site rolü (örn. "SiteStaff") seed olarak gelir; istenirse varsayılan dropdown seçilimi olabilir ama alan boş bırakılamaz
- Muhasebeci için "Muhasebe" rolü, teknisyen için "Teknisyen" rolü, bahçıvan için "SiteStaff" veya "Bahçıvan" özel rolü (dinamik rol yaratılabilir — ADR-0011)

**Aynı kişi birden fazla sitede çalışabilir:**
- Tek Person kaydı (TCKN unique)
- Her site için ayrı Membership — farklı sitede farklı rol olabilir
- Her site için ayrı işe başlama/ayrılış tarihi

**Görev süresi bitince:**
- İşten ayrılma → Membership `IsActive = false`, `ValidTo = ayrılış tarihi`
- Kurul üyeliği biterse (örn. seçimden sonra kurul değişti) → `ValidTo` set
- Membership silinmez — tarihçe olarak kalır
- Bir kullanıcının **son aktif Membership'i** de biterse → LoginAccount otomatik `IsActive = false` (ADR-0011)
- Kullanıcı yeniden işe alınırsa → yeni Membership eklenir, LoginAccount aktive olur (aynı LoginAccount, yeni kayıt)

**Yetki değişikliği:**
- Bir çalışanın rolü **sadece Site Personel Düzenleme ekranından** değiştirilebilir (`site.user.manage` izni)
- Başka yerden müdahale edilemez
- Her rol değişikliği audit log'a yazılır

**"Kendi Profilim" ekranı (MVP):**
- Profil bilgileri (ad, soyad, cep, e-posta)
- Profil güncelleme (cep, e-posta değişirse — kritik bilgi, 2FA doğrulaması gerekir)
- Şifre değiştirme
- 2FA ayarları (ADR-0011 §4)
- Oturum geçmişi (kendi login logları)
- **Bağlam geçişi profilde YOK** — sayfa başındaki ContextSwitcher'dan yapılır

**v5+ eklemeler (yapılacaklar listesinde):**
- İzin talebi (çalışan talep eder, yönetici onaylar)
- Bordro görüntüleme (PDF indir)
- v6: puantaj, mesai, bordro hesaplama, SGK entegrasyonu

### 4.6. Yönetim Transferi (v2'ye Ertelendi)

Bir sitenin yönetici organizasyonu değişebilir (kat malikleri kararı, firma birleşmesi/kapanması, siteninkendi yönetim şirketini kurması vb.). Bu işlem — **yönetim transferi** — MVP kapsamında değil.

**MVP yaklaşımı:**
- `Site.ManagingOrganizationId` basit FK olarak tutulur
- Değişiklik audit.entity_changes'te kaydedilir (kim ne zaman değiştirdi — tarihçe minimum seviyede var)
- Karmaşık tarihçe tablosu / yetki geçiş algoritmaları YOK

**v2 kapsamı (§15'te detayı):**
- `site_management_history` tablosu (tarihçe)
- Transfer tarihi → otomatik yetki geçişi
- Eski yönetim firmasının **kendi dönemine ait** verilere salt-okunur (readonly) erişimi:
  - Banka hesap bakiyeleri ve hareketleri (dönem sonu itibariyle)
  - Malik/Sakin/Kiracı hesap bakiyeleri ve hareketleri
  - Firma/kurumlara borç-alacak bakiye ve hareketleri
- Arşivleme — eski yönetimin bilgileri silinemez, salt-okunur olarak indirebilir
- Yeni yönetim, eski dönemin tarihsel verilerine okuma erişimi alır (tarihçe sürekli)
- Eski yönetim, **transfer tarihinden sonraki** hareketlerden sorumlu tutulmaz (tarih damgası ile ayrılır)

Bu bir v2 işi çünkü:
- Tarihçe tablosu + yetki geçiş servisi karmaşık
- Readonly arşiv export (PDF/Excel) ayrı bir iş
- İlk müşterilerin transfer ihtiyacı nadir

MVP'de basit atama, transfer ihtiyacı çıkarsa manuel SQL/support üzerinden yapılır (ilk 6-12 ayın beklenen zaten).

---

## 5. Yapı Şeması — Ada → Parsel → Yapı → BB

**Hesaplanan alanlar zinciri (v2'de implement edilecek):**

Yapı şeması dört seviyeli bir hiyerarşi. Her seviye kendi altındaki toplamları tutar (denormalize) ve alt seviyede değişiklik olduğunda domain event ile güncellenir:

```
Site (hesaplananlar §4.2'de)
  ↑
Plot / Ada (toplam parsel, yapı, BB, BB tipleri, görevli dairesi, peyzaj, brüt m², net m², arsa payı)
  ↑
Parcel / Parsel (toplam yapı, BB, BB tipleri, görevli dairesi, peyzaj, brüt m², net m², arsa payı)
  ↑
Building / Yapı (toplam BB, BB tipleri, görevli dairesi sayısı zaten mevcut, peyzaj alanı)
  ↑
Unit / BB (temel veri)
```

**Event akışı:**
- `UnitCreated/Updated/Deleted` → `Building` toplamları güncellenir
- `BuildingUpdated` → `Parcel` toplamları güncellenir
- `ParcelUpdated` → `Plot` toplamları güncellenir
- `PlotUpdated` → `Site` toplamları güncellenir

Her seviye sadece **bir üst seviyeyi** etkiler (direkt değil, olay tabanlı zincir).

**MVP kapsamı:** Temel entity alanları (aşağıda). Hesaplanan alanlar v2 — ihtiyaç olunca hesap üzerinden anlık (query-time) veya denormalize eklenir.

### 5.1. Plot (Ada)

Site'nin tapu-hukuki hiyerarşisinin başı.

| Alan | Tip | Zorunlu |
|---|---|---|
| Id | Guid | ✓ |
| SiteId | FK | ✓ |
| Name | string(100) | ✓ (örn. "Ada 1234") |
| Audit + SoftDelete | | |

### 5.2. Parcel (Parsel)

| Alan | Tip | Zorunlu |
|---|---|---|
| Id | Guid | ✓ |
| PlotId | FK | ✓ |
| Number | string(50) | ✓ (örn. "45") |
| Audit + SoftDelete | | |

### 5.3. Building (Yapı/Bina)

| Alan | Tip | Zorunlu | Not |
|---|---|---|---|
| Id | Guid | ✓ | |
| ParcelId | FK | ✓ | |
| Name | string(100) | ✓ | "A Blok", "Bina 1", "C2-10" |
| BuildingVariant | string(50) | ✗ | Aynı tip bloklar için varyant — örn. "C2" tipi binalar (C2-1, C2-2, ... C2-10). "C2-10 nolu bina C2 tipindedir." |
| MunicipalityNumber | string(50) | ✓ | Belediye no |
| BuildingTypeId | FK → building_types | ✓ | Konut, Konut+Dükkan, İşyeri vs. |
| HeatingTypeId | FK → heating_types | ✓ | Bireysel, Doğalgaz Kazanlı, Kombi... |
| UndergroundFloorCount | int | ✓ | 0-10 |
| AboveGroundFloorCount | int | ✓ | 0-50 |
| EntranceDoorCount | int | ✓ | 1-5 |
| CaretakerApartmentCount | int | ✓ | 0-3 |
| ElevatorCount | int | ✓ | 0-6 |
| HydrophoreCount | int | ✓ | 0-8 |
| CombiCount | int | ✓ | 0-8 |
| LandscapeAreaM2 | decimal(10,2) | ✓ | 0.00-2000.00 peyzaj alanı |
| Audit + SoftDelete | | | |

**Kurallar:**
- **Aynı parselde aynı isimli Yapı olamaz** — `(ParcelId, Name)` unique index
- `Name` + `BuildingVariant` beraber doğrulama yapılabilir (opsiyonel): aynı parselde C2 tipinde birden fazla bina olabilir ama isim farklı olmalı

### 5.4. Unit (Bağımsız Bölüm)

| Alan | Tip | Zorunlu | Not |
|---|---|---|---|
| Id | Guid | ✓ | |
| Code | int | ✓ | **7 hane (1000001-9999999), Feistel (§11), all-time unique** |
| BuildingId | FK | ✓ | |
| EntranceDoorNo | int | ✓ | 1-4 (binanın kaç kapısı varsa) |
| UnitNumber | string(20) | ✓ | "A12", "5-3" vs. (serbest format) |
| NationalNumberingCode | string(20) | ✗ | Ulusal numarataj no |
| AreaM2 | decimal(10,2) | ✓ | 0.00-2000.00 Brüt m² |
| NetAreaM2 | decimal(10,2) | ✗ | 0.00-2000.00 Net m² (opsiyonel — bazı BB'lerin net ölçüsü yok) |
| LandShare | decimal(12,2) | ✓ | 0.00-50000.00 Arsa payı |
| AllocationAreaM2 | decimal(10,2) | ✓ | 0.00-5000.00 Tahsis alanı |
| HeatingAreaM2 | decimal(10,2) | ✓ | 0.00-2000.00 Isıtma payı hesaplanmasında kullanılan metraj |
| UnitTypeId | FK → unit_types | ✓ | Daire, Dükkan, Ofis vs. |
| Floor | int | ✓ | -10 ile 50 arası |
| Direction | enum | ✓ | 9 yön (Kuzey, Kuzeydoğu, ..., Bilinmiyor) |
| VehicleRightCount | int | ✗ | Araç hakkı sayısı |
| Audit + SoftDelete | | | |

### 5.5. Kurallar

- `Unit.EntranceDoorNo <= Building.EntranceDoorCount`
- `Unit.Floor` [-Building.UndergroundFloorCount, Building.AboveGroundFloorCount]
- **Aynı yapıda aynı girişte aynı isimli iki Bağımsız Bölüm olamaz** — `(BuildingId, EntranceDoorNo, UnitNumber)` composite unique index
- Site'nin tüm BB'lerinin `LandShare` toplamı bilgi amaçlı tutulur (tutarlılık kontrolü değil)
- BB silinebilir **sadece** hiç aktif dönemi yoksa
- `NetAreaM2` varsa `AreaM2`'den küçük veya eşit olmalı (net <= brüt)

---

## 6. UnitPeriod (BB Dönem)

### 6.1. Kavram

Bir Unit'in zamansal kesiti — "2026-2028 arası A12 dairesi" gibi. Her dönemde bir malik grubu (hissedarlar) ve opsiyonel bir kiracı vardır.

### 6.2. Alanlar

| Alan | Tip | Zorunlu | Not |
|---|---|---|---|
| Id | Guid | ✓ | |
| Code | long | ✓ | **9 hane (111111111-999999999), Feistel (§11), all-time unique** |
| UnitId | FK | ✓ | |
| StartDate | date | ✓ | Dönem başı |
| EndDate | date | ✗ | Dönem sonu (null = hâlâ aktif) |
| StartReason | string(500) | ✓ | "Yeni devir", "Tapu satış", "İlk aktif dönem" |
| EndReason | string(500) | ✗ | "Malik değişti", "Dönem kapatıldı" |
| TitleDeedDocumentId | FK → documents | ✗ | Tapu kopyası |
| Audit + SoftDelete | | | |

### 6.3. Code Üretimi (Kritik)

`Code` alanı **banka tahsilat referansı**. Kullanıcı bu kodu banka havalesinde "Açıklama" alanına yazar; SiteHub bunu parse ederek borcuna mahsup eder.

- **9 hane** olmasının nedeni: çoğu Türk bankasının havale açıklaması short code standardı
- UI'da gruplu gösterim: `234-567-890`
- DB'de `long` olarak saklanır
- **Üretim stratejisi:** Sequence + Feistel obfuscation — detay §11
- **All-time unique** — matematiksel olarak garanti (sequence monoton artar, Feistel bijection). DB çakışma kontrolü gerekmez. Soft-delete edilmiş dönemler dahil tekrar kullanılamaz (paranın yanlış döneme geçmesi felaket).

### 6.4. Dönem Kapatma

Yeni malik devredildiğinde (önceki konuşmada):
- Yeni malik giriş tarihi `T` → eski dönemin `EndDate = T - 1 gün` otomatik set
- Eski dönemdeki tüm Shareholder kayıtlarının `EndDate` da aynı şekilde set
- Eski kiracı varsa onun `EndDate` da (veya kiracı yeni malikle sözleşmeyi devam ettirebiliyorsa ayrı konu — v2)
- `EndReason = "Malik değişti (yeni dönem: {yeniDönemId})"` set
- Eski dönem silinmez, tarihçe olarak kalır
- Bakiye borcu varsa kayıt korunur (tahsilat sürebilir)

### 6.5. Domain Davranışları

```csharp
public sealed class UnitPeriod : AuditableAggregateRoot<UnitPeriodId>
{
    public static UnitPeriod OpenNew(
        UnitId unitId,
        DateTimeOffset startDate,
        string startReason,
        IReadOnlyList<ShareholderInput> shareholders,
        TenantInput? tenant,
        ICodeGenerator codeGen);

    public void Close(DateTimeOffset endDate, string reason);
    public void AddTitleDeedDocument(DocumentId docId);
    public void UpdateTenant(TenantInput? newTenant);  // Kiracı ekleme/çıkarma
}
```

### 6.6. Kurallar

- Bir Unit'te aynı anda **tek bir aktif dönem** (EndDate=NULL) olabilir
- Yeni dönem açılırken eski aktif dönem otomatik kapatılır
- Dönemin `StartDate < EndDate` (ya da EndDate null)
- Hissedarların toplam hisse oranı = 100.00% olmalı (tolerans: ±0.01)
- Dönem açılabilmesi için en az 1 hissedar olmalı

---

## 7. Shareholder (Hissedar/Malik)

### 7.1. Kavram

Bir Unit Period'un malikidir. Tek kişi olabilir (tek hissedar, %100) veya birkaç kişi (hissedarlar, toplam 100%).

### 7.2. Alanlar

| Alan | Tip | Zorunlu | Not |
|---|---|---|---|
| Id | Guid | ✓ | |
| UnitPeriodId | FK | ✓ | |
| PersonId | FK | ✓ | Person ile ilişkilenir (ADR-0011) |
| SharePercent | decimal(8,5) | ✓ | 0.00001-100.00 |
| StartDate | date | ✓ | Dönem başlangıcı ile aynı olabilir |
| EndDate | date | ✗ | Dönem sonu / dönem içinde çıkış |
| ContactPersonName | string(300) | ✗ | İrtibat kişi adı (hissedar tüzel kişiyse) |
| ContactPersonPhone | string(20) | ✗ | |
| RefundIban | string(34) | ✗ | Para iadesi için IBAN (opsiyonel) |
| RefundAccountHolderName | string(500) | ✗ | IBAN sahibi ünvan |
| RefundBankAccountStatus | enum | ✗ | PendingApproval, Approved, Rejected (IBAN varsa) |
| Audit + SoftDelete | | | |

**Para iadesi banka hesabı (opsiyonel):**
- Hissedar sistemden para iadesi almak isterse (fazla ödeme iadesi, avans iadesi vb.) IBAN tanımlar
- Bu IBAN **mutlaka onay sürecinden** geçer (ADR-0013 — onay zinciri altyapısı bunu kullanacak)
- Onaylanmadan iade ödeme yapılamaz (domain kural)
- IBAN doğrulaması (MOD-97) zorunlu
- MVP'de alan tutulur, onay akışı v2 (onay zinciri ADR-0013 sonrasında devreye girer)

### 7.3. Person Referansı ve Otomatik Sistem Kullanıcısı

Her Shareholder bir **Person** kaydına referans verir. Yeni bir hissedar eklerken:

1. NationalId ile Person aranır
2. Varsa mevcut Person kullanılır
3. Yoksa yeni Person oluşturulur (TCKN/VKN/YKN + Tam Ünvan + Cep Telefonu + **E-posta (zorunlu)** + Tebligat Adresi)
4. **LoginAccount otomatik yaratılır** — hissedar doğal sistem kullanıcısıdır
5. Davet bildirimi gönderilir (E-posta + Site'nin `SmsNotificationEnabled=true` olması durumunda SMS)

**İş vizyonu:** Malik/Hissedar = doğal sistem kullanıcısı. Önceden "talep üzerine LoginAccount açılır" denmişti, bu karar değişti: **her hissedar otomatik sistem kullanıcısı**. Gerekçe:
- Malik sitesine dair güncel bilgilere erişmeli (borç görüntüleme, bildirimler, duyurular)
- v2'de tahakkuk/tahsilat geldiğinde hemen kullanılır durumda olacak
- Sonradan açma sürtüşmesi engellenir

**Implicit yetki (ADR-0011 §8.1'de hesaplanır):**
- Hissedarın Person'u aktif bir Shareholder kaydına bağlıysa ilgili UnitPeriod + Unit için `period.read` + `unit.read` izni otomatik verilir
- Membership tablosuna yazılmaz — BB dönem ilişkisinden türer (implicit)
- `EndDate` set edildiğinde implicit yetki otomatik kalkar
- Hissedar **düzenleme yapamaz** (BB/dönem düzenlemesi site yöneticisinin işi)

### 7.4. Hisse Oranı Kuralları

- `SharePercent` [0.00001, 100.00]
- Aynı UnitPeriod'daki tüm aktif Shareholder'ların toplamı = 100.00 ± 0.01
- Tek hissedar = 100.00
- Birden fazla hissedar = farklı oranlar
- Oran değişmesi = **yeni dönem başlatır** (örn. birisi hissesini diğerine devretti → eski dönem kapat, yeni açılır)

---

## 8. Tenant (Kiracı)

### 8.1. Alanlar

| Alan | Tip | Zorunlu | Not |
|---|---|---|---|
| Id | Guid | ✓ | |
| UnitPeriodId | FK | ✓ | |
| PersonId | FK | ✓ | Person ile ilişkilenir |
| StartDate | date | ✓ | |
| EndDate | date | ✗ | |
| ContactPersonName | string(300) | ✗ | İrtibat kişi |
| ContactPersonPhone | string(20) | ✗ | |
| LeaseContractDocumentId | FK → documents | ✗ | Kira kontratı kopyası |
| RefundIban | string(34) | ✗ | Para iadesi için IBAN (opsiyonel) |
| RefundAccountHolderName | string(500) | ✗ | IBAN sahibi ünvan |
| RefundBankAccountStatus | enum | ✗ | PendingApproval, Approved, Rejected |
| Audit + SoftDelete | | | |

**Para iadesi banka hesabı (opsiyonel):** Shareholder §7.2'deki ile aynı kurallar — MOD-97 IBAN doğrulaması + onay süreci (v2, ADR-0013 sonrası aktif).

### 8.2. Kurallar

- Bir UnitPeriod'da aynı anda **birden fazla** kiracı **OLAMAZ** (iş kararı — karışıklığı önlemek için)
- Kira tutarı/artış oranı **tutulmaz** (iş kararı — SiteHub'ın kapsamı değil)
- Kiracı Person'u için **LoginAccount otomatik yaratılır** (Hissedar §7.3 ile aynı kural)
  - Kiracı eklerken E-posta zorunlu
  - Davet bildirimi gönderilir (E-posta + Site SmsNotificationEnabled ise SMS)
  - Implicit yetki: aktif kiracı, ilgili UnitPeriod + Unit için `period.read` + `unit.read`
- Kiracı ayrılırken → `EndDate` set, Tenant kaydı kapanır; UnitPeriod açık kalır
- Yeni kiracı gelirse → eski tenant kayıtı kapatılır (`EndDate` set), yeni Tenant kaydı oluşturulur (UnitPeriod aynı kalır)
- Son aktif Tenant kaydı kapanan Person'un aktif LoginAccount'u varsa → hissedar veya çalışan olarak başka rolü yoksa LoginAccount `IsActive = false` (ADR-0011)

---

## 9. ServiceContract (Servis Sözleşmesi)

### 9.1. Kavram

Bir Site ile bir Service Organization arasındaki sözleşme. Sözleşmede tanımlı izinler dahilinde servis org'un tüm personeli o siteye erişir (ADR-0011 §6.7).

### 9.2. Alanlar

| Alan | Tip | Zorunlu | Not |
|---|---|---|---|
| Id | Guid | ✓ | |
| SiteId | FK | ✓ | |
| ServiceOrganizationId | FK | ✓ | Organization (OrganizationType=Service) |
| ContractTitle | string(200) | ✓ | "ABC Site - Sayaç Okuma Hizmet Sözleşmesi" |
| StartDate | date | ✓ | |
| EndDate | date | ✓ | |
| MonthlyFee | decimal(14,2) | ✗ | Aylık ücret (isteğe bağlı — bilgi amaçlı) |
| Currency | string(3) | ✗ | Default TRY |
| GrantedPermissions | string[] | ✓ | Kod-tanımlı permission listesi (örn. `["site.read", "billing.heating.create"]`) |
| ContractDocumentId | FK → documents | ✗ | Sözleşme PDF'i |
| Status | enum | ✓ | Draft, PendingApproval, Active, Expired, Terminated, Rejected |
| AccessEnabled | bool | ✓ | Default true. Site/Org/Sistem seviyelerinden kapatılabilir (anahtar) |
| TerminationReason | string(1000) | ✗ | Erken fesih nedeni |
| Audit + SoftDelete | | | |

### 9.3. GrantedPermissions

Servis sözleşmesinde verilen izinler kod-tanımlı permission listesinden seçilir (ADR-0011 §6.1). **Özel servis izinleri** de eklenebilir (MVP'de site-geneli kaba izinler):

```
site.read
site.unit.read
billing.heating.create     # Isınma faturası kesebilir (sayaç okuma firması)
billing.water.create
accounting.view            # Muhasebe kayıtlarını okuyabilir (mali müşavir)
legal.document.create      # Hukuki yazışma (avukat)
```

### 9.4. Yaşam Döngüsü

1. **Draft:** Org yöneticisi sözleşme oluşturur. Servis firması henüz erişemez.
2. **PendingApproval:** Sözleşme onay zincirine girer (§9.13). İlgili onaylayıcılara bildirim gider.
3. **Activation:** Tüm onaylar alınınca sözleşme aktive olur. Servis firması erişir.
4. **Active:** `StartDate <= now <= EndDate` aralığında servis firması erişir.
5. **Expired:** `EndDate < now` → otomatik expired, erişim kesilir (ADR-0011 §8.1'deki hesaplama bunu dikkate alır).
6. **Terminated:** Erken fesih (istediği bir tarih) — neden zorunlu; fesih kendi başına bir onay zincirinden geçebilir (v2).
7. **Rejected:** Onay sürecinde reddedildi — red nedeni kaydedilir, tekrar Draft'a alınabilir veya iptal edilir.

Hiçbir durumda sözleşme silinmez — tarihçe.

**Status enum güncellemesi:**

```
Draft, PendingApproval, Active, Expired, Terminated, Rejected
```

### 9.5. Kurallar

- `ServiceOrganizationId` mutlaka `OrganizationType = Service` olmalı (domain invariant)
- `EndDate > StartDate`
- `GrantedPermissions` boş olamaz (en az 1 izin)
- Bir site + servis org çifti için aynı anda tek aktif sözleşme olabilir (önceki aktifse yenisi açılamaz)

### 9.6. Üç Seviyeli Erişim Kontrolü

Aktif bir sözleşmenin yanında **erişim anahtarı** 3 seviyede kapatılabilir:

| Seviye | Kim | Ne yapar | Etki |
|---|---|---|---|
| **Sistem** | SystemAdmin | `ServiceOrganization.SystemSuspended = true` | Servis firmasının **tüm** sistem erişimi kapanır |
| **Organizasyon** | OrganizationManager | `ServiceOrganizationBlock` kaydı oluşturur | Yönetim Org'un **kendi sitelerine** servis firmasının erişimi kapanır |
| **Site** | SiteManager | `ServiceContract.AccessEnabled = false` | Sadece bu sitede erişim kapanır |

### 9.7. ServiceOrganizationBlock Tablosu

Yönetim Organizasyonu belirli bir servis firmasını kendi bazında bloklayabilir.

| Alan | Tip | Zorunlu |
|---|---|---|
| Id | Guid | ✓ |
| ManagementOrganizationId | FK → organizations | ✓ |
| ServiceOrganizationId | FK → organizations | ✓ |
| Reason | string(1000) | ✓ |
| BlockedAt | datetimeoffset | ✓ |
| BlockedBy | FK → login_accounts | ✓ |
| UnblockedAt | datetimeoffset | ✗ |
| UnblockedBy | FK → login_accounts | ✗ |
| UnblockReason | string(1000) | ✗ |

Bir block kaydı aktif (UnblockedAt = null) ise → servis firması bu yönetim organizasyonunun sitelerine erişemez.

### 9.8. CanAccess Algoritması

```
CanAccess(serviceUser, site):
  // 1. Sistem seviyesi
  if serviceUser.Organization.SystemSuspended
    → return (false, "Servis firması sistem seviyesinde askıya alındı")

  // 2. Organizasyon seviyesi
  if exists ServiceOrganizationBlock(
        management = site.ManagingOrganization,
        service    = serviceUser.Organization,
        UnblockedAt is null)
    → return (false, "Yönetim organizasyonu tarafından bloklandı")

  // 3. Sözleşme var mı ve aktif mi?
  contract = ServiceContract(site, serviceUser.Organization)
  if contract is null                           → return (false, "Sözleşme yok")
  if contract.Status != Active                  → return (false, $"Sözleşme durumu: {status}")
  if contract.AccessEnabled == false            → return (false, "Erişim anahtarı kapalı")
  if now < contract.StartDate                   → return (false, "Sözleşme henüz başlamadı")
  if now > contract.EndDate                     → return (false, "Sözleşme süresi doldu")

  // 4. Her şey tamam
  return (true, null)
```

Bu algoritma ADR-0011 §8.1'deki `ComputePermissions` içinde çağrılır — servis kullanıcısının hangi sitelere ve hangi izinlerle erişebileceği hesaplanır.

### 9.9. DAF Okuma Firması Senaryosu (MVP Kapsamı)

**İş tanımı:** DAF Okuma Firması gibi sayaç okuma şirketleri:
- Site bazlı Isı Pay Ölçer (kalorimetre) okuması yapar
- Doğalgaz + Su faturalarını BB'lere paylaştırır
- Paylaşım Pusulalarını Excel olarak üretir
- Siteye teslim eder

**MVP kapsamı:**
- DAF bir **ServiceOrganization** olarak sisteme kaydedilir
- DAF'ın birden fazla kullanıcısı olur (her biri ayrı LoginAccount — Mehmet, Ayşe, Fatma)
- Her site ile ayrı `ServiceContract` (izinler: `site.read`, `site.document.upload`)
- DAF kullanıcısı login olur → ContextSwitcher'dan sözleşmeli siteler arasında geçer
- Site bağlamında **dokümanları yükleyebilir** (Paylaşım Pusulaları Excel dosyası)
- Tahakkuk oluşturma / borç yansıtma **MVP'de YOK** (v2 Tahakkuk Motoru — ADR-0021)

**v2 genişlemesi (Tahakkuk Motoru):**
- Excel şablonu export (BB listesi + BB Dönem Code)
- Excel import + BB eşleştirme + borç oluşturma
- Malik/Kiracı resolve (site konfigürasyonundaki DebtAllocationMode'a göre)
- Retroaktif dönem değişikliğinde borç yeniden hesaplaması

**Cari hesap (DAF'ın kendisine ait):**
- DAF, sitenin bir **cari hesabı** olur (DAF siteye fatura kesecek, site ödeme yapacak)
- Cari Hesap modülü **v2** (ADR-0020)

### 9.10. Erişim Yönetim UI'ı

**SystemAdmin ekranı:** Tüm ServiceOrganization listesi, her biri için "Sistem Seviyesinde Askıya Al" / "Askıdan Kaldır".

**OrganizationManager ekranı:** "Engellediğim Servis Firmaları" listesi + "Yeni Engelleme" butonu (sebep + onay).

**SiteManager ekranı:** Site'nin aktif servis sözleşmeleri listesi + her biri için toggle switch "Erişim Açık/Kapalı".

Her toggle işlemi `audit.entity_changes` + `audit.security_events` kaydı oluşturur.

### 9.11. ServiceOrganization Personel Ekleme Akışı

ServiceOrganization'un kendi kullanıcıları vardır (örn. DAF'ta Mehmet, Ayşe, Fatma). Bu kullanıcılar **DAF'ın kendi yöneticisi** tarafından yönetilir — yönetim organizasyonu DAF'ın personelini görmez/değiştirmez.

**Yetkili roller:**
- `ServiceOrganizationManager` (seed rolü) veya sistem adminleri
- Yetki: `org.user.manage` (kendi ServiceOrganization'u içinde)

**İş akışı (§4.5 ile aynı mantıkta, kapsam farklı):**

1. DAF'ın yöneticisi (Ayşe) "Yeni Personel" ekranını açar
2. Form zorunlu alanları:
   - Ad, Soyad
   - TCKN/VKN/YKN
   - Cep telefonu
   - E-posta (zorunlu — login için)
   - Pozisyon (serbest metin: Teknisyen, Saha Elemanı, Operatör vb.)
   - **Rol (zorunlu — ServiceOrg seviyesinde dinamik roller)**
   - İşe Başlama Tarihi
3. "Kaydet" → atomik olarak:
   - TCKN/VKN/YKN ile Person aranır; varsa bulunur, yoksa yaratılır
   - LoginAccount yaratılır (rolsüz kullanıcı yok, §4.5 ile tutarlı)
   - Membership oluşturulur `(ContextType=ServiceOrganization, ContextId=DAF_orgId, RoleId=seçilen rol)`
   - `ServiceOrgMemberAddedEvent` yayınlanır
4. Davet bildirimi gönderilir:
   - E-posta: davet linki + şifre belirleme
   - SMS: (ServiceOrganization için sistem seviyesinde bir SMS konfigürasyonu var mı v2'ye alınacak; MVP'de sadece e-posta)

**ServiceOrg personelinin site erişimi:**

DAF personeli tek site'ye değil, **DAF'ın tüm aktif sözleşmeli sitelerine** erişir (ADR-0011 §6.7). `CanAccess(serviceUser, site)` algoritması (§9.8) her site için kontrol yapar. Personel bazlı alt-atama **yok** (Mehmet → Ataşehir, Fatma → Kadıköy gibi bir ayrım MVP'de yok).

**Yetki matrisi:**

| Rol | DAF içinde ne yapar |
|---|---|
| `ServiceOrganizationManager` | Personel ekle/çıkar/güncelle, org bilgilerini yönet |
| `ServiceOrganizationStaff` (örn: teknisyen) | Site'ye gir, sözleşmede tanımlı işleri yap (belge yükle vb.) |
| ... (org yöneticisi tarafından dinamik yaratılabilir) | ... |

### 9.12. Belge Yükleme Akışı (Servis Firması)

DAF gibi servis firmaları **birden fazla belge** tipiyle çalışır:

**Belge türleri (DAF örneği):**
- **Site geneli belgeler:** Dönem toplamı listesi (tüm BB'lerin toplam ısınma gideri)
- **BB bazlı belgeler:** Her bir BB'nin kendi gider pusulası (paylaşım detayı)

**Yükleme paketi (ZIP):**

DAF her okuma döneminde **sıkıştırılmış tek bir dosya** yükler. Örnek isim formatı:

```
20260319-20260420-ISINMA-GIDERI-BELGELERI.zip
  ├── site-toplam-listesi.xlsx
  ├── paylasim-pusulalari/
  │   ├── BB-1234567-pusula.pdf
  │   ├── BB-1234568-pusula.pdf
  │   ├── BB-1234569-pusula.pdf
  │   └── ...
  └── metadata.json (opsiyonel — otomatik işlem için)
```

**İsim formatı konvansiyonu:**
`{BaşlangıçTarihi}-{BitişTarihi}-{BELGE_TÜRÜ}.zip`

**MVP'de ZIP işleme (opsiyonel):**
- ZIP yüklenir → DocumentLibrary'ye **tek kayıt** olarak kaydedilir (tek belge, MimeType: `application/zip`)
- İçinde ne olduğu sistem tarafından **açılmaz/parse edilmez** — site yöneticisi indirip manuel inceler
- Bu MVP için yeterli (minimum iş)

**v2'de genişletilecek (Tahakkuk Motoru):**
- ZIP içeriği otomatik açılır
- `site-toplam-listesi.xlsx` parse edilir → tahakkuk kayıtları oluşturulur
- `BB-xxx-pusula.pdf` dosyaları BB'lere bağlanır (DocumentLibrary'de her biri ayrı kayıt, OwnerType=Unit)
- Dönem bilgisi dosya adından çıkarılır

**Belge kategorisi:**

DAF yüklemeleri için özel bir kategori seed'e eklenecek:

| Key | DisplayName |
|---|---|
| `service_organization_delivery` | Servis Firması Teslim Paketi (ZIP) |

SystemAdmin ileride alt kategoriler ekleyebilir ("Isınma Pusula Paketi", "Su Okuma Paketi" vb.).

**Yetkilendirme:**
- Servis firması belge yükleyebilsin diye sözleşme `GrantedPermissions` listesinde `site.document.upload` izni olmalı
- Yüklenen belge `DocumentLibrary.OwnerType = Site`, `CategoryId = service_organization_delivery`
- `UploadedByLoginAccountId` denormalize saklanır (DAF'ta kim yükledi)

### 9.13. Aktivasyon Onay Zinciri

Bir ServiceContract imzalanmadan önce **onay zincirinden** geçer. Bu zincir ADR-0013'teki generic onay zinciri altyapısını kullanır.

**Varsayılan onay şablonu (Site Sözleşmesi için):**

| Sıra | Onaylayıcı | SLA | Not |
|---|---|---|---|
| 1 | OrganizationManager (yönetim org'unun yöneticisi) | 3 gün | Sözleşme metnini + finansalı inceler |
| 2 | SiteBoard (site yönetim kurulu üyeleri) | 7 gün | Site menfaatine olduğunu onaylar; en az yarıdan fazla üye onaylamalı |

**Akış:**

1. Sözleşme Draft'tan `PendingApproval`'a geçer (OrgManager veya yetkili kullanıcı sunar)
2. İlk adım onaylayıcılara (OrganizationManager rolündeki kullanıcılar) bildirim gider
3. Adım 1 onaylanırsa → Adım 2'ye geçer; SiteBoard üyelerine bildirim
4. Adım 2'de quorum sağlanırsa (yarıdan fazla SiteBoard onayı) → sözleşme `Active`
5. Herhangi bir adımda reddedilirse → sözleşme `Rejected`; red nedeni kaydedilir, tarihçe tutulur
6. SLA ihlal edilirse → süreç sahibine ve onaylayıcının amirine bildirim (ADR-0013)

**Onay şablonu esnek:**

- Her organizasyon veya site kendi onay şablonunu tanımlayabilir (ADR-0013 — `ApprovalPolicy`)
- Varsayılan şablon sistem seed
- Org veya Site daha sıkı politikalar ekleyebilir (örn. "Aylık ücret 50.000 TL'yi aşarsa 3. adım: Organization Owner onayı")

**İleride (v3+ Satın Alma Modülü):**
- Hizmet alımı kararı satın alma sürecinin bir parçası olur
- DAF seçimi = sözleşme imzalama tetikleyicisi
- Satın alma onay zinciri + sözleşme onay zinciri entegre edilir
- Şimdilik sadece sözleşme onay zinciri MVP'de

---

## 10. DocumentLibrary

### 10.1. Kavram

SiteHub'da her entity'nin (Organization, Site, BB, Dönem, Sözleşme, Hissedar, Kiracı, Cari Firma...) belgeleri **merkezi tek bir sistemde** tutulur. Her entity için ayrı tablo yerine polimorfik `OwnerType + OwnerId` ile tek tablo — ölçeklenebilir ve tutarlı.

**İki parçalı mimari:**
- **Metadata** → PostgreSQL (document tablosu)
- **Dosya içeriği** → MinIO (S3-uyumlu object storage)

### 10.2. Alanlar

| Alan | Tip | Zorunlu | Not |
|---|---|---|---|
| Id | Guid (v7) | ✓ | |
| OwnerType | enum | ✓ | Organization, Site, Unit, UnitPeriod, BankCustomerProfile, ServiceContract, Person, ServiceOrganization, Cari... |
| OwnerId | Guid | ✓ | Polimorfik owner |
| AssociatedUnitPeriodId | FK → unit_periods | ✗ | BB belgeleri için ilgili dönem (dönem bazlı filtreleme) |
| IsPermanent | bool | ✓ | Default false. True ise belge döneme bağlı değildir (rölöve, ruhsat, iskan vb.) — tüm geçmiş/yeni malikler + kiracılar görebilir |
| CategoryId | FK → document_categories | ✓ | Seed + SystemAdmin eklenebilir |
| Title | string(300) | ✓ | |
| Description | string(2000) | ✗ | |
| OriginalFileName | string(500) | ✓ | Orijinal dosya adı (kullanıcı yüklediği) |
| MimeType | string(100) | ✓ | Beyaz liste ile sınırlı |
| SizeBytes | long | ✓ | Max 25 MB (sistem config) |
| StoragePath | string(500) | ✓ | MinIO path: `site/234567/docs/01908a.pdf` |
| Checksum | string(64) | ✓ | SHA-256 — integrity doğrulama |
| IsEncrypted | bool | ✓ | Default false. Application-level encryption uygulandıysa true (v2) |
| UploadedByLoginAccountId | FK → login_accounts | ✓ | Kim yükledi |
| UploadedAt | datetimeoffset | ✓ | |
| Audit + SoftDelete | | | AuditableAggregateRoot + ISoftDeletable |
| SearchText | string(2000) | ✓ | Title + Description + OriginalFileName + Kategori + Yükleyen adı (TurkishNormalizer) |

### 10.3. Belge Kategorileri ve IsPermanent İşareti

`document_categories` tablosuna iki önemli kolon:

| Kolon | Tip | Not |
|---|---|---|
| IsPermanent | bool | True ise BB'ye yüklendiğinde otomatik `Document.IsPermanent = true` olur (örn. Rölöve, Ruhsat) |
| IsSensitive | bool | True ise MVP'de hassas erişim kuralları, v2'de application-level encryption |

**Seed kategori örnekleri:**

| Kategori | IsPermanent | IsSensitive | Sahip tipler |
|---|---|---|---|
| Yönetim Kararları | false | false | Site |
| Bütçe Tebligatları | false | false | Site |
| Sözleşmeler | false | true (hassas) | Site, Organization, ServiceContract |
| Vekaletname & İmza Sirküleri | false | true | Person |
| Kimlik Kopyaları | false | true | Person |
| Mahkeme Kararları | false | true | Site, Unit, Person |
| Dilekçeler | false | false | Unit, Person |
| Tapu Kopyaları (güncel) | true | true | Unit |
| Rölöve / Plan | true | false | Unit |
| İskan / Ruhsat | true | false | Unit, Building |
| Kira Kontratları | false | true | UnitPeriod (Tenant altında) |
| Faturalar | false | false | UnitPeriod, Site |
| Hesap Ekstreleri | false | true | UnitPeriod, BankCustomerProfile |
| Tebligatlar | false | false | Unit, UnitPeriod |
| Fotoğraflar | false | false | Site, Unit |
| Paylaşım Pusulaları | false | false | Unit, UnitPeriod (DAF çıktısı) |
| Servis Firması Teslim Paketi (ZIP) | false | false | Site |
| Diğer | false | false | * |

### 10.4. Dönem-Bazlı Erişim (Malik/Kiracı Filtreleme)

Malik ve kiracıların belge erişimi **sahip olduğu döneme** göre filtrelenir.

**Kurallar (ADR-0011 §8.1 ComputePermissions içinde uygulanır):**

| Kullanıcı tipi | BB belgelerine erişim |
|---|---|
| **Malik (aktif dönem)** | Kendi aktif döneminin belgeleri + **IsPermanent = true** belgeler (kalıcı BB belgeleri — rölöve, ruhsat) |
| **Malik (eski dönem)** | Kendi eski döneminin belgeleri + IsPermanent belgeler — **ama sadece LoginAccount aktifse** |
| **Kiracı (aktif dönem)** | Kendi aktif döneminin belgeleri + IsPermanent belgeler |
| **Kiracı (eski dönem)** | Aynı — sadece LoginAccount aktifse |
| **Site Yöneticisi** | **Tüm dönemler** (aktif + geçmiş) + tüm IsPermanent belgeler |
| **Ortak malik** (aynı dönem, farklı hisse) | Birbirlerinin belgelerini **görebilir** (ortak mülk) |

**MVP'de eski malik/kiracı erişimi (Karar 2'nin sonucu):**

Son aktif Shareholder/Tenant kaydı kapandığında, Person'un başka aktif rolü/kaydı yoksa → LoginAccount **otomatik `IsActive = false`** (ADR-0011). Yani eski malik/kiracı sisteme giriş yapamaz. Geçmiş belgelerini görüntülemek isterse:
- Site yöneticisinden resmi talep
- Site yöneticisi belgeyi indirip gönderir

**v2'de genişleme:** Eski malik/kiracı için "readonly tarihçe erişimi" modu (örn. 1 yıl süreyle pasif-okuma yetkisi).

### 10.5. Ortak Malik Davranışı (Karar 4)

Aynı UnitPeriod'da birden fazla Shareholder varsa (örn. Ayşe %60, Can %40):
- **Her iki ortak birbirlerinin BB belgelerini görebilir** (ortak mülk — KMK)
- Belge yüklendiğinde kişiye ayrıştırılmaz, BB'ye ait olarak tüm dönem maliklerine gözükür
- Kişisel belge (gizli dilekçe vb.) istiyorsa → Person belgesi olarak yüklenir (BB'ye değil)

### 10.6. Otomatik Dönem Atama (Karar 3)

BB'ye belge yüklenirken `AssociatedUnitPeriodId` otomatik hesaplanır + kullanıcı onayına sunulur.

**Algoritma (upload sırasında):**

```
Input: OwnerType=Unit, OwnerId=X, BelgeTarihi=T (kullanıcı girer)

1. Kategori IsPermanent ise → AssociatedUnitPeriodId = NULL, bitir.
2. Unit X'in tüm UnitPeriod'ları getir
3. StartDate <= T <= EndDate (veya EndDate IS NULL) olan periyodu bul
4. Bulunursa → AssociatedUnitPeriodId = o periyodun Id'si
   Kullanıcıya göster: "Bu belge Dönem X (Malik: Ayşe, 2025-06-01 den itibaren) altına eklenecek. Onaylıyor musunuz?"
5. Bulunmazsa → UI'da manuel seçim dropdown (tüm dönemler listelenir)
6. Kullanıcı onaylar veya değiştirir → kayıt
```

**BelgeTarihi (belge içeriğinin tarihi):**
- Yükleme tarihi DEĞİL — belgenin **içeriğinin** tarihi (fatura tarihi, sözleşme tarihi vb.)
- Kullanıcı yüklerken form'a girer (default: bugün)
- Otomatik OCR ile tespit v2

### 10.7. Site Belge Yönetim Merkezi

**Amaç:** Yönetim Organizasyonu ve yetkili site personelinin tüm site belgelerine tek bir merkezi ekrandan erişimi.

**Erişim:**
- Yönetim Organizasyonu kullanıcıları (Membership ile site'ye erişimi olanlar) — otomatik
- Site personeli — `site.document.view` izniyle (rol atamasında verilir)
- **Malik/Kiracı erişemez** (onlar sadece kendi BB + dönem belgelerini görür, §10.4)
- **Servis Org** — sözleşmedeki `GrantedPermissions`'a göre (genellikle sadece `site.document.upload`)

**Ekran özellikleri:**

- **Liste görünümü:** Belge başlığı, kategori, sahip (Site/BB X/Hissedar Y), yükleyen, tarih, dosya tipi, boyut
- **Filtreleme paneli:**
  - Kategori (çoklu seçim)
  - Sahip tipi (Site / BB / Hissedar / Kiracı / Sözleşme / Cari Firma)
  - BB seçimi (BB'ye bağlı belgeler için)
  - Yükleyen (kullanıcı)
  - Tarih aralığı (upload tarihi / belge tarihi)
  - Dosya tipi (PDF / Resim / Word / Excel / ZIP)
  - Dönem (BB belgeleri için)
  - IsSensitive filtresi (hassas belgeleri göster/gizle)
  - **Silinmiş belgeleri göster** (sadece SystemAdmin)
- **Arama:** Metadata'da Türkçe normalize arama (Title + Description + OriginalFileName + Kategori + Yükleyen adı)
- **Sayfalama:** 50 kayıt/sayfa, sonsuz scroll veya pagination
- **Sıralama:** Tarihe göre (default), başlığa göre, boyuta göre

**v2 arama genişletmesi:**
- Full-text content search (PDF/DOCX içerik indexleme, PostgreSQL tsvector + GIN)
- OCR (taranmış belgelerin içeriği)
- Tag sistemi (kullanıcı kendi tag'lerini ekler)

### 10.8. Yükleme Akışı

1. "Yeni Belge Yükle" dialog'u
2. Form:
   - Dosya seç (drag-drop + browse)
   - Kategori (dropdown, IsPermanent/IsSensitive default değerleri görünür)
   - Başlık (zorunlu)
   - Açıklama (opsiyonel)
   - Belge Tarihi (BB belgesi ise — dönem ataması için)
   - Sahip (genellikle otomatik — ekran bağlamından geliyor)
3. Client-side validation:
   - Dosya boyutu ≤ 25 MB
   - MIME type izinli listede
   - Başlık boş değil
4. Upload:
   - Stream olarak MinIO'ya yükleme
   - SHA-256 checksum hesaplama
   - IsSensitive kategorisi + v2 encryption → dosya şifreli yüklenir
5. Metadata kaydı (atomic):
   - Document entity oluşturulur
   - `AssociatedUnitPeriodId` otomatik atanır (BB ise)
   - `DocumentUploadedEvent` yayınlanır (audit)
6. Kullanıcıya başarı bildirimi

### 10.9. İndirme Akışı

1. Kullanıcı "İndir" butonuna basar
2. Server tarafında yetki kontrolü:
   - OwnerType + OwnerId için izin var mı? (site.document.view, period.read, vb.)
   - IsSensitive ise ek kontrol (kategoriye özel izin — v2)
   - Soft-deleted mı? Deleted ise → sadece SystemAdmin erişir
3. Yetkiliyse → MinIO presigned URL üretilir (5 dakika geçerli)
4. Browser yönlendirilir → dosya iner
5. `DocumentDownloadedEvent` audit log'a yazılır (kim ne zaman)
6. IsSensitive ise → `SensitiveDocumentAccessed` özel event

### 10.10. Önizleme (Browser'da Görüntüleme)

- **PDF:** Browser built-in viewer (iframe)
- **Resim (JPEG/PNG):** img tag
- **Word/Excel/ZIP:** İndirme zorunlu (browser doğrudan göstermiyor)

Önizleme de indirme gibi yetki kontrolünden geçer ve audit'e yazılır.

### 10.11. Silme Politikası ve KVKK

**Temel kural:** Yönetim Organizasyonu ve site kullanıcıları **kalıcı silme (hard delete) yapamaz.** Sadece soft-delete yapabilir.

**Silme matrisi:**

| Kullanıcı | Soft Delete | Hard Delete |
|---|---|---|
| Site kullanıcısı (yetkili) | ✓ | ✗ |
| Yönetim Org kullanıcısı (yetkili) | ✓ | ✗ |
| ServiceOrg kullanıcısı | ✗ | ✗ |
| Malik / Kiracı | ✗ | ✗ |
| SystemAdmin | ✓ | ✓ (sadece özel durumlar) |

**SystemAdmin hard delete koşulları:**
- KVKK "unutulma hakkı" (kişisel veri silme) talebi
- Yanlış yüklemeler (örn. başka kişinin kimlik kopyası yanlışlıkla yüklendi)
- Hukuki zorunluluk (mahkeme kararı)
- Her hard delete **özel onay zincirinden** geçer (v2 — SystemAdmin + Yasal Danışman gibi iki onay)

**Soft-delete davranışı:**
- `IsDeleted = true`, `DeletedAt = now`, `DeletedBy = loginAccountId`
- Belge listede görünmez (varsayılan filter)
- Sadece SystemAdmin "Silinmiş belgeleri göster" filtresiyle görür
- Fiziksel dosya MinIO'da durmaya devam eder
- Retention süresi: **10 yıl** (MVP config — KVKK/KMK/SGK gereksinimlerine göre)

**Soft-delete geri yükleme (restore) — Karar 5 (kullanıcı onayı alındı):**
- **Silme yetkisi olan rol aynı zamanda geri yükleyebilir.**
- `site.document.delete` iznine sahip kullanıcı `restore` işlemi de yapabilir (aynı izin altı işlem — ayrı bir `restore` izni yok)
- Audit log her geri yüklemeyi kaydeder (`DocumentRestored` event, §10.13)
- Gerekçe: Yanlış silme hatasında destek ekibine iş düşmez, sorumlu kullanıcı hızlıca düzeltir

**KVKK Hassas Belge İşlemleri (v2):**
- Hassas belge (IsSensitive=true) görüntülenirken "Erişim sebebi" (Justification) sorulabilir
- SystemAdmin panelinde "KVKK Erişim Raporu" — kim hangi hassas belgeyi ne zaman neden açtı

**Retention ve fiziksel silme — Karar 6 (kullanıcı onayı alındı):**
- **MVP temel retention: 10 yıl** (KVKK + KMK + vergi + SGK gereksinimlerini kapsar)
- Soft-delete'ten **10 yıl sonra** → background job `PermanentDeletionJob` çalışır
- Sadece `DeletedAt + 10 yıl < now` olan belgeler fiziksel silinir (MinIO dosyası + DB kaydı)
- **v2 genişlemesi:** Kategori bazında farklı retention süreleri
  - `DocumentCategory.RetentionYears` alanı eklenecek
  - Örnek: Vergi belgeleri 5 yıl, SGK 10 yıl, Mahkeme Kararları 20 yıl, Kimlik Kopyaları KVKK silme talebine kadar
  - SystemAdmin belge bazında "kritik" işaretleyerek süreyi uzatabilir
- Retention süresi config'te tutulur (appsettings), koddan sabit değil

### 10.12. Şifreleme Stratejisi

**MVP seviyesinde:**

1. **Transport (taşıma) şifrelemesi:** HTTPS/TLS — standart, zaten var
2. **Server-Side Encryption at Rest (SSE-S3):** MinIO tarafında disk seviyesinde otomatik şifreleme. Tüm dosyalar şifreli diskte tutulur; uygulama MinIO'dan çekerken otomatik çözülür. Disk çalınma senaryosuna karşı koruma.

**v2 seviyesinde (hassas belgeler için):**

3. **Application-level encryption:** `IsSensitive = true` kategorilerdeki belgeler yüklemeden önce **AES-256-GCM** ile şifrelenir.
   - **Envelope encryption pattern:**
     - Her belge için unique DEK (Data Encryption Key) üretilir
     - DEK master key (Azure Key Vault — ADR-0008) ile şifrelenir
     - Şifrelenmiş DEK metadata'da tutulur
     - İndirme: master → DEK çöz → dosya çöz
   - **Document.IsEncrypted = true** olan belgeler uygulama tarafından şifrelenmiş
   - MinIO compromise olsa bile içerik güvende

4. **Justification (Erişim Sebebi):** Hassas belge açarken kullanıcıdan sebep sorulur, audit'e yazılır.

**Şifreleme dışı bırakılanlar (content search için):** v2'de full-text content search + IsEncrypted çelişir. Şifreli belgelerde içerik araması yapılamaz (sadece metadata). Bu trade-off kabul edilir.

### 10.13. Güvenlik ve Audit

Her belge işlemi `audit.entity_changes` tablosuna yazılır (ADR-0006):

| Event | Ne zaman |
|---|---|
| DocumentUploaded | Yeni belge yüklendiği zaman |
| DocumentDownloaded | Belge indirildiğinde |
| DocumentViewed | Belge önizlemesi açıldığında |
| DocumentMetadataUpdated | Başlık/açıklama/kategori değiştiğinde |
| DocumentSoftDeleted | Soft-delete |
| DocumentRestored | Soft-delete'den geri yükleme |
| DocumentHardDeleted | SystemAdmin kalıcı silme |
| SensitiveDocumentAccessed | IsSensitive belge açıldığında (özel event) |

**Retention:** audit kayıtları 10 yıl saklanır (ADR-0006).

### 10.14. Kısıtlar

**MVP sınırları:**
- Tek dosya maksimum: **25 MB** (config)
- İzinli MIME types: PDF, JPEG, PNG, DOCX, XLSX, ZIP
- Yasak: EXE, DLL, BAT, JS, diğer executable'lar
- Tek seferde tek dosya (multi-file upload v2)

**v2'ye bırakılanlar:**
- Virüs taraması (ClamAV)
- Otomatik thumbnail (PDF ilk sayfa, resim preview)
- OCR (taranmış belgelerin içerik indexlemesi)
- Büyük dosya multipart upload (100 MB+)
- Versiyonlama (aynı belgenin güncellenmiş hali)
- Tag sistemi (kullanıcı tag'leri)
- ZIP halinde toplu indirme
- Belge yorumu/notu
- Full-text content search
- Application-level encryption
- Justification (erişim sebebi)
- Hard delete onay zinciri
- DocumentLink tablosu (bir belge birden fazla entity'ye bağlanabilmesi)

### 10.15. Domain Davranışları

```csharp
public sealed class Document : AuditableAggregateRoot<DocumentId>, ISoftDeletable
{
    public static Document Upload(
        DocumentOwner owner,
        DocumentCategoryId categoryId,
        string title, string? description,
        string originalFileName,
        string mimeType, long sizeBytes,
        string storagePath, string checksum,
        LoginAccountId uploadedBy,
        UnitPeriodId? associatedPeriod,
        bool isPermanent,
        bool isEncrypted);

    public void UpdateMetadata(string title, string? description, DocumentCategoryId categoryId);
    public void SoftDelete(LoginAccountId by, string reason);
    public void Restore(LoginAccountId by);
    // Hard delete sadece SystemAdmin aggregate'i dışından (Infrastructure/SystemAdmin servisi)

    public bool CanBeAccessedBy(LoginAccountId user, IPermissionService perms);
}
```

**Aggregate boundary:** Document standalone aggregate — başka entity'lere FK yok (owner polimorfik string+Guid).

---

## 11. Kod Üretim Altyapısı

### 11.1. Seçilen Strateji: Sequence + Obfuscation (Feistel Cipher)

Organization, Site, Unit, UnitPeriod gibi entity'ler için URL'de kullanılan numeric kodlar **Sequence + Feistel Cipher obfuscation** yöntemiyle üretilir.

**Neden bu yöntem (rastgele retry yerine)?**

| Kriter | Rastgele + Retry | Sequence + Obfuscation |
|---|---|---|
| Çakışma riski | Retry gerekir (DB sorgusu) | **Yok** (matematik garanti) |
| DB sorgusu | Her üretim için 1 SELECT | **0 SELECT** — sadece sequence next |
| Tahmin edilemezlik | Kriptografik rastgele | Feistel ile gizlenmiş |
| Performance (yüksek yük) | Retry çakışma arttıkça yavaşlar | Sabit O(1) |
| Deterministic (test) | Zor | Kolay |
| Açıklanabilirlik | "Kod çakıştı, tekrar dene" | Matematiksel ispat — her sequence value → tek kod |

### 11.2. Feistel Cipher Nedir?

Feistel Cipher, sıralı bir sayıyı **bijection** (birebir örtüşme) ile görünüşte rastgele bir sayıya dönüştürür. DES, Blowfish, 3DES gibi kriptografik algoritmaların temelinde vardır.

**Özellikler:**
- Girdi ve çıktı aynı büyüklükte (aynı bit/basamak sayısı)
- Her girdi için **tek bir çıktı**, her çıktı için **tek bir girdi** (tersinir)
- "Key" ile parametrize edilir — key değişirse tüm eşleşme değişir
- Görsel olarak rastgele görünür (sıralı değil, tahmin edilemez)

**Basit mantığı:**
```
Input  (2N bit): [L | R]    (sol yarı L, sağ yarı R)
Round 1: L' = R,  R' = L XOR F(R, K1)
Round 2: L'' = R', R'' = L' XOR F(R', K2)
...
Output (2N bit): [L_final | R_final]
```

F(...) bir "round function" — hash, tablo lookup, modular arithmetic. Birkaç round sonra çıktı girdiye benzemez.

**Bijection garantisi:** Feistel cipher matematiksel olarak bijection garanti eder — yani:
- Sequence 1 → Obfuscated A
- Sequence 2 → Obfuscated B
- ...
- Sequence N → Obfuscated N (hiçbiri çakışmaz, hiçbiri atlanmaz)

6-haneli kod üretimi için 20-bit Feistel yeterli (2^20 = 1,048,576 > 999,999).

### 11.3. ICodeGenerator Arayüzü

```csharp
public interface ICodeGenerator
{
    /// <summary>
    /// Belirtilen entity için bir sonraki kodu üretir.
    /// Sequence'den next alınır, Feistel ile obfuscate edilir, aralığa normalize edilir.
    /// </summary>
    Task<long> GenerateAsync<T>(CancellationToken ct = default) where T : class;
}
```

### 11.4. Implementation Yaklaşımı

Her entity için PostgreSQL **sequence** tanımlanır:

```
CREATE SEQUENCE seq_organization_code START 1 INCREMENT 1;
CREATE SEQUENCE seq_site_code         START 1 INCREMENT 1;
CREATE SEQUENCE seq_unit_code         START 1 INCREMENT 1;
CREATE SEQUENCE seq_unit_period_code  START 1 INCREMENT 1;
```

Üretim akışı (pseudo-code):

```
GenerateCode(entity):
  1. seqValue = NEXTVAL(seq_X_code)           -- PostgreSQL, atomic
  2. obfuscated = FeistelCipher.Encrypt(seqValue, key_X, rounds=4)
  3. normalized = (obfuscated % (max - min + 1)) + min
  4. return normalized
```

**Key yönetimi:**
- Her entity için **ayrı key** (key_organization, key_site, ...)
- Key'ler sabit (kod içinde const veya config'te)
- Key rotate edilmez (edilirse tüm eski kodlar "kaybolur" — bu istenen bir durum değil)
- Key'ler minimum 128-bit (16 byte) rastgele

**Round sayısı:** 4 round (hem yeterli obfuscation hem hızlı — crypto-grade değil, "tahmin edilemez" yeterli).

### 11.5. Aralık Normalizasyonu Problemi

Feistel cipher N-bit alan içinde çalışır (örn. 20-bit = 0 ile 1,048,575). Ama biz 100001-999999 istiyoruz.

**Naive yaklaşım (modulo bias):**
```
normalized = (obfuscated % 900000) + 100000
```
Bu çalışır **ama hafif modulo bias** var — bazı değerler diğerlerinden biraz daha sık gelir. 900K üzerinden fark %0.01'den az — MVP için kabul edilebilir.

**Temiz yaklaşım (cycle walking):**
```
obfuscated = FeistelCipher.Encrypt(seqValue)
while (obfuscated >= 900000):         -- 100001-999999 = 900000 slot
    obfuscated = FeistelCipher.Encrypt(obfuscated)
normalized = obfuscated + 100001
```
- Eğer obfuscated değer 900000'den büyükse, tekrar Feistel'e sokup dönüş yapılır
- Bijection korunur, bias YOK
- Ortalama iterations: ~1.05 (hız kaybı ihmal edilebilir)

**Tavsiye:** Cycle walking — mathematik olarak temiz, performance farkı yok.

### 11.6. Kullanım Matrisi

| Entity | Min | Max | Slot | Sequence | Feistel bits |
|---|---|---|---|---|---|
| Organization | 100001 | 999999 | 900,000 | seq_organization_code | 20 bit |
| Site | 100001 | 999999 | 900,000 | seq_site_code | 20 bit |
| Unit | 1,000,001 | 9,999,999 | 9,000,000 | seq_unit_code | 24 bit |
| UnitPeriod | 111,111,111 | 999,999,999 | 888,888,889 | seq_unit_period_code | 30 bit |

**Kapasite:**
- 900K organization = yıllarca yeter
- 900K site = yıllarca yeter
- 9M unit = çok büyük ölçekte bile yeter
- 889M unit period = sonsuz denebilir

### 11.7. Sequence Aşımı (Wraparound)

Sequence max değere ulaşırsa (örn. 900K site açıldı):
- PostgreSQL sequence overflow error verir
- Bu çok uzak bir ihtimal ama yakalanmalı
- Monitoring: sequence kullanım oranı %80'e ulaşınca alarm
- Uzun vadede aralık genişletilir (7 haneli site kodu vs.)

### 11.8. Collision Senaryosu ve Neden Yok

Sequence her zaman farklı değer üretir (PostgreSQL atomic). Feistel bijection olduğu için farklı girdi → farklı çıktı. Dolayısıyla:
- **DB check gerekmez**
- **Retry gerekmez**
- **All-time unique** matematiksel garanti

**Soft-delete durumu:** Soft-delete edilmiş kayıtın kodu "tekrar kullanılamaz" — sequence geri gitmez, her çağrıda bir sonraki değer. Dolayısıyla soft-deleted kayıtlar dahil tüm geçmişte unique.

### 11.9. Test Edilebilirlik

Determinism avantajı — test'te:
```csharp
// Test'te sequence + key sabit
var generator = new FeistelCodeGenerator(sequence: mockSeq, key: "test-key");
var code1 = await generator.GenerateAsync<Site>();  // Always same result for sequence=1
```

Rastgele retry yaklaşımında test yazmak zor (mock RandomNumberGenerator gerekir), Feistel'de deterministik.

### 11.10. Güvenlik Tartışması

Feistel'in "crypto-grade" olmaması bir eksiklik mi?

**Hayır — çünkü amacımız farklı:**
- AES gibi crypto-grade cipher'lar **saldırgan değiştirse bile çözemesin** diye var
- Bizim amacımız **düzenli kullanıcı tahmin edemesin** (brute force yerine "sıradaki kod hangisi?" diyemesin)
- Feistel bunu yeterince sağlar

**Güvenlik URL tahmin edilemezlikten gelmez:** Authorization middleware (ADR-0011) her URL'yi kontrol eder. Birisi `/c/site/234567/` yazarsa bile yetkisi yoksa 403 döner. Kod tahmin edilebilir olsa bile erişim kontrol edilir. Feistel ek güvenlik katmanı, asıl güvenlik **authorization**.

### 11.11. Domain Entegrasyonu

Entity factory metodlarında kod üretimi:

```csharp
public sealed class Site : SearchableAggregateRoot<SiteId>
{
    public static async Task<Site> CreateAsync(
        // ... diğer parametreler
        ICodeGenerator codeGenerator,
        CancellationToken ct)
    {
        var code = await codeGenerator.GenerateAsync<Site>(ct);
        // ...
        return new Site(SiteId.New(), code, /* ... */);
    }
}
```

Kod bir kez atanır, **immutable** — Site yaratıldıktan sonra Code değişmez.

### 11.12. Migration Stratejisi

İlk migration'da:
1. Sequence'ler oluşturulur (`seq_organization_code`, ...)
2. Feistel key'leri `appsettings.json` veya Azure Key Vault'a konur (ADR-0008)
3. Entity'ler `Code` kolonu + unique index ile oluşturulur

Production'da key rotate EDİLMEZ — edilirse tüm eski kodlar "başka bir hikaye" anlatır (matematiksel olarak geçerli ama uygulama açısından problem). Bu kısıt dokümante edilir.

### 11.13. Alternatif: Rastgele + Retry (Reddedildi)

Önceki tasarımda **rastgele kod + DB check + retry** yaklaşımıydı:

```csharp
for (int i = 0; i < 20; i++) {
    var candidate = RandomInRange(min, max);
    if (!await ExistsAsync(candidate)) return candidate;
}
throw new CodeGenerationException();
```

**Dezavantajları:**
- Her üretim ek DB sorgusu
- Yoğun kullanımda retry sayısı artar
- 20 retry sonunda başarısızlık senaryosu (düşük ama var)
- Test deterministic değil

Sequence + Feistel tüm bu problemleri çözer. Seçim: **Sequence + Feistel.**

---

## 12. Address (Adres) Ortak Yapısı

### 12.1. Tablolar

```
countries (2 kayıt)
  └── regions (8 kayıt: 7 coğrafi + Türkiye Dışı)
        └── provinces (82 il)
              └── districts (958 ilçe)
                    └── neighborhoods (4125 mahalle)
```

### 12.2. addresses Tablosu

Her entity'nin "Tebligat Adresi" alanı bir `AddressId` FK'dir. Ortak tablo:

| Alan | Tip | Zorunlu |
|---|---|---|
| Id | Guid | ✓ |
| NeighborhoodId | FK | ✓ (bölge/il/ilçe buradan türetilir) |
| AddressLine1 | string(500) | ✓ (Açık adres 1) |
| AddressLine2 | string(500) | ✗ (Açık adres 2) |
| PostalCode | string(10) | ✗ (Neighborhood'dan default, override edilebilir) |

### 12.3. Neden ayrı tablo?

- Bir Person'un tebligat adresi, Organization'ın adresi, Site'nin adresi — hep aynı yapıda
- Tekrar kullanım (bir org'un ofisi taşındıysa adres kaydı update edilir, referanslar etkilenmez)
- Adres bir **Value Object** değil, **Entity** — çünkü ayrı yaşam döngüsü var (adres güncellenebilir, varlık kalır)

### 12.4. AddressFactory ve Cascading Dropdown

Frontend'de bölge → il → ilçe → mahalle kademeli seçim. Her adım bir sonraki dropdown'u yükler (deferred loading).

Backend `IAddressService`:
```csharp
Task<IReadOnlyList<RegionDto>> GetRegionsAsync(CancellationToken ct);
Task<IReadOnlyList<ProvinceDto>> GetProvincesByRegionAsync(RegionId id, CancellationToken ct);
Task<IReadOnlyList<DistrictDto>> GetDistrictsByProvinceAsync(ProvinceId id, CancellationToken ct);
Task<IReadOnlyList<NeighborhoodDto>> GetNeighborhoodsByDistrictAsync(DistrictId id, CancellationToken ct);
```

Bu veriler **cache**'te tutulur (ICacheStore — ADR-0016 gelecek) — nadiren değişir (yılda bir güncelleme).

---

## 13. Implementation Sırası (MVP)

ADR-0011 ile birlikte sıralı yol haritası:

**Faz 1 — Altyapı:**
1. `SiteHub.Contracts` projesi (ADR-0017 — ileride yazılacak)
2. `ApiResponse<T>`, `ApiError`, Exception middleware
3. `ICacheStore` + Redis implementation (ADR-0016 — ileride yazılacak)
4. `ICodeGenerator` + test'ler

**Faz 2 — Referans veriler:**
5. `Country`, `Region`, `Province`, `District`, `Neighborhood` entity + seed (CSV'den)
6. `Address` entity + CRUD
7. `Bank` entity + seed (CSV senden gelecek)
8. `SiteType`, `BuildingType`, `UnitType`, `HeatingType`, `MeterType`, `PaymentMethod`, `DocumentCategory`, `Currency` + seed

**Faz 3 — Kimlik (ADR-0011):**
9. `Person` + `NationalId` VO + test
10. `LoginAccount` + Identity
11. `Permission` + sync migration
12. `Role` + `RolePermission` + seed (sistem rolleri)
13. `Membership`
14. Login UI (tek input)
15. Kural kontrolü middleware
16. Session yönetimi (Redis + SignalR)
17. 2FA
18. Password reset
19. `audit.security_events`

**Faz 4 — Onay Zinciri (ADR-0013):**
20. Generic `IApprovable` + `ApprovalWorkflow` + `ApprovalPolicy`
21. Multi-step, multi-approver, SLA, vekalet
22. Dashboard (onay analitikleri)

**Faz 5 — Organizasyon:**
23. `Organization` + slug üretimi
24. `Branch` + merkez-şube kuralı
25. `BankAccount` (onay zinciriyle entegre)

**Faz 6 — Site:**
26. `Site`
27. `Plot` / `Parcel` / `Building` / `Unit` (yapı şeması)
28. `UnitPeriod` + kapatma akışı
29. `Shareholder` + hisse oranı doğrulama
30. `Tenant`

**Faz 7 — Servis:**
31. `ServiceContract`
32. Permission hesaplamasında servis sözleşmesinin hesaba katılması

**Faz 8 — Belge:**
33. `DocumentLibrary` + MinIO upload/download

Her fazda: domain test + integration test + basit UI (CRUD ekranı).

---

## 14. Alternatifler (Reddedilen)

### 14.1. Organization Tipleri için Ayrı Tablo
Yönetim ve Servis org'ları için ayrı tablolar. Reddedildi çünkü:
- %95 alanları aynı
- Org kullanıcıları her iki tipi yönetir — kod duplikasyonu olurdu
- Single table + discriminator daha esnek (ileride başka tipler eklenebilir: Denetçi, Danışman...)

### 14.2. BB için Code yok — sadece UnitNumber
URL'de `/units/A12` gibi. Reddedildi çünkü:
- UnitNumber binanın içinde unique (site-geneli değil)
- Farklı binalarda aynı numara olur — karışır
- Kod-tabanlı routing tutarlı olur (Organization, Site, Unit hepsi kod)

### 14.3. Shareholder = Membership
Hissedar bir Membership olarak tanımlansın. Reddedildi çünkü:
- Hisse oranı Membership'te olmaz (bağlam dışı)
- Dönem-tabanlı (zamansal) ilişkilendirme Membership'te zor
- Hissedar sisteme giremeyebilir — Membership LoginAccount gerektiriyor

### 14.4. Site VKN Opsiyonel
Reddedildi — senin kararın "zorunlu". Kendi VKN'si olmayan site nadirdir, bu durumda yönetici organizasyonun VKN'si girilir (domain rule değil, kullanıcı elle yapar).

### 14.5. 6 Haneli Kod Sıralı
Önceki versiyonda sıralıydı, son kararla **rastgele** yapıldı — URL güvenliği (tahmin edilemezlik) için.

---

## 15. Açık Konular (v2+)

- **İK Modülü (v5):**
  - İzin talep/onay sistemi
  - Bordro görüntüleme (PDF indir)
  - Çalışanın iş geçmişi görüntüleme
  - Mesai saati tanımlama
- **Puantaj ve Bordro Hesaplama (v6):**
  - Puantaj girişi
  - Bordro hesaplama motoru
  - SGK e-bildirge entegrasyonu
  - Maaş ödeme listesi → banka dosyası export
  - Vergi dilimleri, yasal kesintiler
- **Yönetim Transferi Tarihçesi (ADR adayı):** `site_management_history` tablosu + yetki geçiş algoritmaları + eski yönetim salt-okunur arşivi.
  - Transfer tarihinden önceki kayıtlar için eski yönetim sorumluluk dışında
  - Eski yönetim kendi dönemine ait verilere **salt-okunur** erişir:
    - Banka hesap bakiyeleri ve hareketleri (transfer tarihine kadar)
    - Malik/Sakin/Kiracı hesap bakiyeleri ve hareketleri
    - Firma/kurum borç-alacak bakiye ve hareketleri
  - Arşiv export — PDF/Excel/ZIP olarak indirilebilir
  - Veriler silinemez (arşivlidir)
  - Yeni yönetim tarihçeye okuma erişimi alır
- **Yapı Şeması Hesaplanan Alanlar:** Site/Ada/Parsel/Yapı seviyelerinde computed totals. Domain event zinciriyle senkronize edilir. MVP'de anlık query, v2'de denormalize kolon + event handler.
- **Aidat/Tahakkuk motoru (ADR-0021):** BB × Dönem × Aidat Planı → tahakkuk. DAF gibi servis firmalarının Excel import'u ile BB'lere borç yansıtması da burada.
  - **DebtAllocationMode enum değerleri:**
    - `ByShareholderPercent` — hisse oranına göre (Ahmet %60 = 600 TL, Ayşe %40 = 400 TL)
    - `SingleDebtAccount` — tek "hissedarlar borcu" (kim öderse öder)
    - Site bazında konfigüre edilir
- **Retroaktif Dönem Düzeltme (v2 kritik):** Malik/Sakin/Kiracı dönem tarihleri değişirse, önceki tahakkuklar yeniden hesaplanmalı. Borç tipine göre doğru dönem hedeflenir. `PeriodChangeRecalculationService`:
  1. Değişen dönem aralığındaki tüm tahakkuk kayıtları bulunur
  2. Her biri için "bu borç kimin dönemine yansır?" yeniden hesaplanır
  3. Borç yeniden yönlendirilir (eski kiracıdan yeni malik'e vb.)
  4. Gecikme cezaları yeniden hesaplanır
  5. Tüm değişiklikler audit.entity_changes'e yazılır
- **Cari Hesap modülü (ADR-0020):** Site'nin cari defteri, firma/kişi borç-alacak takibi.
- **Tahsilat:** Banka extract file import; Sanal POS entegrasyonu (her banka için ayrı mağaza/terminal no); Kurumsal tahsilat online (Web servis/API).
- **Kurumsal Tahsilat Entegrasyonları:**
  - Offline (FTP): Günlük/anlık dosya alışverişi
  - Online (API): Real-time tahsilat bilgisi + borç sorgulama
- **Hesap Ekstre Entegrasyonları:**
  - Manuel (el ile girilir)
  - Excel import
  - FTP
  - Web Servis/API
- **Bütçe modülü:** Yıllık/dönemlik bütçe planı, gider kategorileri.
- **Para iade onay akışı:** Shareholder ve Tenant'ın `RefundIban` alanları için onay zinciri entegrasyonu (ADR-0013 sonrası).
- **E-fatura entegrasyonu.**
- **Araç yönetim modülü** (otopark tahsisi, araç geçiş sistemleri).
- **AVM özel yönetim** (kira artış, dükkan değişikliği, ciro-bazlı ek aidat) — v3?
- **Mobil native app.**
- **BankAccountPurpose modülü:** Hesaba çoklu amaç atama.
- **DocumentLibrary v2 genişlemeleri:**
  - Full-text content search (PDF/DOCX içerik indexleme, PostgreSQL tsvector + GIN)
  - OCR (taranmış belgelerin metin çıkarımı, arama için)
  - Application-level encryption (AES-256-GCM, envelope encryption, Azure Key Vault)
  - Justification (hassas belge açarken erişim sebebi sorma + KVKK raporu)
  - Hard delete onay zinciri (SystemAdmin + Yasal Danışman)
  - Eski malik/kiracı için "readonly tarihçe erişimi" modu (1 yıl)
  - DocumentLink tablosu (bir belge birden fazla entity'ye bağlanabilmesi)
  - Versiyonlama
  - Tag sistemi
  - Multi-file upload + ZIP halinde toplu indirme
  - Otomatik thumbnail üretimi
  - Virüs tarama (ClamAV)
  - Belge yorum/notu
  - Kategori-bazlı farklı retention süreleri (`DocumentCategory.RetentionYears` — vergi 5 yıl, SGK 10 yıl, Mahkeme 20 yıl vb.)
- **Personel Özlük Belgeleri (v5 İK):** Çalışanların kimlik kopyası, iş sözleşmesi, özgeçmiş, sağlık raporu vb. v5 İK modülüyle beraber gelecek.

---

## 16. Karar Kaydı

Bu ADR'ı onaylayan: _________________
Tarih: _________________
Notlar: _________________
