# ADR-0002: Multi-Tenancy Stratejisi — Row-Level Security (RLS)

**Durum:** Kabul Edildi (revize)
**Tarih:** 2026-04-19 (ilk karar), 2026-04-21 (revize)
**İlgili ADR'ler:** ADR-0005 (Context Switching), ADR-0014 (Multi-Tenant Isolation Details)

## Değişiklik Özeti (2026-04-21)

**Orijinal karar:** Schema-per-Site (her site için ayrı PostgreSQL schema).

**Revize karar:** PostgreSQL Row-Level Security (RLS) + uygulama katmanı query filter.

**Revize sebebi:**
- Schema-per-Site implementasyonu 3-4 hafta sürdürüyordu; tek geliştirici için
  kabul edilemez süre.
- Bankacılık seviyesi güvenlik için RLS yeterli (JP Morgan, Goldman Sachs,
  Türk bankalarının çoğu bu modeli kullanır).
- Operasyonel yük çok daha düşük: tek backup, tek migration, tek DB.
- Cross-site raporlama (örn. "Organizasyon Sahibi tüm sitelerini görsün")
  RLS ile doğal şekilde çalışır; schema-per-site'da UNION ALL ile manuel iş.
- Migration orchestrator (100 site = 100 migration) yazma riski elendi.

## Bağlam

SiteHub, her biri yüzlerce site yöneten birden fazla yönetim firmasına hizmet
verecektir. Tenant hiyerarşisi:

```
Organization (Yönetim Firması — SaaS kiracısı)
  └── Site (Toplu Yapı — sakin/aidat verisi BURADA)
        └── Unit (Daire/Bağımsız Bölüm)
              └── Residency (Malik/Kiracı ilişkisi)
                    └── Person (kişi bilgisi)
```

Bu proje **bankacılık seviyesi** güvenlik gerektirir:
- Aidat tahakkuk/tahsilat
- IBAN bilgileri
- Ödeme tokenları (iyzico/PayTR)
- Sakin kişisel bilgileri (KVKK)

Bir site'nin verisi, yanlış bir sorgu veya uygulama bug'ı nedeniyle başka
site'ye sızarsa **KVKK ihlali + ticari felaket** olur.

## Değerlendirilen Seçenekler

### Seçenek A: Tek DB + Tek Schema + TenantId Kolonu ❌
- **Artı:** En basit, hızlı başlangıç
- **Eksi:** Her sorguda `WHERE tenant_id = ?` unutmak = sızıntı. Bankacılık için yetersiz.

### Seçenek B: Schema-per-Tenant ❌ (orijinal karar, reddedildi)
- **Artı:** Güçlü izolasyon, DB seviyesinde namespace ayrımı
- **Eksi:** Migration, backup, cross-site rapor, connection pool yönetimi karmaşık.
  Tek dev için 3-4 hafta.

### Seçenek C: DB-per-Tenant ❌
- **Artı:** En güçlü izolasyon
- **Eksi:** 1000 tenant = 1000 DB = operasyonel cehennem.

### Seçenek D: Row-Level Security (RLS) ✅ **SEÇİLDİ**
- **Artı:**
  - PostgreSQL seviyesinde garanti: uygulamadaki bug bile sızıntı yapmaz
  - EF Core uyumlu
  - Tek backup + tek migration + tek connection pool
  - Cross-site rapor doğal (Organization Sahibi tüm sitelerini görebilir)
  - Bankacılık endüstri standardı
