# ADR-0014: Multi-Tenant İzolasyon — Uygulama Detayları ve Admin Impersonation

**Durum:** Kabul Edildi
**Tarih:** 2026-04-21
**İlgili ADR'ler:** ADR-0002 (RLS Stratejisi), ADR-0005 (Context Switching), ADR-0011 (Identity + Authorization)

## Bağlam

ADR-0002 Row-Level Security (RLS) yaklaşımını benimsedi. ADR-0005 context
switching ve hiyerarşik erişimi tanımladı. Bu ADR, **implementasyon detaylarını**
ve özellikle **admin impersonation** akışını netleştirir.

Bankacılık seviyesi izolasyon gereksinimi için:
- Uygulama seviyesinde bug olsa bile veri sızmamalı
- Sistem Yöneticisi "destek" için müşteri verisine erişebilmeli
- Her erişim izlenebilir (audit) olmalı
- Kullanıcı farklı sekmelerde farklı context'lerde çalışabilmeli (ADR-0005)

## Karar

### 1. ITenantContext — Ana İnterface

Application katmanında (`SiteHub.Application.Abstractions.Tenancy`):

```csharp
public interface ITenantContext
{
    /// <summary>Aktif organization (yönetim firması). Organization/Site-scoped işlemler için zorunlu.</summary>
    Guid? OrganizationId { get; }

    /// <summary>Aktif site. Site-scoped işlemler için zorunlu, org-scoped için null olabilir.</summary>
    Guid? SiteId { get; }

    /// <summary>Resident context aktifse — login olmuş sakinin PersonId'si. Kendi residency'lerine erişimi belirler.</summary>
    Guid? ResidentPersonId { get; }

    /// <summary>Admin impersonation mode. True ise RLS bypass edilir, tüm erişim audit'e yazılır.</summary>
    bool IsAdminImpersonating { get; }

    /// <summary>Kullanıcı Sistem Yöneticisi mi? (Context seçiminden bağımsız, kimlik seviyesi bilgi.)</summary>
    bool IsSystemUser { get; }

    /// <summary>Context tipi: System, Organization, Site, Resident (ADR-0005 hiyerarşi).</summary>
    TenantContextType ContextType { get; }
}

public enum TenantContextType
{
    None = 0,
    System = 1,           // SiteHub internal — System Admin
    Organization = 2,     // Yönetim Firması seviyesinde
    Site = 3,             // Site seviyesinde
    Resident = 4          // Sakin — kendi kişisel view'ı, cross-tenant
}
```

### 1.a Resident Context — Cross-Tenant Özel Durum

**Senaryo:** Ali Kemal'in TCKN'si `12345...`. ABC Yönetim'in Yıldız Sitesi'nde
malik (3 no'lu daire) + XYZ Yönetim'in Güneş Apartmanı'nda kiracı (12 no'lu daire).
Ali tek login ile **her iki dairesini** görür, tüm borçlarını ödeyebilir.

**Özellik:**
- Resident login olunca: `ContextType = Resident`, `ResidentPersonId = Ali.PersonId`
- `OrganizationId` ve `SiteId` başlangıçta NULL
- Kullanıcı bir daire seçince (veya borcu ödemeye basınca) `SiteId` o site'ye set olur
- **Cross-tenant liste** (Ali'nin tüm residency'leri + borçları) için özel RLS kuralı

**RLS policy (Residency tablosu — site-scoped ama resident için özel):**
```sql
CREATE POLICY residencies_access ON tenancy.residencies
    USING (
        -- Normal site-scoped: kendi site'sinin residency'leri
        site_id::text = current_setting('app.current_site_id', true)
        -- Admin impersonation
        OR current_setting('app.is_admin_impersonating', true) = 'true'
        -- Resident: kendi PersonId'sinin residency'leri (cross-tenant)
        OR (current_setting('app.context_type', true) = 'Resident'
            AND person_id::text = current_setting('app.resident_person_id', true))
    );
```

