# SiteHub — Proje Durumu

> **Her yeni seansın başında bu dosyayı oku.** İçinde nerede kaldığımız, temel
> kararlar ve bir sonraki adım var. Detaylar ADR'lerde.

**Son güncelleme:** 2026-04-22 (F.6 A.1 → F.6 B.4 + F.6 Cleanup tamamlandı — Organization UI tam bitti, DTO tek kaynakta)

---

## 1. Proje Bir Cümlede

Türkiye'de apartman/site yönetimi yapan firmalar için SaaS. Aidat tahakkuk,
tahsilat takibi, duyurular, karar defteri, iletişim. Bankacılık seviyesi
güvenlik. KVKK'ya tabi, veri Türkiye'de kalır.

## 2. Geliştirici Bağlamı

- **Yusuf Gülmez** — tek geliştirici (solo dev), en az 1-2 yıl tek başına
- Türkçe dokümantasyon + yorumlar, İngilizce kod tanımlayıcıları
- Windows 10 + PowerShell 5.1 + Docker Desktop
- Proje: `D:\Projects\siteHub\`
- GitHub: `https://github.com/YusufGulmezAi/siteHub` (public)
- Ticari şirket mevcut (LLC). Konu ticari üretim için uygun.

## 3. İş Modeli — Kritik

**SiteHub para tahsil ETMEZ.** Sadece yazılım sağlar. Tahsilat akışları:

1. **Open Banking:** Sakin bankaya yönlendirilir → para site hesabına gider
2. **PSP (iyzico/PayTR):** Kart bilgisi PSP'de, SiteHub sadece token görür

**Havuz hesap / escrow YAPILMAZ** (BDDK lisansı gerektirir, reddedildi).
Dolayısıyla BDDK kapsamında değiliz, sadece **KVKK**'ya tabiyiz.

Detay: ADR-0012 (Organizational Structure) içinde değinilir; düzenleyici
tarafı için ayrı ADR ileride gelecek.

## 4. Mimari Temeller (aklında tutman gerekenler)

### 4.a Katmanlar (ADR-0001)
Modular monolith + vertical slice. 5 proje:
- `SiteHub.Domain` — entity, value object, aggregate
- `SiteHub.Application` — CQRS (MediatR), abstractions, feature slices
- `SiteHub.Infrastructure` — EF Core, Redis, Hangfire, MailHog, vs.
- `SiteHub.ManagementPortal` — Blazor Server (yönetim portalı)
- `SiteHub.Shared` — cross-cutting (yoksa ihtiyaç duyulunca)

### 4.b Multi-Tenant Stratejisi (ADR-0002)
**PostgreSQL Row-Level Security (RLS)** + uygulama katmanı query filter
(defense-in-depth). Her tenant-scoped tabloda `organization_id` veya `site_id`
kolonu + RLS policy. Connection interceptor session variable set eder.

**Tablo kategorileri:**
- Global: `geography.*`, `identity.permissions`, `hangfire.*`, `identity.persons`, `identity.login_accounts`
- Organization-scoped: `identity.roles`, `identity.memberships`, `tenancy.sites`
- Site-scoped: `tenancy.units`, `tenancy.residencies`, `financial.*`, duyuru, karar defteri

### 4.c Context Switching (ADR-0005)
- Bir kullanıcı aynı tarayıcıda **farklı sekmelerde farklı context'lerde** çalışabilir
- Her Blazor Server circuit kendi scope'u → doğal izolasyon
- URL: `/c/{contextType}/{contextId}/...`
- ITenantContext circuit-scoped

