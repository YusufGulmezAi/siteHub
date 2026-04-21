# SiteHub — Proje Durumu

> **Her yeni seansın başında bu dosyayı oku.** İçinde nerede kaldığımız, temel
> kararlar ve bir sonraki adım var. Detaylar ADR'lerde.

**Son güncelleme:** 2026-04-21

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
| E-P1 | Organization CRUD backend (Code, VKN zorunlu, endpoint'ler) | 🟡 LOKAL'DE (push yok) | — |

**Dikkat:** Faz E-P1 kodu senin lokal çalışan portalında ama **commit edilmedi**.
Portal `/api/organizations` endpoint'ini gösteriyor ama RLS olmadığı için henüz
bu şekilde üretime çıkamaz.

## 6. Sıradaki İş — **Faz E-Pre: RLS Implementation**

**Amaç:** Multi-tenant RLS altyapısını kur. Organization API'sini RLS uyumlu hale getir.

**Tahmini süre:** 4 iş günü

### Gün 1 — Tenant Context Infrastructure
- [ ] `ITenantContext` interface (Application)
- [ ] `CircuitTenantContext` implementation (Infrastructure, Blazor Server)
- [ ] URL path routing: `/c/{contextType}/{contextId}/...`
- [ ] Session ActiveContext genişletme (OrganizationId + SiteId + IsImpersonating)
- [ ] Default context seçimi (login sonrası en yüksek seviye)

### Gün 2 — Database RLS
- [ ] Migration: Tüm tenant-scoped tablolara `organization_id` / `site_id` kolon
- [ ] Migration: `ENABLE ROW LEVEL SECURITY` + `FORCE ROW LEVEL SECURITY`
- [ ] Migration: RLS policy'leri (organization-scoped + site-scoped)
- [ ] `TenantContextConnectionInterceptor` (her connection open'da set_config)
- [ ] Fail-closed test: set edilmezse hiçbir satır dönmez

### Gün 3 — Application Integration
- [ ] EF Core global query filter (defense-in-depth)
- [ ] Command/Query handler'lara tenant context wiring
- [ ] Admin impersonation API (`/api/admin/impersonation/start`, `/end`, `/status`)
- [ ] Banner component (Blazor, sabit pozisyon, kırmızı)
- [ ] `audit.impersonation_events` + `audit.impersonation_access` tabloları

### Gün 4 — Sıkılaştırma + Test
- [ ] Hangfire job tenant context (parametre bazlı)
- [ ] System Admin için zorunlu 2FA enforcement
- [ ] RLS violation integration testleri (gerçek PG ile)
- [ ] Impersonation audit testi
- [ ] Commit + push

## 7. Sıradaki Fazlar (Faz E-Pre sonrası)

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