**Resident'ın görebileceği tablolar (cross-tenant):**
- `identity.residencies` (kendi kayıtları)
- `financial.invoices` (kendi borçları — residency_id üzerinden)
- `financial.payments` (kendi ödemeleri)

**Resident'ın göremeyeceği:**
- Başka sakinlerin bilgisi (aynı sitede bile)
- Site'nin geneline ait raporlar/kararlar (yönetim personeli işi)
- Başka Organization'ların varlığı
```

### 2. Scope ve Lifecycle

**Blazor Server / Web Request:** `Scoped` lifetime.
- Her Blazor circuit (sekme) kendi ITenantContext'i alır → multi-tab doğal izolasyon
- Her HTTP request kendi scope'u → kimlik bulaşması yok

**Implementasyon:** `CircuitTenantContext` (Infrastructure).
- Session'dan `ActiveContext` okur
- URL path segment (`/c/{type}/{id}`) öncelikli (deep link + yenileme)
- Her ikisi de yoksa default context (en yüksek seviye) — ADR-0005 §3

**Hangfire Job:** Job parametrelerinde TenantContext **açıkça geçirilir**.
- HttpContext yok, session yok → manual set
- Job başlangıcında `IJobTenantContextSetter.Set(orgId, siteId, isImpersonating)` çağrılır

### 3. EF Core Entegrasyonu — Çift Katman

#### 3.a Katman 1: Query Filter (Application seviyesi)

`SiteHubDbContext.OnModelCreating`:

```csharp
// Organization-scoped entity
modelBuilder.Entity<Site>()
    .HasQueryFilter(s =>
        s.DeletedAt == null
        && (_tenantContext.IsAdminImpersonating
            || s.OrganizationId == _tenantContext.OrganizationId));

// Site-scoped entity
modelBuilder.Entity<Unit>()
    .HasQueryFilter(u =>
        u.DeletedAt == null
        && (_tenantContext.IsAdminImpersonating
            || u.SiteId == _tenantContext.SiteId));
```

**Bu katmanı kimse manuel bypass etmemeli.** `IgnoreQueryFilters()` kullanımı
yasak (lint kuralı ileride eklenecek).

#### 3.b Katman 2: PostgreSQL RLS (DB seviyesi)

Her tenant-scoped tablo için:

```sql
-- Organization-scoped örnek
ALTER TABLE tenancy.sites ENABLE ROW LEVEL SECURITY;
ALTER TABLE tenancy.sites FORCE ROW LEVEL SECURITY;  -- superuser bile bypass etmesin

CREATE POLICY sites_tenant_isolation ON tenancy.sites
    FOR ALL
    USING (
        -- Normal mod: sadece kendi organization
        organization_id::text = current_setting('app.current_organization_id', true)
        -- Impersonation mod: bypass (ama target org set edilmişse)
        OR (current_setting('app.is_admin_impersonating', true) = 'true'
            AND current_setting('app.impersonation_target_org_id', true) IS NOT NULL)
    );
```

**`FORCE ROW LEVEL SECURITY`:** Normal RLS'yi tablo sahibi (`sitehub` user)
bypass edebilir. FORCE bunu da engeller.

#### 3.c Connection Setup — DbConnectionInterceptor

Her `DbConnection.OpenAsync` sonrasında session variable'lar set edilir:

```csharp
public sealed class TenantContextConnectionInterceptor : DbConnectionInterceptor
{
    private readonly ITenantContext _ctx;

