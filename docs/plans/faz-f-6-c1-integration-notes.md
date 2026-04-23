# Faz F.6 C.1 — Flat /api/sites endpoint + OrganizationName + ISitesApi.GetAllAsync

**Tarih:** 2026-04-22 (veya devam eden seans)
**Scope:** Backend eklemesi — yeni flat endpoint, DTO'lara OrganizationName alanı, typed client'a yeni method.

## Değişen/Yeni 8 Dosya

| Dosya | Durum | Açıklama |
|---|---|---|
| `Contracts/Sites/SiteDtos.cs` | Değişti | `OrganizationName` alanı eklendi (List + Detail, OrganizationId'den sonra) |
| `Application/Features/Sites/GetSitesQuery.cs` | Değişti | Explicit Join + OrganizationName projection + alan sıralaması |
| `Application/Features/Sites/GetSiteByIdQuery.cs` | Değişti | Aynı pattern, tek site için |
| `Application/Features/Sites/GetAllSitesQuery.cs` | **YENİ** | Flat query (opsiyonel `organizationId` filter) |
| `Application/Features/Sites/SiteMappings.cs` | **YENİ** | Placeholder extension class (boş — ADR-0017 anticipation) |
| `ManagementPortal/Endpoints/Sites/SiteEndpoints.cs` | Değişti | `GET /api/sites` (flat) endpoint eklendi |
| `ManagementPortal/Services/Api/ISitesApi.cs` | Değişti | `GetAllAsync` method signature eklendi (ilk sıraya) |
| `ManagementPortal/Services/Api/SitesApi.cs` | Değişti | `GetAllAsync` implementation (OrganizationsApi pattern kopyası) |

## Kritik Değişiklikler

### 1. `SiteListItemDto` alan sıralaması değişti

Positional record'da alan eklemek = alan sırası değişiyor. Eski `.Select(new SiteListItemDto(...))` çağrıları kırılırdı — ama bu çağrılar sadece `GetSitesQuery` ve `GetSiteByIdQuery` içinde. Her ikisi de bu commit'te güncellenmiştir.

**Eski sıralama (12 alan):**
```
Id, OrganizationId, Code, Name, CommercialTitle, Address,
ProvinceId, DistrictId, Iban, TaxId, IsActive, CreatedAt
```

**Yeni sıralama (13 alan):**
```
Id, OrganizationId, OrganizationName, Code, Name, CommercialTitle, Address,
ProvinceId, DistrictId, Iban, TaxId, IsActive, CreatedAt
```

`SiteDetailDto` da aynı konumda `OrganizationName` aldı.

### 2. Explicit LINQ Join (Site entity'sinde navigation property yok)

`SiteConfiguration.cs`'de:
```csharp
builder.HasOne<Organization>()      // ← TYPE-ONLY, no property expression
    .WithMany()
    .HasForeignKey(s => s.OrganizationId)
```

Bu demek ki `Site.Organization` navigation property **tanımlı değil**. Dolayısıyla query'lerde `.Include(s => s.Organization)` veya `s.Organization.Name` kullanılamaz. Yerine explicit Join:

```csharp
.Join(_db.Organizations.AsNoTracking(),
      s => s.OrganizationId,
      o => o.Id,
      (s, o) => new { Site = s, OrgName = o.Name })
.Select(x => new SiteListItemDto(..., x.OrgName, ...))
```

PostgreSQL'de bu tek SQL JOIN olarak çalışır, performans kaybı yoktur.

### 3. RLS davranışı

Sites + Organizations iki ayrı tabloda RLS var. Join yapıldığında **her iki filtre** devreye girer:
- System Admin: RLS bypass → tüm org'lar + tüm site'lar
- Organization user: sadece kendi org'u + o org'un site'ları

Şu anda yalnız System Admin var, bu risk kabul edildi. Multi-tenant kullanıcı seed'i geldiğinde (Faz G+) test edilecek.

### 4. `GetAllSitesQuery` opsiyonel `organizationId` filter

UI bu parametreyi kullanmayacak (nested endpoint zaten var). Ama backend'de hazır dursun — gelecek use case'ler için (ör. admin cross-org dashboard).

## Uygulama

```powershell
cd D:\Projects\sitehub

# Zip'i aç
Unblock-File C:\Users\<sen>\Downloads\sitehub-f6-c1.zip
Expand-Archive -Path "C:\Users\<sen>\Downloads\sitehub-f6-c1.zip" `
    -DestinationPath "D:\Projects\sitehub\" -Force

# Build
dotnet build

# Test
dotnet test --no-build
```

**Beklenen:**
- Build temiz (ManagementPortal + Application + Contracts)
- 146 test hâlâ yeşil (DTO alan sıralaması UI'da Site sayfası henüz yok, test dosyalarında direct `new SiteListItemDto(...)` kullanımı yok)

## Manuel Test — Yeni Endpoint

Portal'ı başlat:
```powershell
dotnet run --project src\SiteHub.ManagementPortal
```

Tarayıcıda login sonrası, DevTools F12 → Console:

```javascript
// Flat endpoint — tüm siteler
fetch('/api/sites?page=1&pageSize=20', { credentials: 'include' })
  .then(r => r.json())
  .then(console.log);
```

**Beklenen response:**
```json
{
  "items": [
    {
      "id": "...",
      "organizationId": "...",
      "organizationName": "ABC Yönetim",
      "code": 619806,
      "name": "Yıldız Sitesi",
      ...
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 2,
  ...
}
```

Önemli kontrol: **`organizationName` alanı DOLU mu?** Boş veya null ise join çalışmıyor demektir.

Nested endpoint de test et (aynı alan gelsin):
```javascript
fetch('/api/organizations/<orgId>/sites', { credentials: 'include' })
  .then(r => r.json())
  .then(console.log);
```

Organization Detail sayfasını aç — hâlâ çalışmalı (bu değişiklik onu etkilemez, regresyon kontrolü).

## Bilinen İşler / İleri Planlar

- **C.3 Site Form:** IL/İlçe cascading dropdown — `IGeographyApi` zaten hazır, form bu API'yi çağıracak
- **C.4 Site Lists:** İki ayrı sayfa yazılacak, `GetAllAsync` + `GetByOrganizationAsync` kullanacaklar
- **C.5 Site Detail:** Tab yapısı — `OrganizationName` breadcrumb'da gösterilecek
- **ADR-0017:** F.6 sonunda yazılacak. `SiteMappings.cs` placeholder dosyası o ADR'ye referans olacak.

## Commit Önerisi

```
Faz F.6 C.1: Flat /api/sites endpoint + OrganizationName + ISitesApi.GetAllAsync

Yeni:
- Application/Features/Sites/GetAllSitesQuery.cs (flat, opsiyonel org filter)
- Application/Features/Sites/SiteMappings.cs (bos placeholder, ADR-0017 anticipation)
- ManagementPortal: GET /api/sites endpoint (FlatListQueryParams)
- ISitesApi.GetAllAsync method + SitesApi implementation

Degisen:
- Contracts/Sites/SiteDtos.cs: SiteListItemDto + SiteDetailDto'ya
  OrganizationName alani eklendi (OrganizationId'den sonra, 13 alan)
- Application/Features/Sites/GetSitesQuery.cs: Explicit Join + OrganizationName
- Application/Features/Sites/GetSiteByIdQuery.cs: Ayni pattern

Tasarim notlari:
- Site entity'sinde Organization navigation property yok
  (SiteConfiguration HasOne<Organization>() property'siz) — explicit Join
- OrganizationName zorunlu (non-null) — Organization her zaman mevcuttur (FK)
- Arama Organization adini kapsamaz — UI kolon gorsellik icin
- Flat endpoint'te RLS otomatik filtreler, System Admin tum org'lari gorur
- Opsiyonel organizationId parametresi hazir ama UI kullanmaz

Test: 146 test yesil, build temiz.
Smoke: /api/sites + /api/organizations/{id}/sites response'unda
       organizationName alani dolu gelir.
```
