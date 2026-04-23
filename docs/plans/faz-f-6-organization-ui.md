# Faz F.6 — Organization UI (Blazor Server + MudBlazor)

**Süre:** 2026-04-21 → 2026-04-22 (3 seans)
**Durum:** Organization UI tam bitti. List + Form + Detail + Delete/Activate. Cleanup ile DTO tek kaynağa çekildi.

**Son commit:** `43752d5` (F.6 Cleanup)

## Bu Faz'da Neler Yapıldı

### F.6 A — Altyapı

**A.1 Geography read endpoint'leri** (`3d5417f`)
Site formunda IL/İlçe dropdown için backend hazırlığı. Read-only query endpoint'ler.

**A.2 HttpClient altyapısı** (`ff81635`)
- `IOrganizationsApi` + `ISitesApi` + `IGeographyApi` typed client interfaces
- `CookieForwardingHandler` — Blazor Server'da SignalR circuit cookie'lerini
  outgoing HttpClient'a taşır
- `AddSiteHubApiClients()` extension
- **Not:** Bu aşamada Contracts DTO'ları Application'dan duplicate edildi. Sonraki
  Cleanup seansında bu karar revize edildi — tek kaynak Contracts.

### F.6 B — Organization UI

**B.2 Organization List** (`40036b2`)
- `/organizations` sayfası
- MudDataGrid + ServerData (paging + search)
- Switch: "sadece aktif / hepsi"
- "+ Yeni Organizasyon" butonu + göz/kalem ikonları

**B.2 iyileştirme** (`120aa71`) — tek bir seansın uzun CSS/tema macerası:
1. `SiteHubDataGrid` reusable wrapper bileşeni (`Components/Shared/`)
2. Tema navy paleti (`#1E3A8A` primary)
3. Hardcoded CSS temizliği (`!important` anti-pattern düzeltmesi)
4. `MudBlazor.Translations 3.1.0` paketi — kendi localizer silindi
5. SiteHubDataGrid: Hideable, DragDropColumnReordering, ColumnsPanelReordering

**B.3 Form (Create + Edit)** (`dee59ff`)
- `/organizations/new` + `/organizations/{id}/edit` tek sayfa
- MudForm + validation (isim, ticari ünvan, VKN padleft, telefon mask, email)
- VKN: `CreateVknRelaxed` — checksum kaldırıldı (dev test rahatlığı)
- Telefon: `PatternMask("0(000) 000 00 00")` + CleanDelimiters
- VKN çakışma mesajı: "Bu VKN '{firma adı}' firmasında kayıtlı"
- Row click kaldırıldı, göz ikonu geçici edit'e yönleniyor (B.4'te düzeltildi)

**B.4 Detail + Delete/Activate UI + List fix** (`ac3e37c`)
- `/organizations/{id}` Detail sayfası: readonly 3 kart (Firma / İletişim / Sistem)
- Action bar: Listeye Dön + Düzenle + Aktif↔Pasif + Sil
- Durum badge'i: yeşil "Aktif" / turuncu "Pasif" (div+palette, MudChip workaround)
- `Components/Shared/Dialogs/ConfirmDialog.razor` — generic (Activate/Deactivate)
- `Components/Shared/Dialogs/DeleteConfirmDialog.razor` — zorunlu reason input
  (MudTextField, min 5 karakter, anlık validation, disabled buton)
- List fix: Göz ikonu artık `/organizations/{id}` (Detail), kalem ikonu `/edit`
- List Durum kolonu: MudChip → div badge (aynı workaround)
- `CreatedByName` / `UpdatedByName` gösterimi (audit info)
- Prerender auth tuzağına karşı `OnAfterRenderAsync(firstRender)` pattern

### F.6 Cleanup (`43752d5`)

**Neden:** F.6 A.2'de Contracts duplicate kararı alınmıştı. Organization +
Site için DTO'lar hem `Application/Features/Xxx/`, hem `Contracts/Xxx/`
altında iki kopya hâlindeydi. JSON düzeyinde çalışıyordu (aynı alanlar) ama
tip sistemi açısından iki ayrı tipti. Yeni alan eklenirken birini unutma
riski = sessiz bozulma.

**Ne yapıldı:**
- Silindi: `Application/Features/Organizations/OrganizationDtos.cs`,
  `Application/Features/Sites/SiteDtos.cs`
- Değişti: 4 query handler + 2 endpoint — using'leri Contracts'a döndü,
  LINQ projection'lar aynı kaldı (tip namespace'i fark)
- `PagedResult<T>` ctor stili değişti: positional record → sealed class init
  pattern (`new PagedResult<T> { Items = ..., Page = ..., ... }`)