    public override async ValueTask<InterceptionResult> ConnectionOpenedAsync(
        DbConnection conn, ConnectionEndEventData data, CancellationToken ct)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT
              set_config('app.context_type', @ctx_type, false),
              set_config('app.current_organization_id', @org_id, false),
              set_config('app.current_site_id', @site_id, false),
              set_config('app.resident_person_id', @resident_id, false),
              set_config('app.is_admin_impersonating', @imp, false),
              set_config('app.impersonation_target_org_id', @target_org, false),
              set_config('app.impersonation_target_site_id', @target_site, false),
              set_config('app.current_user_id', @user_id, false);
        ";
        // ctx_type: 'System', 'Organization', 'Site', 'Resident', veya ''
        // resident_id: sadece ContextType=Resident ise dolu
        // ... parameter binding
        await cmd.ExecuteNonQueryAsync(ct);
        return InterceptionResult.Suppress();
    }
}
```

**Connection pool uyarısı:** PostgreSQL connection havuzdan geri alındığında
önceki session variable'lar **kalır**. Bu yüzden her açılışta set EDİLMELİ.
Set edilmezse `current_setting(..., true)` NULL döner → RLS hiçbir satır
döndürmez → fail-closed (güvenli).

### 4. Admin Impersonation — Detaylı Akış

#### 4.a Amaç

Sistem Yöneticisi, müşteriye destek verirken o müşterinin verisine erişmek
zorunda. Bu erişim:
- **Açıkça bildirilir** (banner, log, bildirim)
- **Audit'e yazılır** (kim, kime, ne zaman, ne yaptı)
- **Süreli olur** (varsayılan 1 saat, max 4 saat; sonra otomatik exit)
- **Hedef müşteriye bildirilebilir** (opsiyon, Faz F)

#### 4.b Başlatma

System Admin bir Organization/Site seçer:
```
POST /api/admin/impersonation/start
{
  "organizationId": "550e8400-...",
  "siteId": null,                    // null: tüm organization seviyesi
  "reason": "Destek talebi #1234: faturalandırma hatası",
  "durationMinutes": 60
}
```

Handler:
1. User'ın `IsSystemUser = true` kontrol (yetki check)
2. Reason zorunlu + min 10 karakter
3. Session güncellenir:
   - `IsAdminImpersonating = true`
   - `ImpersonationTargetOrgId = org_id`
   - `ImpersonationTargetSiteId = site_id`
   - `ImpersonationExpiresAt = now + durationMinutes`
4. `audit.impersonation_events` tablosuna kayıt:
   ```
   id, admin_user_id, target_org_id, target_site_id,
   reason, started_at, duration_minutes, ended_at (null)
   ```
5. Tüm aktif Blazor circuit'lere SignalR broadcast → banner göster

#### 4.c UI Banner

AppBar'ın ÜSTÜNDE, tüm sayfalarda, kırmızı:

```
🚨 DESTEK MODU AKTİF
ABC Yönetim Hizmetleri A.Ş. adına görüntülüyorsunuz
Başlangıç: 14:32  |  Kalan süre: 47 dakika  |  [Çıkış]
```

CSS: Sabit pozisyon, z-index max, kapatılamaz.

#### 4.d Her İşlem Audit'e Yazılır

Normal mod'da sadece kritik işlemler audit'e gider. Impersonation mod'da
**her okuma bile** audit'e yazılır:

```
audit.impersonation_access (append-only)
├── id
├── impersonation_event_id  → impersonation_events.id
├── timestamp
├── action_type             → Query / Command / FileDownload
├── resource                → tenancy.sites, financial.invoices, vb.
├── resource_id             → hangi kayıt
├── operation               → SELECT, UPDATE, DELETE
└── query_text              → geliştirici tarafı, hash
```

#### 4.e Sonlandırma

- **Manuel:** Banner'dan "Çıkış" → session reset
- **Otomatik:** `ImpersonationExpiresAt` geçince middleware session sıfırlar
- **Zorla:** Başka System Admin "end_impersonation" API'siyle sonlandırabilir

Her durumda `audit.impersonation_events.ended_at` doldurulur.

#### 4.f Kısıtlamalar

Impersonation mod'da YASAK:
- Başka Organization oluşturma (System işlemi)
- System-level yapılandırma değişikliği
- Başka impersonation başlatma (nested impersonation yok)
- Password reset (müşteri için — müşteri kendisi yapar)

### 5. Hangfire Job'larda Tenant Context

Background job'lar HttpContext'e erişemez. Job parametreleri içinde taşınır:

```csharp
public class SendMonthlyInvoicesJob
{
    public async Task ExecuteAsync(
        Guid organizationId,
        Guid siteId,
        Guid triggeredByUserId)
    {
        // Manuel set
        _tenantContextSetter.Set(organizationId, siteId, isImpersonating: false);

        // Normal iş
        var invoices = await _mediator.Send(new GenerateMonthlyInvoicesCommand(siteId));
        // ...
    }
}