- **Eksi:**
  - PostgreSQL session variable (`current_setting`) her connection'da set edilmeli
  - Defense-in-depth gerekli (uygulama filter + DB RLS — ikisi de)
  - Admin "bypass" için özel kural (ADR-0014'te detaylı)

## Karar: RLS (Seçenek D)

### 1. Tablo Sınıflandırması

Her tablo **üç kategoriden birindedir**:

#### 1.a Global (tenant-free)
Herkese açık, tenant izolasyonuna tabi değil:
- `geography.*` (countries, provinces, districts, neighborhoods)
- `identity.permissions` (kod sabiti)
- `public.__ef_migrations_history`
- `hangfire.*`

#### 1.b Organization-scoped
`organization_id` kolonu + RLS policy "sadece kendi organization'ının kaydı":
- `identity.roles` (organization-specific custom roller)
- `identity.memberships` (kullanıcının o organization'daki görevleri)
- `tenancy.sites` (ABC Yönetim'in siteleri XYZ'nin sitelerinden ayrı)
- `tenancy.service_organizations` (taşeron firma bilgileri)

#### 1.c Site-scoped
`site_id` kolonu + RLS policy "sadece kendi site'nin kaydı":
- `tenancy.units` (bağımsız bölümler)
- `tenancy.residencies` (kişi-daire eşleşmesi) — **resident'lar kendi kayıtlarına cross-tenant erişir, ADR-0014 §1.a**
- `financial.invoices` (borç tahakkuku) — resident kendi faturalarına cross-tenant erişir
- `financial.payments` (tahsilat) — resident kendi ödemelerine cross-tenant erişir
- `financial.*` (geri kalanı aidat, bütçe, kasa — sadece yönetim personeli)
- `announcements` (duyurular) — resident kendi site'sindeki duyuruları görür
- `requests` (talep/şikayet/öneri) — resident kendi talep'lerini görür
- `decisions` (karar defteri) — resident kendi site'sinin kararlarını görür

#### 1.d Person — özel durum (ADR-0005'e göre)
`identity.persons` tablosu **global** kalır (public schema).
- Bir kişi birden fazla sitede sakin olabilir
- Kişinin kamusal bilgisi (TCKN, ad, doğum tarihi) kritik değil
- Site'ye özel hassas bilgiler (telefon, email) `identity.person_contact_info`
  tablosunda tutulur (site-scoped)

**Not:** `PersonContactInfo` henüz yok — Faz F'de eklenecek.

#### 1.d.ii Resident Cross-Tenant Erişim — Kritik Kural

Bir kişi (örn. Ali Kemal) birden fazla tenant'ta sakin olabilir: ABC Yönetim'in
bir site'sinde malik + XYZ Yönetim'in bir site'sinde kiracı. Ali Resident Portal'a
login olduğunda **tüm residency'lerini** tek listede görmeli.

**RLS policy'leri Resident context için ek koşul içerir:**
```sql
-- Residency / Invoice / Payment tabloları:
USING (
    -- Normal site-scoped: yönetim personeli kendi site'sini görür
    site_id::text = current_setting('app.current_site_id', true)
    OR current_setting('app.is_admin_impersonating', true) = 'true'
    -- Resident: kendi PersonId'sine ait kayıtlar (cross-tenant erişim)
    OR (current_setting('app.context_type', true) = 'Resident'
        AND person_id::text = current_setting('app.resident_person_id', true))
);
```

Bu kural detaylı ADR-0014 §1.a'da.

#### 1.e LoginAccount + Session
`identity.login_accounts` + Redis session → **global** (kullanıcı kimliği
tenant-bağımsız, membership'lerle farklı context'lere girer).

### 2. Tenant Context — Session Variable

RLS policies PostgreSQL session variable'ı okur. EF Core her connection
açılışında bu değişkeni set eder:

```sql
-- Connection interceptor SET ederek:
SET LOCAL app.current_organization_id = '550e8400-...';
SET LOCAL app.current_site_id = 'a3f1...';
SET LOCAL app.is_admin_impersonating = 'false';
```

### 3. RLS Policy Örnekleri

#### Organization-scoped:
```sql
ALTER TABLE tenancy.sites ENABLE ROW LEVEL SECURITY;

CREATE POLICY sites_tenant_isolation ON tenancy.sites
    USING (
        organization_id::text = current_setting('app.current_organization_id', true)
        OR current_setting('app.is_admin_impersonating', true) = 'true'
    );
```

#### Site-scoped:
```sql
ALTER TABLE tenancy.units ENABLE ROW LEVEL SECURITY;

CREATE POLICY units_tenant_isolation ON tenancy.units
    USING (
        site_id::text = current_setting('app.current_site_id', true)
        OR current_setting('app.is_admin_impersonating', true) = 'true'
    );
```

**Dikkat — `current_setting(..., true)`:** İkinci parametre `true` → değişken
set edilmediyse NULL döner, hata vermez. Kötü durumda RLS hiçbir kayıt döndürmez
(güvenli default — fail-closed).

### 4. Uygulama Katmanı — Defense in Depth

RLS **tek güvenlik katmanı olmasın**. Uygulama tarafında da EF Core query
filter eklenir:

```csharp
modelBuilder.Entity<Site>()
    .HasQueryFilter(s =>
        s.OrganizationId == _tenantContext.OrganizationId
        || _tenantContext.IsAdminImpersonating);
```

**İki kat koruma:**
- RLS: DB seviyesinde kilit
- Query filter: Uygulamadaki hataların DB'ye gitmeden önlenmesi

### 5. Admin Impersonation (ADR-0014 detaylı)

Sistem Yöneticisi RLS'yi "impersonation mode" ile bypass eder:
- Bir Organization/Site seçer → "ABC Yönetim destek moduna geç"
- Session'da `IsAdminImpersonating = true` + hedef Org/Site set edilir
- RLS policy: `is_admin_impersonating = 'true'` → filtre bypass
- UI'da büyük kırmızı banner: **"DESTEK MODU: ABC Yönetim gibi görüntülüyorsunuz"**
- Tüm işlemler audit'e yazılır (kim, kimi impersonate etti, ne yaptı)

### 6. Context Switching (ADR-0005 ile uyumlu)

Multi-tab: Her Blazor Server circuit kendi scope'u → kendi
`ITenantContext` instance'ı. Sekme 1 = Site A, Sekme 2 = Site B doğal olarak
izole.

URL path: `/c/{contextType}/{contextId}/...` → derin link + yenileme sonrası
survival.

### 7. Migration Stratejisi

Tek DB, tek migration setti. Yeni bir tablo eklerken:
- `tenant_id` kolonu ekle (organization_id VEYA site_id)
- `ALTER TABLE ... ENABLE ROW LEVEL SECURITY;`
- `CREATE POLICY ...`

EF Core'un `MigrationBuilder`'ına custom extension yazılır:
```csharp
migrationBuilder.AddTenantRLS<Site>(scope: TenantScope.Organization);
```

### 8. Backup Stratejisi

Schema-per-site'ın "her tenant ayrı backup" avantajı yok ama:
- `pg_dump` tek komut → tüm DB backup (normal)
- PITR (Point-In-Time Recovery) standart
- Tenant-specific recovery gerekirse: restore + `WHERE tenant_id = ?` export

## Sonuçları

**Olumlu:**
- 3-4 gün implementasyon (Schema-per-Site 3-4 hafta)
- Operasyonel basitlik
- Cross-tenant raporlama doğal
- Test edilebilir güvenlik (RLS violation testleri)
- KVKK için güçlü dayanak ("DB seviyesinde izolasyon")

**Olumsuz / Dikkat:**
- Connection pool'daki her connection için session variable set EDİLMELİ
  (unutulursa → fail-closed = hiçbir kayıt dönmez, iyi default ama UX bozar)
- RLS policy'leri test edilmeli (integration test — "başka tenant verisi sızar mı?")
- Admin impersonation audit edilmeli (denetim için zorunlu)

## İleriye Dönük Seçenekler

**Çok büyük kurumsal müşteri geldiğinde** (örn. devlet kurumu, 1M+ sakin):
- O müşteri için **DB-per-Tenant**'a geçilebilir
- RLS altyapısı DB'nin tamamını temsil ettiği için geçiş nispeten kolay

## Referanslar

- PostgreSQL Docs: Row Security Policies
  (https://www.postgresql.org/docs/current/ddl-rowsecurity.html)
- Microsoft EF Core: Global Query Filters
- "Building Multi-Tenant Applications with PostgreSQL RLS" — AWS Database Blog
- ADR-0014: Multi-Tenant Isolation — Implementation Details
- ADR-0005: Context Switching and Hierarchical Access
