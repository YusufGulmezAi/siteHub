# SiteHub — Proje Durumu

> **Her yeni seansın başında bu dosyayı oku.** İçinde nerede kaldığımız, temel
> kararlar ve bir sonraki adım var. Detaylar ADR'lerde.

**Son güncelleme:** 2026-04-24 (F.6 C tamamlandı + Madde 3/5/6/9 + Kategori A — Organization & Site UI tam bitti, breadcrumb + permission + 401 handler hazır)

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
| F.6 C.1 cleanup | Flat `/api/sites` geri alındı | ✅ | `941f87f` |
| F.6 C.3 | Site Form (Create + Edit) + cascading Il/Ilce | ✅ | `e2f52b6` |
| F.6 C.4 | Site listesi (nested, org context URL'den) | ✅ | `38f7d5a` |
| F.6 C.2 | Permission altyapısı (context-aware, hibrit B-Cascade) | ✅ | `973dab5` |
| F.6 C.5a | Site Detail (Tab iskeleti + Ana Bilgiler full) | ✅ | `b53da9c` |
| F.6 M9 | Global 401 handler + session expired dialog | ✅ | `d4981eb` |
| F.6 M3 | Site Detail = Form birleştirme (tek sayfa Create+Edit) | ✅ | `c59a22c` |
| F.6 Kat-A | UI cilası ve menü sadeleştirme (9 madde) | ✅ | `4ba1ef0` |
| F.6 M6 | Organization Detail = Form birleştirme | ✅ | `51e40a9` |
| F.6 M5 | Breadcrumb standardı (shared component) | ✅ | `1849442` |

**Son commit:** `1849442` (2026-04-24)
**Test durumu:** Build temiz. Portal canlı çalışır durumda. Tüm smoke testler yeşil.

### F.6 Nerede Kaldı?

**F.6 C tamamlandı (2026-04-24).** Hem Organization hem Site için:

- **Detail = Form birleşimi (M3 Site, M6 Org):** Ayrı Form sayfaları silindi, Detail tek sayfada hem Create (`/new`) hem Edit (`/{id}`) mod'unu yönetiyor. Tab yapısı (Site 8, Org 3). Kaydet / İptal / Sil butonları tab altında, Sil solda kırmızı edit-only.
- **Permission altyapısı (C.2):** Hibrit B-Cascade — Organization → Site permission expansion login'de yapılır, cache'lenir. Her context için ayrı `PermissionSet`. `HasPermission` component'i UI'da permission-gated render yapar.
- **Global 401 handler (M9):** Singleton event service + session identifier filter. Cookie değeri ile circuit'leri izole eder, A kullanıcısının 401'i B'ye gitmez. 5 saniye countdown + `/auth/login` redirect.
- **Breadcrumb standardı (M5):** `SiteHubBreadcrumb` shared component. Subtitle1 font + 1px divider alt çizgi. Array parameter API.
- **UI cilası (Kat-A, 9 madde):** Başlık "Yönetim Firmaları", Aktif badge koyu yeşil, menü sadeleştirme, Aktif/Pasif switch DataGrid toolbar'ında.
- **DeleteConfirmDialog pattern:** Site "Siteyi Sil", Org "Yönetim Firmasını Sil" başlıkları. Reason zorunlu (min 5 karakter).

**F.6 kalan maddeler (sonraki seanslar):**

| Madde | İş | Boyut | Öncelik |
|---|---|---|---|
| M8 | Genel audit log sistemi (tüm Detail'lerde kullanılacak shared component + backend endpoint) | Büyük | Yüksek |
| M11 | Organization domain genişletme (Sözleşme tarihi, Hizmet başlama/bitiş, Ek süre 0-30 gün, Token süresi config) | Büyük | Orta |
| M9-URL | URL'de Feistel Code (GUID yerine, Org + Site + sonraki entity'ler) | Büyük | Orta |
| — | Pasife Çek / Aktife Al toggle butonu (Detail sayfalarında) | Küçük | Düşük |
| F.6 Kapanış | ADR-0011'e Hibrit B-Cascade ekle, ADR-0017 Manuel Mapping, duplicate TurkishNormalizer temizliği | Orta | Kapanış |

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

**11. PermissionSet JSON roundtrip — sealed class + Dictionary** [DECISION, F.6 C.2 `973dab5`]

Başlangıçta `PermissionSet` positional record olarak tanımlıydı, `Dictionary<string, HashSet<string>>` alanı vardı. Redis'te JSON'a serialize edilip deserialize edildiğinde `HashSet` tipini kaybediyordu (System.Text.Json varsayılan davranışı). Deserialize sonrası `HashSet` değil `List` olarak gelip null-ref patlıyordu.

**Çözüm:** `PermissionSet` **sealed class + init setter + Dictionary<string, HashSet<string>>** oldu (record yerine). `Has(scope, key)` metodu ile permission check yapılır. JSON deserialize doğru şekilde `HashSet` rebuild eder.

**Pattern olarak öğrendik:** Redis/JSON roundtrip yapan collection'lar için positional record + complex generic args (HashSet, ConcurrentDictionary vb.) **güvenilmez**. Sealed class + init setter daha deterministik.

**12. Blazor Server cross-scope event — Singleton + filter pattern** [DECISION, F.6 M9 `d4981eb`]

İlk denemede `IAuthenticationEventService` **Scoped** idi. HttpClient handler 401 yakalayınca event raise etti, ama MainLayout event'i hiç almadı — dialog açılmadı.

**Sebep:** Blazor Server'da HttpClient handler scope'u ile SignalR circuit scope'u **aynı DI scope değil**. Handler'ın aldığı event service instance'ı ile layout'unki farklıydı.

**Çözüm:** Event service **Singleton** oldu. Multi-user broadcast problemini çözmek için event'e `SessionIdentifier` (auth cookie değeri) eklendi. MainLayout `OnInitialized`'de `IHttpContextAccessor` ile kendi cookie değerini cache'ler, event geldiğinde filter: `if (e.SessionIdentifier != _mySessionIdentifier) return;`. `ConcurrentDictionary<string, DateTime>` ile session bazlı tek-tetikleme + 30 dk cleanup timer.

**Pattern olarak öğrendik:** Blazor Server'da HttpClient handler → UI bildirim için **mutlaka Singleton event service + session/circuit identifier ile filter**. Scoped değil.

**13. SiteHubDataGrid wrapper parameter adı: `OnRowClick` (RowClick değil)**

MudDataGrid native parameter adı `RowClick` (EventCallback). Ama SiteHubDataGrid wrapper'ında `OnRowClick` (EventCallback<T>) olarak expose edildi — direkt `T item` geçer, `DataGridRowClickEventArgs` değil.

```razor
<!-- DOĞRU -->
<SiteHubDataGrid OnRowClick="OnRowClick" ...>
private void OnRowClick(SiteListItemDto item) { ... }

<!-- YANLIŞ -->
<SiteHubDataGrid RowClick="OnRowClick" ...>
private void OnRowClick(DataGridRowClickEventArgs<SiteListItemDto> args) { ... }
```

Yanlış yazılırsa compile geçer ama **runtime'da exception**, tüm sayfa donuyor. SiteHubDataGrid kullanırken her zaman `OnRowClick` ve item parametresi.

**14. MudDataGrid child content `@if` bloğu içinde olamaz**

`<ToolBarContent>` gibi slot'ları koşullu render etmek için `@if (X) { <ToolBarContent>...</ToolBarContent> }` **compile hatası** verir (RZ9996). Razor compiler top-level item'ları tanıyamıyor.

**Çözüm:** Slot her zaman render edilsin, içeriği koşullu olsun:

```razor
<ToolBarContent>
    @if (X) { <span>İçerik</span> }
    @ToolBarContent <!-- RenderFragment parameter -->
</ToolBarContent>
```

**15. MudIconButton `@onclick:stopPropagation` + `OnClick` duplicate hatası**

Blazor'da `@onclick:stopPropagation="true"` direktifi generated `OnClick` parametresi ekliyor. MudIconButton'un kendi `OnClick` parametresiyle çakışıp compile hatası veriyor ("OnClick used two or more times").

**Çözüm:** MudIconButton'u `<span @onclick:stopPropagation="true">...</span>` ile sar. stopPropagation span'de kalır, button normal OnClick kullanır.

**Kullanım yeri:** DataGrid row click + row içindeki button click ayrımı (bir sütunda button varsa, button'a tıklamada satır navigation'ı tetiklenmesin diye).

**16. MudBlazor `ToolBarContent` slot — DataGrid dişlisi otomatik sağa**

MudDataGrid'in `<ToolBarContent>` slot'una content koyarsan, `ShowMenuIcon="true"` ile dişli ikonu **otomatik olarak toolbar'ın sağ ucuna** konur. Sen content'i soldan yaz, MudSpacer ile aradaki boşluğu doldur — dişli zaten sağda.

```razor
<ToolBarContent>
    <MudSwitch Label="Aktif" ... />
    <MudSpacer />
    <!-- Dişli burada otomatik görünür -->
</ToolBarContent>
```

F.6 Kat-A Madde A-b'de Organizations ve Sites list'leri bu pattern'e geçti (switch + dişli aynı satırda).

**17. PowerShell 5 `Set-Content -Encoding utf8NoBOM` yok**

PowerShell 5.1 (Windows default) `utf8NoBOM` encoding'i desteklemez, sadece `UTF8` (BOM ile). BOM git tarafından tolere edilir ama başlarda commit mesajlarında görülebilir. Pratik: `Set-Content -Encoding UTF8 ...` kullan, commit message dosyaları için BOM sorun değil.

**18. Set-Content heredoc vs array — Türkçe karakter güvenilirliği**

`Set-Content -Value @(...)` array formu heredoc (`@'...'@`)'dan daha güvenilir — Türkçe karakterler (İ, Ş, Ğ, Ü, Ö, Ç) korunuyor. Özellikle commit mesajları için `Set-Content -Value @("satır 1", "satır 2", ...)` tercih edilir.

**19. Yapıştırma sırasında `<` kaybolabiliyor — her generic syntax sonrasında doğrula**

Uzun komutları yapıştırırken PowerShell bazen `<` karakterini yutuyor. C# generic syntax (`List<T>`, `AddScoped<IService, TImpl>()`) etkilenir. Her yapıştırmadan sonra `Get-Content | Select-String` ile hızlı kontrol: `<` karakterleri yerinde mi?

**20. `dotnet run` profile bağımlılığı — ASPNETCORE_ENVIRONMENT env var gerekli**

`dotnet run --project ...` komutu `launchSettings.json`'u otomatik okumayabilir (Visual Studio VEYA `dotnet watch run` ise okur). Sonuç: `appsettings.Development.json` yüklenmez, connection string null, portal "ConnectionString has not been initialized" ile patlar.

**Çözüm:** `$env:ASPNETCORE_ENVIRONMENT = "Development"` session'a set et, veya kalıcı: `[Environment]::SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development", "User")`.

## 6. Sıradaki İş — **F.6 Kalan Maddeler**

F.6 C (Site CRUD UI) tamamlandı. Kullanıcı gözden geçirmesiyle 12 madde açıldı; yarısı bitti, 4 büyük iş bekliyor.

### F.6 Kalan (bekleyen maddeler)

- [ ] **Madde 8 — Genel Audit Log Sistemi** (Büyük, öncelikli)
  - **Kullanıcı kararı (2026-04-24):** Sistem Bilgileri kartı tamamen kaldırılsın, yerine audit tablosu gelsin. Toggle icon pattern (sarı = gizli, koyu kırmızı = görünür). Büyük iter seans (backend + frontend tek commit).
  - **Audit kullanıcı bilgisi kararı:** Snapshot (Yaklaşım B) — `user_id` GUID + `user_name` + **`user_email`** (yeni kolon). `user_name` = `persons.full_name`, `user_email` = `login_accounts.email`. UI'da "Kullanıcı" kolonu iki satırlı: üst isim, alt gri email.
  - **Bulunan bug (YENİ SEANSTA İLK İŞ):** Mevcut audit kayıtlarında `user_id` ve `user_name` **NULL geliyor** (canlı DB kontrol edildi, admin login-sonrası bile). `ICurrentUserService` audit interceptor'ına null dönüyor. Muhtemelen session context'ten user okuma sorunu. Düzeltilmeden M8 anlamsız — audit'in kimlik değeri yok.
  - **Keşfedilen dosyalar:**
    - `src/SiteHub.Domain/Audit/AuditEntry.cs` — entity (sealed class, AuditOperation enum: Insert=1, Update=2, Delete=3 soft, Restore=4, HardDelete=5)
    - `src/SiteHub.Infrastructure/Persistence/Interceptors/AuditSaveChangesInterceptor.cs` — SaveChanges interceptor, `ICurrentUserService` + `ICurrentConnectionInfo` kullanır, EntityType = `entry.Entity.GetType().Name` (namespace'siz kısa ad — "Organization", "Site", "LoginAccount")
    - `src/SiteHub.Infrastructure/Persistence/Configurations/AuditEntryConfiguration.cs` — EF Core mapping, ToTable "audit.entity_changes"
  - **Mevcut şema (migration öncesi):** `id, timestamp, entity_type, entity_id, operation, user_id, user_name, ip_address, user_agent, correlation_id, context_type, context_id, changes(jsonb)`. Index'ler: PK + timestamp + (entity_type, entity_id) + user_id + correlation_id — performans yeterli.
  - **ChangesJson format (interceptor'dan):**
    - Insert: `{ "__snapshot_after": { field1, field2, ... } }`
    - Update: `{ "fieldName": { "old": ..., "new": ... }, ... }` (camelCase field names)
    - Delete (soft): `{ "__snapshot_before": {...}, "reason": "..." }`
    - Restore: `{ "deletedAt_was": "..." }`
    - HardDelete: `{ "__snapshot_before": {...} }`
    - Hassas alanlar MASKELENMİŞ (password, token, TCKN partial)
  - **Planlanan mimari:**
    1. **Bug fix:** `ICurrentUserService` audit'e doğru user dolduracak şekilde düzelt
    2. **Migration:** `audit.entity_changes` tablosuna `user_email varchar(300)` ekle
    3. **Domain:** `AuditEntry` class'a `UserEmail` property ekle + ctor
    4. **Config:** `AuditEntryConfiguration` güncelle (yeni kolon mapping)
    5. **Interceptor:** Her audit kaydına `user_email` doldur (ICurrentUserService'ten)
    6. **Contract:** `namespace SiteHub.Contracts.Audit; public sealed record AuditLogEntryDto(Guid Id, DateTimeOffset Timestamp, Guid? UserId, string? UserName, string? UserEmail, string? IpAddress, string? UserAgent, int Operation, string EntityType, Guid EntityId, string? ChangesJson);`
    7. **Application:** `Features/Audit/GetAuditHistory/` slice + handler
    8. **API endpoint:** `GET /api/audit?entityType={x}&entityId={id}&page={n}&pageSize=10` (auth required, pagination, timestamp DESC)
    9. **Portal API client:** `IAuditApi.GetHistoryAsync(entityType, entityId, page, pageSize, ct)` + DI
    10. **Shared component:** `Components/Shared/EntityAuditLogTable.razor`
        - Parameters: `string EntityType`, `Guid EntityId`
        - Sayfa altında info icon toggle (Size.Small, sarı bg görünmezken, koyu kırmızı bg görünürken)
        - Tıklanınca alt panel açılır (MudCollapse)
        - MudDataGrid 10 kayıt/sayfa, timestamp DESC
        - Kolonlar: Tarih / Kullanıcı (2 satır: isim + email) / IP / Browser / Değişen Alanlar
        - "Değişen Alanlar" kolonu için ChangesJson parse → expandable (tıklayınca alan-alan old → new tablosu)
        - Insert/Delete için snapshot özet gösterim
    11. **Entegrasyon:**
        - `Organizations/Detail.razor` — Sistem Bilgileri kartı **kaldır**, altına `<EntityAuditLogTable EntityType="Organization" EntityId="@OrganizationId.Value" />`
        - `Sites/Detail.razor` — aynı şekilde `EntityType="Site"`
  - **Açık sorular (yeni seansta çöz):**
    - `ICurrentUserService` tam olarak nerede, neden NULL dönüyor? Blazor prerender + interceptor timing sorunu olabilir (M9'dakine benzer bir tuzak?)
    - Permission kontrolü ilk iterasyonda gevşek mi (auth yeterli mi) yoksa entity'ye göre mi?
    - ChangesJson'daki `__snapshot_before` / `__snapshot_after` UI'da nasıl okunur? İlk sürümde raw JSON göster, ileride formatla?
    - EntityType literal "Organization"/"Site" frontend'de nasıl gösterilir (Türkçe'ye map: "Yönetim Firması" / "Site")?

- [ ] **Madde 11 — Organization + Site Domain Genişletme** (Büyük)
  - **Organization** ve **Site** için AYNI alanlar eklenecek (kullanıcı kararı 2026-04-24):
  - Yeni alanlar: Sözleşme tarihi (Tarih), Hizmet başlama tarihi (Tarih), Hizmet bitiş tarihi (Tarih), Ek süre (0-30 gün, int)
  - Validation kuralları:
    - Sözleşme tarihi ≤ Hizmet başlama tarihi
    - Hizmet başlama tarihi ≤ Hizmet bitiş tarihi
    - Ek süre 0-30 arası
  - Organization Detail'in "Sözleşme & Hizmet" tab'ında form
  - Site Detail'in "Sözleşme & Hizmet" tab'ına ekle (şu an placeholder, ama tab iskelet'te yok — yeni tab eklemek gerekebilir)
  - Alt madde: **Token süresi parametrik** — config ayarları modülü (gelecek ADR)
  - Domain + migration (iki tablo için) + form UI (iki Detail için) + validator

- [ ] **Madde 9 — URL Feistel Code** (Büyük, mimari)
  - GUID yerine `Code` (Feistel encoded) URL'lerde: `/organizations/142857631/sites/271828183`
  - Tüm nav etkilenir: breadcrumb link'leri, `Nav.NavigateTo` çağrıları, satır tıklamaları
  - API endpoint değişikliği: `GET /api/organizations/{code}` veya alternatif endpoint
  - ISitesApi / IOrganizationsApi metod imzaları etkilenebilir
  - 404 senaryoları (geçersiz code)

- [ ] **Pasife Çek / Aktife Al** toggle butonu (Küçük)
  - Detail sayfalarında Sil'den ayrı bir aksiyon
  - Soft delete değil, `IsActive` flag
  - Backend `ActivateAsync` / `DeactivateAsync` metodları zaten var

### F.6 Kapanış Maddeleri

- [ ] **ADR-0011 güncelleme:** Hibrit B-Cascade kararı eklenecek (permission expansion login'de yapılır)
- [ ] **ADR-0017 (yeni):** Manuel DTO Mapping Pattern (F.6 Cleanup sonrası karar)
- [ ] **Duplicate TurkishNormalizer temizliği** — domain + application'da iki kopya var mı kontrol
- [ ] **PROJECT_STATE güncellemesi** (bu seansta yapılıyor)

### F.7 ve Sonrası

- [ ] F.7 Backup automation (pg_dump + WAL, Hangfire)
- [ ] G — Unit + Residency + Person hassas bilgi (column encryption)
- [ ] H — Aidat tahakkuk + tahsilat

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
- [x] **F.6 C** Site CRUD UI — C.2 Permission, C.3 Form, C.4 List, C.5a Detail, M3 birleşim, Kat-A UI cilası, M5 Breadcrumb, M6 Org Detail=Form, M9 401 handler → `941f87f...1849442`
- [ ] **F.6 Kalan:** M8 (audit log), M11 (Org domain), M9-URL (Feistel), Pasife çek, F.6 Kapanış ADR'ler
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

**F.6 Kalan (sonraki seanslar):**
- [ ] **Madde 8 — Genel audit log sistemi** (öncelikli). Shared component + backend endpoint, Org + Site Detail'lerinde Sistem Bilgileri'nin yerine gelecek.
- [ ] **Madde 11 — Organization + Site Domain genişletme.** Her ikisi için aynı alanlar: Sözleşme + Hizmet tarihleri + Ek süre + Token config. Kullanıcı kararı (2026-04-24).
- [ ] **Madde 9 — URL Feistel Code.** GUID yerine Code (tüm nav etkilenir).
- [ ] **Pasife Çek / Aktife Al** toggle Detail sayfalarında.
- [ ] SiteHubDataGrid: server-side filter (ROADMAP #3), export (#6), AND-search (#7) — F.6 sonrası tek iterasyonda.
- [ ] VKN checksum kontrolü (ilerde Gelir İdaresi servisiyle).

**F.6 kapanış ADR'leri:**
- [ ] **ADR-0011 güncelleme:** Hibrit B-Cascade kararı (permission expansion login'de).
- [ ] **ADR-0017 Manuel DTO Mapping Pattern** (Cleanup'ta alınan karar, §5 Teknik Öğrenim 10).

**Ön-iş (ertelendi, bloke değil):**
- [ ] **MudBlazor 9.0.0 → 9.1.x+ upgrade** değerlendirmesi. F.6 C boyunca workaround çalıştı, acil değil. Faz G öncesi bakılsın.

**Eski kalıntılar (devam ediyor):**
- [ ] RedisCacheStore self-heal anti-pattern (log error, don't delete) — Faz E-Pre içinde düzeltilecek
- [ ] SMS provider gerçek implementation (şu an NullSmsSender) — Faz F içinde
- [ ] Audit log'a kullanıcı IP + User-Agent ekleme — M8 kapsamında gelecek
- [ ] System Admin için zorunlu 2FA — Faz E-Pre içinde
- [ ] Backup automation (pg_dump + WAL archive) — Faz F.7

**Temizlik:**
- [ ] Duplicate TurkishNormalizer kontrolü (Domain + Application iki kopya var mı?)

## 14. Hafıza Yönetimi Notu

**Claude (asistan) hafıza yaşıyor:** Her yeni seans boş başlar. Önceki kararlar
bu dosyadan + ADR'lerden okunarak öğrenilir. Kullanıcı:
1. Her büyük karardan sonra ADR güncellenir/yeni ADR eklenir
2. Her seans sonunda bu dosya güncellenir (şimdiki durum, sıradaki iş)
3. Kod yorumları içinde ADR referansları var: `// ADR-0005: circuit-scoped`

Bu sistemi sürdürmek **hayati**.

## 15. Kritik Alan Adları ve Gotcha'lar (Claude için Cheat Sheet)

Bu bölüm Claude'un hafızasız başlarken **yanlış tahmin yapmaması** için. Çoğu
benzer ama sürekli unutulan detaylar. Yeni hata keşfedildikçe buraya eklenir.

### Contract Response Alan Adları

| Tip | Alan | Tip | Notu |
|---|---|---|---|
| `CreateOrganizationResponse` | `OrganizationId` | `Guid?` | ❌ `Id` değil |
| `CreateOrganizationResponse` | `Code` | `long?` | — |
| `CreateOrganizationResponse` | `FailureCode` | `string?` | — |
| `CreateSiteResponse` | `SiteId` | `Guid?` | ❌ `Id` değil |
| `CreateSiteResponse` | `Code` | `long?` | — |
| `OrganizationStatusResponse` | `Success` / `Code` / `Message` | — | `Id` yok |
| `SiteStatusResponse` | `Success` / `Code` / `Message` | — | `Id` yok |

**Redirect sonrası:** `Nav.NavigateTo($"/organizations/{result.OrganizationId}")` — SiteId veya OrganizationId, **asla `result.Id` değil**.

### Cookie Adları (CookieSchemes)

- `ManagementCookieName` = `.SiteHub.Mgmt`
- `ResidentCookieName` = `.SiteHub.Resident` (olması bekleniyor, yeri gelince kontrol)
- `DeviceIdCookieName` = (bkz. `CookieSchemes.cs`)

### SiteHubDataGrid Wrapper Parametreleri

- `T` — item tipi
- `ServerData` — `Func<GridState<T>, CancellationToken, Task<GridData<T>>>`
- `OnRowClick` — **`EventCallback<T>`** (❌ `RowClick` değil, ❌ `DataGridRowClickEventArgs` imza ALMA)
- `ToolBarContent` — `RenderFragment?` (opsiyonel, dişli otomatik sağa)
- `NoRecordsContent` — `RenderFragment?`
- `Filterable` — `bool` (default false)
- `PageSizeOptions` — `int[]` default `{10, 20, 50, 100}`
- `ReloadAsync()` — public metod, `_grid?.ReloadAsync()` ile çağrılır

### SiteHubBreadcrumb Kullanım

```razor
@using SiteHub.ManagementPortal.Components.Shared
<SiteHubBreadcrumb Items="@_breadcrumbItems" />

private SiteHubBreadcrumb.BreadcrumbItem[] _breadcrumbItems =>
    new[]
    {
        new SiteHubBreadcrumb.BreadcrumbItem("Yönetim Firmaları", "/organizations"),
        new SiteHubBreadcrumb.BreadcrumbItem("...", null)  // son item
    };
```

### Permission Service — HasPermission Component

```razor
<HasPermission Required="@Permissions.Organization.Create"
               ContextType="MembershipContextType.System">
    <!-- permission varsa render -->
</HasPermission>

<HasPermission Required="@Permissions.Site.Read"
               ContextType="MembershipContextType.Organization"
               ContextId="@orgId">
    <!-- org-scoped permission -->
</HasPermission>
```

- **System scope:** organization sahibi olmayan — tüm organization'larda geçerli
- **Organization scope:** `ContextId="@orgId"` zorunlu
- **Site scope:** `ContextId="@siteId"` zorunlu, org'un sitelerine cascade olur

### IAuthenticationEventService

- **Singleton** (Scoped değil — Blazor Server cross-scope sorunu)
- Event imzası: `Func<SessionExpiredEventArgs, Task>`
- `RaiseSessionExpiredAsync(string sessionIdentifier)` — filter için cookie değeri
- MainLayout her circuit kendi cookie değerini cache'ler, event'te eşleştirir

### Dialog Çağırma Pattern (Çok Kullanılan)

```csharp
// Generic confirm
var parameters = new DialogParameters<ConfirmDialog>
{
    { x => x.Message, "..." },
    { x => x.ConfirmText, "Evet" },
    { x => x.CancelText, "Hayır" },
    { x => x.ConfirmColor, Color.Warning },
    { x => x.ConfirmIcon, Icons.Material.Filled.Cancel }
};

// Delete with reason
var parameters = new DialogParameters<DeleteConfirmDialog>
{
    { x => x.Message, "\"X\" kaydını silmek istediğinize emin misiniz?" },
    { x => x.MinLength, 5 }
};

// Standart dialog options
var options = new DialogOptions
{
    CloseButton = true,
    MaxWidth = MaxWidth.Small,
    FullWidth = true,
    BackdropClick = false
};

// Delete dialog başlık örnekleri (F.6'dan):
// - Site: "Siteyi Sil"
// - Organization: "Yönetim Firmasını Sil" (❌ "Firmayı Sil" değil, ❌ "Organizasyonu Sil" değil)
```

### MudBlazor 9.0.0 Bilinen Workaround'lar

- **MudChip → div + inline style** (sessiz render fail)
- **MudBreadcrumbs → SiteHubBreadcrumb shared component** (sessiz render fail)
- **MudDataGrid child content `@if` içinde olamaz** → slot her zaman render, içerik koşullu
- **MudIconButton `Title` yok → lowercase `title`** (HTML native attribute)
- **MudTabs `PanelClass` yok → her panel'e `Class="pa-4"`**

### Blazor Server Tuzakları

- **Prerender + HttpClient:** `OnInitializedAsync` yerine `OnAfterRenderAsync(firstRender)` (auth cookie prerender'da taşınmıyor)
- **DelegatingHandler + Scoped service:** Çalışmaz — Singleton + filter pattern kullan
- **Row click + button click:** `<span @onclick:stopPropagation="true"><MudIconButton ...></span>`
- **Same-component nav re-init:** `OnParametersSetAsync` içinde route param değiştiyse reload

### PowerShell 5.1 Tuzakları

- `Set-Content -Encoding utf8NoBOM` **yok** — `UTF8` kullan (BOM git'te tolere edilir)
- Heredoc (`@'...'@`) Türkçe karakterde kararsız — `@("satır1", "satır2")` array formu tercih
- Yapıştırma sırasında `<` kaybolabilir — C# generic syntax sonrası doğrula (`<T>`, `AddScoped<X, Y>()`)
- `dotnet run` profile okumayabilir — `$env:ASPNETCORE_ENVIRONMENT = "Development"` set et

### Commit Disiplini (Tekrarlama Güvenliği)

Sıra:
1. `git status` → değişen/yeni dosyaları gör
2. `git add <paths>` → stage
3. `Set-Content commit-msg.txt -Encoding UTF8 -Value @(...)` → mesaj hazırla
4. `git commit -F commit-msg.txt` → **commit hash'i gör**, tag atmadan önce doğrula
5. `Remove-Item commit-msg.txt`
6. `git push`
7. `git tag f6-mX-done` (tag konvansiyonu: `faz-madde-durum`)
8. `git push origin <tag>`
9. `git log --oneline -5` → doğrula

Tag atmadan önce **her zaman** commit hash kontrolü — eski commit'e tag yapışmasın.