// Enqueue ederken:
BackgroundJob.Enqueue<SendMonthlyInvoicesJob>(
    job => job.ExecuteAsync(orgId, siteId, currentUserId));
```

**Kritik:** Job parametreleri Hangfire'ın JSON serializer'ı tarafından
tablolara yazılır. TenantId'ler buradan görünür → system admin "X Site için
ne jobları çalıştı" görebilir.

### 6. Test Stratejisi

#### 6.a RLS Violation Tests (integration)

Gerçek PostgreSQL ile test:

```csharp
[Fact]
public async Task BaskaOrganizationVerisineErisimImkansiz()
{
    // Arrange: İki organization yarat
    var orgA = await CreateOrganizationAsync();
    var orgB = await CreateOrganizationAsync();
    var siteInOrgB = await CreateSiteAsync(orgB.Id);

    // Act: Org A context'inde sorgula
    _tenantContext.SetOrganization(orgA.Id);
    var sites = await _db.Sites.ToListAsync();

    // Assert: Org B'nin site'si dönmemeli
    sites.Should().NotContain(s => s.Id == siteInOrgB.Id);
}
```

#### 6.b Query Filter Bypass Attempt

```csharp
[Fact]
public async Task IgnoreQueryFiltersBilePostgreSQLRLSEngeller()
{
    var orgA = ..., orgB = ...;
    _tenantContext.SetOrganization(orgA.Id);

    // IgnoreQueryFilters uygulama filtresini atlar, ama RLS hala koruma
    var sites = await _db.Sites.IgnoreQueryFilters().ToListAsync();

    // RLS yüzünden hala sadece Org A site'leri döner
    sites.Should().OnlyContain(s => s.OrganizationId == orgA.Id);
}
```

#### 6.c Impersonation Audit Test

```csharp
[Fact]
public async Task ImpersonationModundaHerQuerylogged()
{
    var admin = await CreateSystemAdminAsync();
    var targetOrg = await CreateOrganizationAsync();

    await _impersonation.StartAsync(admin.Id, targetOrg.Id, "Test");

    _ = await _db.Sites.ToListAsync();
    _ = await _db.Units.ToListAsync();

    var auditEvents = await _db.ImpersonationAccess
        .Where(a => a.AdminUserId == admin.Id)
        .ToListAsync();

    auditEvents.Should().HaveCount(2);
}
```

### 7. Uygulama Sırası

**Gün 1:** ITenantContext + CircuitTenantContext + URL path routing
**Gün 2:** DbConnectionInterceptor + Session variable setup + migration (her tabloya tenant_id)
**Gün 3:** RLS policies migration + EF Core query filter + admin impersonation API
**Gün 4:** Hangfire tenant context + testler + system admin zorunlu 2FA

## Sonuçları

**Olumlu:**
- Bankacılık seviyesi izolasyon — DB seviyesinde kilit
- Defense-in-depth: uygulama bug'ı bile sızıntı yapmaz
- Destek akışı açıkça izlenebilir (impersonation audit)
- Multi-tab doğal olarak izole

**Olumsuz / Dikkat:**
- Connection interceptor her open'da çalışmalı — unutulursa tüm sorgular boş döner (fail-closed, iyi default ama debug zor)
- RLS policy'leri integration test edilmeli (unit test yetmez, gerçek PG gerek)
- Impersonation banner kapatılamaz — UX dikkat gerekir
- `FORCE ROW LEVEL SECURITY` production'da aktif (development'ta kapatılabilir için config)

## Referanslar

- PostgreSQL: `current_setting` function + row security policies
- ADR-0002: RLS Strategy (üst seviye karar)
- ADR-0005: Context Switching (kullanıcı akışı)
- ADR-0011: Identity and Authorization (Membership hiyerarşisi)
- NIST SP 800-53: AC-3 (Access Enforcement), AU-2 (Audit Events)
