# Faz F.6 Cleanup — Application DTO'ları Contracts'a konsolide

**Tarih:** 2026-04-22
**Amaç:** Duplicate DTO problemini çöz. Contracts tek kaynak, Application tek yerden import.

## Değişen Dosyalar (6 adet)

| Dosya | Değişiklik özeti |
|---|---|
| `Application/Features/Organizations/GetOrganizationsQuery.cs` | `+using Contracts.Common + Contracts.Organizations` — `return new PagedResult<T>(items,...)` → `new PagedResult<T> { Items=..., ... }` init stil |
| `Application/Features/Organizations/GetOrganizationByIdQuery.cs` | `+using Contracts.Organizations` |
| `Application/Features/Sites/GetSitesQuery.cs` | `-using Application.Features.Organizations (PagedResult hack-import)` + `using Contracts.Common + Contracts.Sites` + init stil |
| `Application/Features/Sites/GetSiteByIdQuery.cs` | `+using Contracts.Sites` |
| `ManagementPortal/Endpoints/Organizations/OrganizationEndpoints.cs` | `+using Contracts.Common + Contracts.Organizations` (Application using'i kalır, Command'lar için) |
| `ManagementPortal/Endpoints/Sites/SiteEndpoints.cs` | `-using Application.Features.Organizations (hack)` + `using Contracts.Common + Contracts.Sites` |

## Silinecek Dosyalar (2 adet) — **MANUEL SİLİNİR**

Zip içinde silme talimatı yok (zip sadece ekleme/değiştirme yapıyor). İki dosyayı elle sil:

```powershell
cd D:\Projects\sitehub

Remove-Item "src\SiteHub.Application\Features\Organizations\OrganizationDtos.cs" -Force
Remove-Item "src\SiteHub.Application\Features\Sites\SiteDtos.cs" -Force
```

## Uygulama Adımları

```powershell
cd D:\Projects\sitehub

# 1) Zip'i aç
Unblock-File C:\Users\<sen>\Downloads\sitehub-f6-cleanup.zip
Expand-Archive -Path "C:\Users\<sen>\Downloads\sitehub-f6-cleanup.zip" `
    -DestinationPath "D:\Projects\sitehub\" -Force

# 2) Application DTO dosyalarını sil
Remove-Item "src\SiteHub.Application\Features\Organizations\OrganizationDtos.cs" -Force
Remove-Item "src\SiteHub.Application\Features\Sites\SiteDtos.cs" -Force

# 3) Build + test
dotnet build
dotnet test --no-build

# 4) Portal'ı başlat → manuel smoke test
dotnet run --project src\SiteHub.ManagementPortal
```

**Beklenen:** Build temiz, 146 test yeşil.

## Kritik Detaylar

### 1. PagedResult<T> init pattern
Contracts'taki `PagedResult<T>` **sealed class + required init** şeklinde:
```csharp
public sealed class PagedResult<T>
{
    public required IReadOnlyList<T> Items { get; init; }
    public required int Page { get; init; }
    public required int PageSize { get; init; }
    public required int TotalCount { get; init; }
    // computed: TotalPages, HasNext, HasPrevious
}
```

Application'daki eski versiyonu positional record'du (`(Items, TotalCount, Page, PageSize)` — alan sırası farklı). Yeni init pattern'e geçerken alan adları kullanıldı, sıra problemi yok.

### 2. JSON compatibility korundu
Eski PagedResult (record) ve yeni PagedResult (sealed class) aynı JSON şemasını üretir:
```json
{
  "items": [...],
  "page": 1,
  "pageSize": 20,
  "totalCount": 100,
  "totalPages": 5,
  "hasNext": true,
  "hasPrevious": false
}
```
Frontend zaten Contracts versiyonunu deserialize ediyordu → UI tarafında değişiklik yok, dokunulmadı.

### 3. Application Command/Query + Result tipleri ETKİLENMEDİ
Aşağıdakiler **aynen kalır, dokunulmaz:**
- `CreateOrganizationCommand`, `UpdateOrganizationCommand`, `ActivateOrganizationCommand`, `DeactivateOrganizationCommand`, `DeleteOrganizationCommand` + Result'ları
- `CreateSiteCommand`, `UpdateSiteCommand`, `ActivateSiteCommand`, `DeactivateSiteCommand`, `DeleteSiteCommand` + Result'ları
- Bütün `FailureCode` enum'ları

Sebep: Bunlar CQRS handler internal sözleşmeleri, dış dünyaya DTO değil. HTTP endpoint'ler bu Result'ları alıp kendi response type'larına (`CreateResponse`, `StatusResponse`) map'liyor — o response type'lar Endpoint dosyasında tanımlı, Contracts'a taşımaya gerek yok (küçük ve lokal).

### 4. Endpoint'lerde iki using var (ambiguity yok)
`OrganizationEndpoints.cs`:
```csharp
using SiteHub.Application.Features.Organizations;  // Command/Query tipleri
using SiteHub.Contracts.Common;                    // PagedResult<>
using SiteHub.Contracts.Organizations;             // DTO'lar
```
DTO isimleri aynı (`OrganizationListItemDto`) — eski Application versiyonu silindiği için artık tek tek tanım. Derleme temiz.

## Rollback (gerekirse)

Cleanup sonrası beklenmedik bir sorun çıkarsa:

```powershell
# Henüz commit yapılmadıysa
git checkout -- src/
git clean -fd src/

# Commit yapıldıysa
git revert HEAD
```

## Smoke Test Senaryosu

### Başarı kriterleri
1. **Login** → `/organizations` listesi yüklensin (paging + search çalışsın)
2. Bir firmaya **göz** ikonu → Detail sayfası (B.4) açılsın, 3 kart dolu
3. **Yeni Organizasyon** → form doldur → kaydet → listede görünsün
4. Bir firmanın **kalem** ikonu → form doldur → güncelle → değişiklik kaydedilsin
5. Detail sayfasında **Pasif Yap / Aktif Yap / Sil** flow'u çalışsın

### Potansiyel sürpriz noktaları
- `PagedResult.TotalPages` / `HasNext` / `HasPrevious` compute property'leri JSON'a dahil olur mu? Olmuyorsa UI pager yanlış sayfa sayısı gösterebilir. **Kontrol:** F12 Network tab'da `/api/organizations` response JSON'ı.
- SiteList tarafı test edilemez (B.3'te Site UI yok) — backend build yeşilse yeter.

## Commit Önerisi

```
Faz F.6 Cleanup: Application DTO'lari Contracts'a konsolide

Amac: Duplicate DTO problemini coz. Contracts tek kaynak, Application
tek yerden import. Gelecek entityler icin (Unit, Residency, Aidat)
ayni pattern: DTO sadece Contracts'ta tanimli.

Silinen:
- Application/Features/Organizations/OrganizationDtos.cs
- Application/Features/Sites/SiteDtos.cs

Degisen (6 dosya):
- Application handler'lari Contracts DTO'larini kullanir
- PagedResult<T> init pattern'ine (sealed class) geciş
- SiteEndpoints'te Organizations hack-import kaldirildi
- Endpoint dosyalari Contracts using'lerini ekledi

Etkilenmeyen:
- Command/Query + Result tipleri Application'da kalir (CQRS internal)
- Endpoint'lerde lokal RequestBody/Response record'lar
- 146 test + frontend (JSON sema ayni)

Kazanim:
- Tek DTO tanimi, manuel senkron ihtiyaci yok
- Gelecekte alan eklemek daha guvenli (tek yerde degisir)
- Contracts ResidentPortal icin de hazir (ayni shape paylasir)
```