### 4.c.ii Resident Portal — Cross-Tenant Özel Durum (ADR-0014 §1.a)
Sakinler **ayrı bir uygulamada** (`SiteHub.ResidentPortal`) çalışır:
- Cross-tenant view: bir kişi birden fazla yönetim firmasında daire sahibi olabilir
- Tek login → tüm residency'leri listelenir (ABC'de 3 no'lu + XYZ'de 12 no'lu)
- RLS policy'leri Resident için özel koşul içerir: `person_id = current_user's person_id`
- Yönetim personeli bilgilerine erişim YOK

### 4.d Admin Impersonation (ADR-0014)
System Admin RLS'yi bypass edebilir ama:
- Açık "destek modu" seçimi
- Kırmızı banner (kapatılamaz)
- Her işlem audit'e yazılır
- Süre sınırı (default 1 saat, max 4 saat)

### 4.e Identity (ADR-0003 + ADR-0011)
- Login: TCKN / VKN / YKN / Email (4 yöntem)
- Session: Redis
- 2FA: TOTP (Google Authenticator uyumlu) — System Admin için zorunlu olacak
- Membership → kullanıcı + context (System/Org/Site) + Role

### 4.f Güvenlik Yüklemi (production-ready)
- Password hashing (ASP.NET Identity v3)
- Session (Redis, IP/device binding)
- Login lockout (5 yanlış → config'e göre süre)
- 2FA rate limit (5 yanlış → lockout)
- Hangfire + token cleanup
- Audit log (basit seviye — `audit.entity_changes`)

### 4.g Eksik güvenlik (planda, henüz yok)
- ❌ Column-level encryption (TCKN, VKN, telefon, IBAN) — Faz G
- ❌ Audit hash chain (tamper detection) — Faz G
- ❌ Automated backup schedule — Faz F öncesi
- ❌ PITR (Point-In-Time Recovery) — Faz F öncesi
- ❌ Secret management (User Secrets / Vault) — prod deploy öncesi
- ❌ WAF / rate limit (API endpoint'leri) — prod deploy öncesi
- ❌ RLS infrastructure — **sıradaki iş (Faz E-Pre)**

## 5. Tamamlanan Fazlar

| Faz | Açıklama | Durum | Commit |
|---|---|---|---|
| 0 | Solution scaffolding, DI, config | ✅ | (ilk commit'ler) |
| A | Domain base types, audit interceptor | ✅ | |
| B | Geography seed (81 il, 973 ilçe, 50K+ mahalle) | ✅ | |
| C | Login + Session + ICurrentUser | ✅ | `f0e79e0` |
| D1 | Password Reset (email + SMS altyapısı) | ✅ | `8992f21` |
| D2 | 2FA TOTP (setup, verify, disable) | ✅ | `b9f5308` |
| D3 | Hangfire + 2FA rate limit + config-based lockout | ✅ | `5392585` |
| — | Menü temizliği + ROADMAP.md | ✅ | `444bf6d` |
| — | ADR'ler: RLS mimarisi + ADR-0014 + PROJECT_STATE | ✅ | `de79d8b` |
| E-P1 | Organization CRUD backend (Code, VKN zorunlu, 7 endpoint, 28 test) | ✅ | `943455a` |
| E-Pre G1 | ITenantContext + HttpTenantContext + api-tests.http | ✅ | `c02b495` |
| E-Pre G2-A1 | TenantContextConnectionInterceptor (session variable setup) | ✅ | `6b42da6` |
| E-Pre G2-A2 | `identity.roles` RLS policy + seed bootstrap mode | ✅ | `c43eb78` |
| E-Pre G2-A3 | Integration tests (Testcontainers) — ⚠️ SİLİNDİ | ❌ | — |
| E-Pre G2-A4 | `public.organizations` RLS policy | ✅ | `48c0b89` |
| — | Hijyen + Faz F.1 plani (ADR-0014 §8) | ✅ | `e51bc83` |
| F.1 | Site domain entity + factory + 45 unit test | ✅ | `6882fd8` |
| F.2 | Site EF Core config + migration (tenancy.sites) | ✅ | `9cb2296` |
| F.3 | Site CRUD backend (7 endpoint, nested REST) | ✅ | `f0942c5` |
| F.4 | HttpTenantContext Site→Org resolver (ISiteOrgResolver + IMemoryCache) | ✅ | `4164b72` |
| F.4 hf | SiteOrgResolver lazy DbContext (circular DI fix) | ✅ | `1e98eba` |
| F.5 | `tenancy.sites` RLS policy + Program.cs hijyen | ✅ | `d2a4443` |
| F.6 A.1 | Geography read endpoint'leri + hijyen | ✅ | `3d5417f` |
| F.6 A.2 | HttpClient altyapısı + typed API clients | ✅ | `ff81635` |
| F.6 B.2 | Organization listesi (ilk Blazor UI sayfası) | ✅ | `40036b2` |
| F.6 B.2* | SiteHubDataGrid + tema navy + lokalizasyon | ✅ | `120aa71` |
| F.6 B.3 | Organization Form (Create + Edit) + API CRUD | ✅ | `dee59ff` |
| F.6 B.4 | Organization Detail + Delete/Activate UI + List fix | ✅ | `ac3e37c` |
| F.6 Cleanup | Application DTO'ları Contracts'a konsolide | ✅ | `43752d5` |

**Son commit:** `43752d5` (2026-04-22)
**Test durumu:** 146 test yeşil. Build temiz. Portal canlı çalışır durumda.

### F.6 Nerede Kaldı?

- **Organization UI tam bitti.** List + Form + Detail + Delete/Activate + durum badge.
- Göz ikonu Detail sayfasına, kalem Edit'e ayrı ayrı gidiyor (B.4'te düzeltildi).
- Destructive action'lar (Sil/Pasif) sadece Detail sayfasında — kazara silme riski düşürüldü.
- Delete reason zorunlu (min 5 karakter, `DeleteConfirmDialog` yeniden kullanılabilir).
- `ConfirmDialog` + `DeleteConfirmDialog` generic — Site/Resident UI'da aynen çalışır.
- **Cleanup sonrası:** Application DTO'ları silindi, Contracts tek kaynak. `PagedResult<T>` init pattern (Contracts.Common). Tüm 146 test yeşil.
- **Site CRUD UI (F.6 C)** sıradaki iş — Organization pattern'i Site'a taşınacak.

### E-Pre G2-A3 (Integration Tests) — Neden Silindi?

Testcontainers + EF Core + PostgreSQL RLS kombinasyonunda timing sorunu.
`set_config(..., is_local=false)` session variable'ları test ortamında
beklenen şekilde davranmadı. Kök neden tespit edilemedi (olasılıklar: Npgsql
connection pool davranışı, EF Core DbCommand pipeline, session variable
propagation). **Asıl kanıt mevcut:** docker exec manuel test + portal login
+ API verify. Test altyapısı olgunlaşınca ayrı araştırma iş emri ile dönülecek.

### Faz F.4 Hotfix — Öğrenilen Ders (Circular DI)

F.4'ün ilk hâli teoride doğruydu ama runtime'da **sessiz deadlock** çıktı:
- `TenantContextConnectionInterceptor` her connection açılışında `ITenantContext` resolve ediyor
- `HttpTenantContext` constructor `ISiteOrgResolver` istiyor
- `SiteOrgResolver` constructor `ISiteHubDbContext` istiyor
- DbContext oluşturulurken **interceptor tekrar çağrılıyor** → döngü

Portal "Başlatılıyor..." yazdıktan sonra donuyordu. DI lock'a giriyor,
exception bile log'a düşmüyordu. Teşhis: `Console.WriteLine` probe'ları
(PROBE 1-10) ile aşama aşama nereye kadar gittiğini tespit.

**Çözüm (commit `1e98eba`):** SiteOrgResolver ctor'da `IServiceProvider`
inject edilir, DbContext ancak cache miss'te `GetRequiredService` ile
resolve edilir. O anda interceptor zaten tamamlanmış olur → döngü kırılır.

**Pattern olarak öğrendik:** Interceptor'da resolve edilen scoped service'ler,
DbContext gerektiriyorsa `IServiceProvider` lazy kullanmalı.

### Faz F.5 Dev-Ortam Notu (Migration Senkronizasyonu)

F.5'te migration uygulaması iki adımlı oldu:
1. Boş migration oluşturuldu (`dotnet ef migrations add AddRlsToSites`)
2. Zip ile SQL içeriği yazıldı, portal başlatılınca circular DI nedeniyle
   donuyordu
3. Circular fix öncesi: DB'ye SQL **manuel** uygulandı (`psql -f`),
   `__ef_migrations_history`'ye manuel `INSERT` atıldı
4. Circular fix sonrası: portal başlattığında "No migrations to apply"
   gördü ve seed geçti

**Dev-specific adım** — prod'da yapılmayacak. Production'da önce circular
fix pushlanmış olur, migration normal akışla uygulanır.

### Ertelenen Adımlar

- **E-Pre G2-A.2.b** `identity.memberships` RLS — login handler'ı
  `current_login_account_id` session variable'ı set edecek şekilde değişmeli.
  Dikkatli iş, ayrı seans.

### Faz F.6 Teknik Öğrenimleri + Kararlar

**1. Blazor Server prerender + HttpClient auth TUZAĞI** 🔥

`@rendermode InteractiveServer` default `prerender=true` — prerender aşaması
sırasında HttpContext cookie'leri outgoing HttpClient'a tam taşınmıyor.
Sonuç: auth'lu API çağrısı 401 döner, redirect HTML'i gelir, JSON deserialize
patlar (`'S' is an invalid start of a value`).

**Çözüm:** `OnInitializedAsync` yerine `OnAfterRenderAsync(firstRender)` +
`StateHasChanged()`. Interactive circuit kurulduktan sonra cookie düzgün gider.

**Not:** MudDataGrid `ServerData` callback'i zaten interactive render sonrası
çağrılır, ServerData kullanan sayfalar güvenli. Sorun **manuel OnInitializedAsync**
HttpClient çağrılarında.

**2. MudBlazor `Hideable` default FALSE**

Column görünürlük toggle (göz ikonu) çalışmıyordu. `PropertyColumn`'a
`Hideable="true"` vermek gerekiyor — **veya MudDataGrid-level** parametresiyle
tüm column'lara tek yerden: `Hideable="true"`.

**SiteHubDataGrid wrapper'da** grid seviyesinde açık, tüm liste sayfaları otomatik destekler.

**3. MudBlazor.Translations paketi > kendi localizer**

Kendi `TurkishMudLocalizer` yazmıştık (B.2). MudBlazor'un key adlandırması
tutarsız (bazı metinler dot notation, bazıları space, bazıları underscore).
Resmi community paketi `MudBlazor.Translations 3.1.0` tüm bu sorunu çözdü:
`builder.Services.AddMudTranslations()` tek satır.

**Karar:** Kendi localizer'ımızı yazmayız. MudBlazor.Translations güncel tutar.

**4. Hardcoded CSS `!important` tema'yı bypass etti**

`app.css`'de `.sitehub-sidebar { background: #1F2937 !important; }` vardı.
Bu `MudTheme.DrawerBackground` palette değerini ezdi, tema değişse bile
sidebar siyah kalıyordu.

**Pattern:** Blazor + MudBlazor'da renk için **her zaman** palette CSS
variable'lar kullan: `var(--mud-palette-drawer-background)`. Hardcoded hex +
`!important` anti-pattern.

**5. Blazor CSS isolation child DOM'a ulaşmıyor**

`SiteHubDataGrid.razor.css` dosyasına yazdığımız stiller MudDataGrid'in
render ettiği child DOM'a **uygulanmıyordu** (Blazor scope ID'leri sadece
direct rendered element'lere eklenir). MudBlazor wrapper component'ler
için özel style hedefleme mümkün değil.

**Çözüm:** Global `app.css`'e taşı ve `.sitehub-data-grid` class'ı ile scope'la.

**6. VKN validation — dev vs production kararı** [DECISION]

Organization VKN için checksum (Gelir İdaresi algoritması) **dev ortamda
gevşetildi**. `NationalId.CreateVknRelaxed` metodu: 10 hane + rakam + tamamı
sıfır değil, **checksum yok**.

**Sebep:** Rastgele 10 rakamla dev test yapılabilsin. Gerçek VKN tespiti
banka entegrasyonunda Gelir İdaresi servisi ile yapılacak.

**CreateVkn (checksum'lı) Site.cs + Identity için korundu** — sadece
Organization handler'ları Relaxed kullanıyor.

**EF Core converter de Relaxed kullanır** — DB'de checksum'dan geçmeyen
VKN'ler olabilir (seed + relaxed yazma), okurken patlamasın.

**İlerde tekrar açılacak iş:** ADR-0015 veya F.8+ fazlarda.

**7. Row click navigation KALDIRILDI (kurumsal UI patterni)** [DECISION]

MudDataGrid'te `RowClick` ile sayfaya yönlendirme kötü UX:
- Kolon seçmek için tıklarsan yanlışlıkla navigation
- Hover state + click navigation birbirine karışır
- Columns dialog input click'leri tabloya leak eder

**Pattern:** Satır **pasif**, sadece **action button'lar** (göz, kalem) navigation
yapar. List.razor'dan kaldırıldı.

**8. SiteHubDataGrid wrapper bileşeni — tek-yerden-özellik**

Tüm liste sayfaları bu wrapper'ı kullanır. Kazanımlar:
- Yatay scroll container (kolon taşma yok)
- Hideable column (grid-level)
- Drag-drop column reordering (header + dialog)
- Dense mode, hover, tema uyumu
- Pager Türkçe (MudBlazor.Translations ile otomatik)

**İlerde tek yerden eklenecek:** Server-side filter (ROADMAP #3),
Excel/PDF export (#6), AND-search (#7).

**9. MudBlazor 9.0.0 sessiz render fail (MudChip + MudBreadcrumbs)** 🔥

F.6 B.4'te Detail sayfası test edilirken keşfedildi: MudChip ve MudBreadcrumbs
bileşenleri build hatası vermeden DOM'a düşmüyor. Sayfanın diğer tüm bileşenleri
(MudText, MudStack, MudPaper, MudButton, MudIcon, MudLink) normal render oluyor.
B.2'de List.razor'un Durum kolonundaki MudChip'in de aslında görünmediği geç
fark edildi (kolon küçük olduğu için göze batmamış).

**Tetikleyici şüphelileri:**
- MudChip + `Icon` parametresi (Detail'de kullandığımız hâl)
- MudChip + `Variant.Filled` + ChildContent (List B.2'deki hâl)
- MudBreadcrumbs + `BreadcrumbItem(..., href: null, disabled: true)` son item

**Workaround (kalıcı, tema uyumlu):** `div + inline style + palette CSS variable`
(`var(--mud-palette-success)`, `var(--mud-palette-warning)` vs.). Detail'de pill
badge (Icon + label), List'te compact badge, breadcrumb `MudLink + MudIcon`
kombinasyonu.

**İlerde:** §13'e eklendi — MudBlazor 9.0.0 → 9.1.x+ upgrade değerlendirmesi
(F.6 C Site UI öncesi tercih edilir). Upgrade breaking değilse native bileşene
dönülebilir; kalırsa workaround pattern'i Site UI'da aynen uygulanır.

**10. Contracts DTO konsolidasyonu** 🔥 [DECISION, F.6 Cleanup `43752d5`]

F.6 A.2'de, Contracts paketi bağımsız shipping için tanıtıldığında, Application
ve Contracts'ta **aynı DTO'ların iki kopyası** oluşmuştu (`OrganizationListItemDto`,
`OrganizationDetailDto`, `SiteListItemDto`, `SiteDetailDto`, `PagedResult<T>`).
JSON seviyesinde çalışıyordu çünkü alanlar aynıydı, ama tip sistem olarak
iki ayrı tipti. Tehlike: alan eklerken birini unutmak.

**Karar (F.6 Cleanup):** Contracts tek kaynak. Application DTO'ları silindi.
Application query handler'ları `Contracts.Xxx.YyyDto` döndürür (namespace değişimi,
LINQ projection aynen kaldı — `.Select(o => new Contracts.Dto(...))`).

**PagedResult<T> değişikliği:** Application versiyonu positional record idi.
Contracts versiyonu sealed class + required init. Ctor stili farklı → handler
return'lerinde `new PagedResult<T> { Items = ..., Page = ..., ... }` init
pattern'ine geçildi. JSON şeması aynı → frontend etkilenmedi.

**Etkilenmeyen:** Command/Query + Result tipleri Application'da kalır (CQRS
internal sözleşmesi, dış dünyaya DTO değil). Endpoint lokal RequestBody/Response
record'lar da Endpoint dosyasında kalır (küçük, lokal concern).

**Mapping stratejisi:** Manuel extension method'lar (`SiteMappings.ToListItemDto()`).
AutoMapper kullanılmaz (derleme-zamanı güvenliği, AOT uyumlu, debug kolay). Şu an
projection'larda inline `new Dto(...)` yeterli, extension'lar in-memory entity →
DTO için ihtiyaç doğduğunda yazılır.

**ADR-0017 (planlı):** F.6 sonunda "Manuel DTO Mapping Pattern" ADR'si yazılır.
Geriye dönük olarak bu kararı belgeler.

**Kazanım:** Tek tanım, manuel senkron gereksiz. Gelecek entity'ler (Unit,
Residency, Aidat, Transaction) için aynı pattern — DTO sadece Contracts'ta.
ResidentPortal Contracts'ı hazır (ortak shape paylaşır).

## 6. Sıradaki İş — **Faz F.6 C: Site CRUD UI**

**Amaç:** Organization pattern'i Site'a aynen uygula. Backend F.3'te hazır,
MudBlazor 9.0.0 chip/breadcrumb workaround'u Organization tarafında test edildi,
Site UI'da direkt yeniden kullanılır.

### F.6 C Alt Parçaları (7-9 seans toplam tahmin)

- [ ] **F.6 C.1** — Flat `/api/sites` endpoint + `OrganizationName` alanı + `ISitesApi.GetAllAsync`
- [ ] **F.6 C.2** — (ertelendi, C sonuna) Permission altyapısı hazırlığı + seed
- [ ] **F.6 C.3** — Site Form (Create + Edit) + IL/İlçe cascading dropdown
- [ ] **F.6 C.4** — Site List × 2 (flat `/sites` + nested `/organizations/{id}/sites`) + menu girişi + Organization List'e "Siteler" action
- [ ] **F.6 C.5** — Site Detail (tab yapılı) + permission-aware tab visibility
- [ ] **F.6 C.6** — PROJECT_STATE güncelleme + ADR-0017 (Manuel Mapping) + faz kapanış

### F.6 C Temel Kararlar

- **İki liste sayfası:** flat `/sites` (Organization kolonu var) + nested `/organizations/{orgId}/sites` (Organization kolonu redundant, yok). Aynı SiteHubDataGrid paylaşılır.
- **Site Detail tab yapısı:** Ana Bilgiler (default) + Bankalar&Hesaplar + Muhasebe Parametreleri + Banka Parametreleri + Genel Parametreler. İlk faz'da sadece Ana Bilgiler dolu; diğer tab'lar permission-aware placeholder (Faz H/I'da dolacak).
- **Permission altyapısı ertelendi (C.2 sona):** Tab check'leri placeholder olarak yazılır, gerçek permission retrofit C sonunda.
- **Cross-site rapor:** Organization context'inde ayrı kategori (Hibrit Pattern A). Site-bazlı raporlar Site context'inde, konsolide raporlar Organization context'inde.
- **Multi-tab davranışı:** Context switching her zaman URL navigate ile (Ctrl+Click yeni sekmede başka site açma doğal çalışır). ADR-0005 uyumlu.

### F.6 C Öncesi — Tercih Edilen Ön-İş

- [ ] **MudBlazor 9.0.0 → 9.1.x+ upgrade** değerlendirmesi (30 dk.). Bilinen issue:
      MudChip/MudBreadcrumbs sessiz render fail (§5 Teknik Öğrenim 9). Upgrade
      breaking değilse native bileşenlere dönülür; kalırsa workaround pattern'i Site UI'da aynen uygulanır.

## 7. Faz F Kalan Alt Parçaları

- [x] **F.1** Site domain entity + 45 test → `6882fd8`
- [x] **F.2** EF Core config + migration → `9cb2296`
- [x] **F.3** Site CRUD backend (nested REST) → `f0942c5`
- [x] **F.4** HttpTenantContext Site→Org resolver → `4164b72` + `1e98eba`
- [x] **F.5** `tenancy.sites` RLS policy → `d2a4443`
- [x] **F.6 A.1** Geography read endpoint'leri → `3d5417f`
- [x] **F.6 A.2** HttpClient altyapısı → `ff81635`
- [x] **F.6 B.2** Organization List + SiteHubDataGrid + tema + lokalizasyon → `40036b2` + `120aa71`
- [x] **F.6 B.3** Organization Form (Create + Edit) + API CRUD → `dee59ff`
- [x] **F.6 B.4** Organization Detail + Delete/Activate UI + List fix → `ac3e37c`
- [x] **F.6 Cleanup** Application DTO'ları Contracts'a konsolide → `43752d5`
- [ ] **F.6 C** Site CRUD UI (Organization pattern'i tekrar uygulama, 5 alt parça) ← **BURADAYIZ (C.1)**
- [ ] **F.7** Backup automation (pg_dump + WAL, Hangfire)

## 8. Sıradaki Fazlar (Faz F sonrası)

| Faz | İş |
|---|---|
| E | Organization CRUD UI (MudDataGrid liste, form, detay) |
| F | Site CRUD (backend + UI) + ilk backup schedule (pg_dump) |
| G | Unit + Residency + Person hassas bilgi (column encryption buraya gelir) |
| H | Aidat tahakkuk + tahsilat (finansal modül) |
| I | Duyurular + Karar Defteri + Talepler |
| J | iyzico/PayTR entegrasyonu |
| K | Open Banking entegrasyonu |
| L | Resident Portal (sakin mobil/web) |

## 8. Sistem Kullanıcıları (Dev ortam)

### System Admin (seeded)
- **TCKN:** 10000000146
- **Email:** admin@sitehub.local
- **Parola:** Admin123!
- **2FA:** Opsiyonel (sistem admin'e zorunluluk Faz E-Pre'de gelir)
- **Rol:** Sistem Yöneticisi (tüm 32 permission)

### Sistem Rolleri (seeded, 8 adet)
1. Sistem Yöneticisi (System scope)
2. Sistem Destek (System scope)
3. Organizasyon Sahibi (Organization scope)
4. Organizasyon Yöneticisi (Organization scope)
5. Organizasyon Muhasebeci (Organization scope)
6. Site Yöneticisi (Site scope)
7. Site Teknisyen (Site scope)
8. Servis Firması Yöneticisi (ServiceOrganization scope)

## 9. Teknik Stack

- **.NET 10** (preview)
- **Blazor Server** + MudBlazor 9.0
- **PostgreSQL 17** (dockerized, user: `sitehub`)
- **Redis 7** (session, cache, rate limit state)
- **Hangfire** (background jobs, SQL storage)
- **MailHog** (SMTP dev — port 1025, UI 8025)
- **MinIO** (S3-compatible object storage, Faz H'den sonra)
- **Seq** (structured logging UI)
- **Docker Compose** (hepsi bir arada)

### Connection strings (dev)
- Postgres: `Host=localhost;Port=5432;Database=sitehub;Username=sitehub;Password=...`
- Redis: `localhost:6379;password=sitehub_dev_pw_change_me`

## 10. Çalışma Akışı

### Yeni seansa başlarken — benim (Claude) için

1. **Bu dosyayı oku** (PROJECT_STATE.md) — tam bu dosya
2. **ADR'leri tara** (`docs/adr/` klasöründeki 14 dosya)
3. **Son commit'leri gör:** `git log --oneline -10`
4. **Mevcut `git status`**'u sor
5. Sonra kullanıcıya "nereden devam ediyoruz?" diye sor

### Geliştirme döngüsü

1. **Plan:** Feature → ADR gerekli mi? Gerekiyorsa önce ADR yaz
2. **Kod:** Delta zip olarak ver (full zip değil, Windows MOTW unblock)
3. **Build:** Kullanıcı `dotnet build` → hata varsa düzelt
4. **Test:** Kullanıcı test eder, sonuç paylaşır
5. **Commit:** Anlamlı mesaj + push
6. **PROJECT_STATE.md güncelle:** Yeni durum yazılır

### Zip konvansiyonları

- Sadece **değişen** dosyalar (delta)
- Windows MOTW için kullanıcı `Unblock-File` sonra `Expand-Archive -Force`
- Proje kök dizinini bozmaz, overwrite yapar

## 11. Önemli ADR'ler (referans)

| ADR | Konu | Önemi |
|---|---|---|
| 0001 | Modular monolith + vertical slice | Katman yapısı |
| 0002 | **Multi-tenancy RLS** (revize) | 🔥 Kritik |
| 0002 (eski) | Schema-per-Tenant | Süperseded |
| 0003 | Identity strategy | Login pattern |
| 0004 | Blazor render modes | UI tarafı |
| 0005 | **Context switching + multi-tab** | 🔥 Kritik |
| 0006 | Logging + PII masking | Audit temeli |
| 0007 | Timezone (UTC storage, TR display) | Hesap doğruluğu |
| 0008 | Secrets management | Prod öncesi |
| 0009 | Turkish culture + collation | Arama/sıralama |
| 0010 | Search strategy | SearchText pattern |
| 0011 | Identity + authorization | Role/permission |
| 0012 | Organizational structure | Tenant hiyerarşisi |
| 0013 | Approval workflow | Sonraki fazlar |
| 0014 | **Multi-tenant isolation details** | 🔥 Kritik |

## 12. Kısıtlamalar ve İlkeler

- **Güvenlik > hız.** "Sonra ekleriz" yaklaşımı reddedilir.
- **Yedekleme ve veri bütünlüğü > yeni feature.**
- **ADR'siz karar olmaz.** Mimari değişiklik → ADR güncellenir.
- **Türk dili ve kültürü** birinci sınıf vatandaş (collation, normalizer, format).
- **Lisans maliyeti 0.** Açık kaynak + self-hosted.
- **Delta zip** + detaylı Türkçe açıklama — kullanıcı deneyimi öncelikli.
- **Tek seansı değerli kıl** — o seansın sonunda ya commit ya net bir delta paketi.

## 13. Bilinen Bug'lar / İşler (bekleyen)

**F.6 C içinde yapılacak:**
- [ ] Site CRUD UI (F.6 C, 5 alt parça) — buradayız, C.1'den başla
- [ ] SiteHubDataGrid: server-side filter (ROADMAP #3), export (#6), AND-search (#7) — F.6 sonrası tek iterasyonda tüm liste sayfaları kazanır
- [ ] VKN checksum kontrolü (ilerde Gelir İdaresi servisiyle açılacak)

**Ön-iş (F.6 C öncesi tercih edilen):**
- [ ] **MudBlazor 9.0.0 → 9.1.x+ upgrade** değerlendirmesi. Bilinen sessiz render fail (MudChip + MudBreadcrumbs) — §5 Teknik Öğrenim 9. Upgrade breaking değilse native bileşenlere dön.

**F.6 sonrası yazılacak ADR'ler:**
- [ ] **ADR-0017 Manuel DTO Mapping Pattern** (Cleanup'ta alınan karar, §5 Teknik Öğrenim 10). F.6 sonunda yazılır.

**Eski kalıntılar (devam ediyor):**
- [ ] RedisCacheStore self-heal anti-pattern (log error, don't delete) — Faz E-Pre içinde düzeltilecek
- [ ] SMS provider gerçek implementation (şu an NullSmsSender) — Faz F içinde
- [ ] Audit log'a kullanıcı IP + User-Agent ekleme — Faz E-Pre içinde
- [ ] System Admin için zorunlu 2FA — Faz E-Pre içinde
- [ ] Backup automation (pg_dump + WAL archive) — Faz F öncesi MUTLAKA

## 14. Hafıza Yönetimi Notu

**Claude (asistan) hafıza yaşıyor:** Her yeni seans boş başlar. Önceki kararlar
bu dosyadan + ADR'lerden okunarak öğrenilir. Kullanıcı:
1. Her büyük karardan sonra ADR güncellenir/yeni ADR eklenir
2. Her seans sonunda bu dosya güncellenir (şimdiki durum, sıradaki iş)
3. Kod yorumları içinde ADR referansları var: `// ADR-0005: circuit-scoped`

Bu sistemi sürdürmek **hayati**.