- JSON şeması aynı → frontend değişmedi
- 146 test yeşil, build temiz

**Dokunulmayan:** Command/Query + Result tipleri Application'da kaldı (CQRS
internal sözleşmesi). Endpoint lokal RequestBody/Response record'ları kaldı.

**Karar:** Mapping için AutoMapper kullanılmaz; manuel extension method'lar
(`ToListItemDto()`, `ToDetailDto()`). Derleme-zamanı güvenliği + AOT uyumlu.
ADR-0017 F.6 sonunda bu kararı belgeler.

**Kazanım:** Tek DTO tanımı, senkron ihtiyacı yok. Gelecek entity'ler için
(Unit, Residency, Aidat, Transaction) aynı pattern. ResidentPortal için de hazır.

## Teknik Tuzaklar (detaylı PROJECT_STATE.md §5.5'te)

1. **Blazor Server prerender + HttpClient auth** — `OnInitializedAsync`'de
   auth gerektiren API çağrısı patlayabilir. Çözüm: `OnAfterRenderAsync(firstRender)`.
2. **MudBlazor `Hideable` default false** — grid-level parametreyle tek yerden aç.
3. **Hardcoded CSS `!important`** — MudTheme palette'ı bypass eder. Her zaman
   palette CSS variable kullan.
4. **Blazor CSS isolation** child component DOM'una ulaşmıyor — MudBlazor
   wrapper stilleri global `app.css`'e taşınmalı.
5. **MudBlazor 9.0.0 silent render fail (MudChip + MudBreadcrumbs)** — build hatası
   yok, DOM'a düşmüyor. Workaround: div + palette CSS variable. 9.1.x upgrade
   değerlendirilecek.
6. **Contracts DTO konsolidasyon derinliği:** Duplicate DTO'lar tehlikeli; JSON
   seviyesinde çalışır ama tip sistemi katı değildir. Tek kaynak ilkesi → F.6
   Cleanup'ta uygulandı.

## F.6 C Başlarken Okunacaklar

1. Bu dosya + `PROJECT_STATE.md` §6 (F.6 C alt parça tanımları)
2. `Components/Pages/Organizations/Form.razor` — Site Form'u benzer pattern
3. `Components/Pages/Organizations/Detail.razor` — Site Detail'ı tab yapılı
   varyantı olacak (Detail'den esinlenilecek, farklar: tab yapısı, permission-aware)
4. `Components/Shared/Dialogs/ConfirmDialog.razor` + `DeleteConfirmDialog.razor` —
   Site'ta aynen yeniden kullanılacak
5. `SiteEndpoints.cs` — Backend zaten hazır (F.3'te yazıldı, Cleanup sonrası temiz)
6. `ISitesApi` — 7 method mevcut (C.1'de `GetAllAsync` flat metodu eklenecek)

## Kararlar Özeti (detaylar ADR'ler ve PROJECT_STATE §5.5'te)

- [DECISION] VKN checksum dev'de kapalı, bankayla açılır
- [DECISION] Row click kaldırıldı, button-only navigation
- [DECISION] MudBlazor.Translations resmi paketi (kendi localizer silindi)
- [DECISION] Picker (favori + recent + AND arama) F.6 sonrasına ertelendi
- [DECISION] SiteHubDataGrid wrapper — tüm liste sayfaları için tek kaynak
- [DECISION] B.4: Destructive action'lar sadece Detail sayfasında (kazara silme riski ↓)
- [DECISION] B.4: Delete reason zorunlu (min 5 karakter, audit trail)
- [DECISION] B.4: Detail'de `DeletedAt` gösterilmez — DTO'da alan yok, 404 handle
- [DECISION] Cleanup: Contracts tek kaynak; Application DTO'ları silindi
- [DECISION] Cleanup: Manuel DTO mapping (AutoMapper yok) — ADR-0017 bekleyen
- [DECISION] Cleanup: Command/Query + Result tipleri Application'da kalır (CQRS internal)

## F.6 C'ye Geçiş Notu

Organization UI pattern'i **olgun** — SiteHubDataGrid + Form + Detail + Dialog'lar
Site UI'da aynen yeniden kullanılır. Yeni iş parçaları:

- Flat `/api/sites` endpoint + `OrganizationName` join (C.1)
- IL/İlçe cascading dropdown (C.3)
- Tab yapılı Detail (C.5) — permission-aware tab visibility
- Nav menüsüne "Siteler" girişi + Organization List'e "Siteler" action

Tahmini süre: 7-9 seans. Başlangıç: C.1 (backend ekleme + Contracts tamamlama).
