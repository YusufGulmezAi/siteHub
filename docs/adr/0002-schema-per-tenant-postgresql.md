# ADR-0002 (ESKİ): Multi-Tenancy Stratejisi — Schema-per-Tenant (PostgreSQL)

**Durum:** 🚫 SÜPERSEDE EDİLDİ (yerine `0002-multi-tenancy-rls-strategy.md`)
**Tarih:** 2026-04-19 (orijinal), 2026-04-21 (süperseded)
**Yerine geçen:** [ADR-0002-v2: Row-Level Security](./0002-multi-tenancy-rls-strategy.md)

## Süpersede Notu (2026-04-21)

Bu ADR, geliştirme süresi tahmininde gerçekçi olmadığı için süperseded edildi:

- Schema-per-Site için tahmin edilen 2 hafta, gerçekte 3-4 hafta iş çıkıyor
- Tek geliştirici için operasyonel yük çok yüksek (migration orchestrator,
  backup per-tenant, connection pool yönetimi)
- PostgreSQL Row-Level Security (RLS) aynı güvenlik seviyesini 3-4 günde
  sağlayabilir

**Bankacılık seviyesi güvenlik iddiasından taviz verilmedi** — RLS, bankacılık
endüstrisinde yaygın kullanılan bir yaklaşımdır. Detaylı gerekçe yeni ADR'de.

Aşağıdaki içerik **tarihsel referans** amaçlıdır, artık uygulanmıyor.

---

## (Eski içerik — uygulanmıyor)

## Bağlam

SiteHub, her biri yüzlerce site yöneten birden fazla yönetim firmasına hizmet
verecektir. Tenant hiyerarşisi:

```
Firma (Yönetim Firması)
  └── Site (Toplu Yapı)
        └── Unit (Daire/Bağımsız Bölüm)
              └── Residency (Malik/Kiracı ilişkisi)
```

Her tenant'ın verisi **birbirinden izole** olmalıdır (KVKK ve ticari gereklilik).
Bir tenant'ın verisi, yanlış bir SQL sorgusu yüzünden başka tenant'a sızarsa
felaket olur.

## Değerlendirilen Seçenekler

### Seçenek A: Tek DB + Tek Schema + TenantId Kolonu
- **Artıları:** En basit, hızlı başlangıç
- **Eksileri:** Her sorguda `WHERE tenant_id = ?` eklemeyi unutursan veri sızar;
  izolasyon en zayıf seviyede; global query filter'lara bağımlılık yüksek risk

### Seçenek B: Tek DB + Schema-per-Tenant ✅
- **Artıları:** PostgreSQL'in native schema desteği; izolasyon güçlü (yanlışlıkla
  başka schema'ya sızma DB seviyesinde engellenir); tek bağlantı havuzu (connection
  pool), tek backup; geliştirici için "sanki tek tenantlık bir DB"
- **Eksileri:** Migration'ları her tenant schema'sına ayrı ayrı çalıştırmak gerekir;
  tenant sayısı çok büyürse (>10.000) yönetim zorlaşır

### Seçenek C: DB-per-Tenant
- **Artıları:** En güçlü izolasyon, tenant başına backup/restore kolay
- **Eksileri:** 1000 tenant = 1000 DB bakımı; connection pool sorunları; operasyonel
  yük solo dev için kabul edilemez

## Karar

**Seçenek B: Schema-per-Tenant** seçildi.

### Uygulama Detayları

1. **Her site bir schema'ya karşılık gelir.** Schema adı: `tenant_<site_id>`.
2. **Ortak schema** (`public`): Identity, Firm, Site katalog, audit log gibi
   tenant-bağımsız veriler burada.
3. **Tenant Resolver:** Her HTTP request'te, subdomain veya JWT claim'inden aktif
   tenant belirlenir ve `ITenantContext` üzerinden akışa enjekte edilir.
4. **EF Core:** Tenant-scoped entity'ler için `DbContext`'in `OnConfiguring`
   metodunda Npgsql'in `SearchPath` özelliği o tenant'ın schema'sına yönlendirilir.
5. **Migration Stratejisi:**
   - Ortak schema migration'ları: uygulama başlangıcında otomatik (Development/Test);
     Production'da manuel/CI üzerinden kontrollü.
   - Tenant schema migration'ları: yeni tenant provisioning sırasında + global bir
     "migration broadcast" background job'u ile (Hangfire ile).

### Tenant Context Çözümü

Başlangıçta **JWT claim'i** + **route parameter** kombinasyonu:
- Login sonrası kullanıcı bir context (firm/site) seçer
- Token'a `active_tenant_type` ve `active_tenant_id` eklenir
- Middleware bu claim'i `ITenantContext` service'ine basar
- İleride isteğe göre subdomain tabanlı yönlendirme eklenebilir

## Sonuçları

**Olumlu:**
- Veri izolasyonu DB seviyesinde garanti
- KVKK için güçlü bir dayanak
- Geliştirici kodunda "tenant filter" unutma riski yok

**Olumsuz / Dikkat:**
- Migration deployment'ı iyi planlanmalı (ADR-0009'da detaylanacak)
- 1000+ tenant ölçeğinde `pg_dump` yedeği uzun sürebilir — pg_basebackup veya
  logical replication düşünülmeli
- Her yeni migration'ı tüm tenant schema'larına yaymak için otomasyon şart

## Referanslar

- PostgreSQL Docs: Schemas (https://www.postgresql.org/docs/current/ddl-schemas.html)
- Microsoft EF Core Multi-tenancy Docs
