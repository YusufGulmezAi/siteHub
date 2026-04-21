# ADR-0005: Bağlam Geçişi, Hiyerarşik Erişim ve Sekme İzolasyonu

**Durum:** Kabul Edildi
**Tarih:** 2026-04-19

## Bağlam

SiteHub'da bir kullanıcı farklı seviyelerde sorumluluk sahibi olabilir:
- Sistem kullanıcısı → tüm kiracıları yönetir
- Kiracı (Yönetim Firması) kullanıcısı → o firmanın tüm sitelerini yönetir
- Site kullanıcısı → sadece görevli olduğu site(ler)de çalışır

Bir Sistem kullanıcısı, her kiracı için ayrı kullanıcı oluşturulmadan otomatik
olarak tüm kiracıların içerisine girebilmelidir. Aynı şekilde, Kiracı'da tanımlı
bir muhasebeci o kiracının tüm sitelerinin muhasebesine otomatik erişebilmelidir.

Ek gereksinim: Bir kullanıcı aynı tarayıcıda **farklı sekmelerde farklı bağlamlarda**
çalışabilir. Örn: Sekme 1'de "A Sitesi"nde muhasebe kontrol ederken Sekme 2'de
"B Sitesi"nde aidat tahakkuk edebilir. Bağlamlar karışmaz.

## Karar

### 1. Hiyerarşik Erişim Modeli

```
System (seviye 0)
  └── Firm (Kiracı, seviye 1)
        └── Site (seviye 2)
              └── Unit (Bağımsız Bölüm, seviye 3)
                    └── Residency (Malik/Kiracı ilişkisi, seviye 4)
```

**Kural:** Bir `Membership`, kullanıcıya o seviyenin altındaki TÜM alt bağlamlara
rol/izinleri çerçevesinde otomatik erişim verir.

```
Membership                                   Erişilebilir Bağlamlar
────────────────────────────────────────    ────────────────────────────
User=Ali, ContextType=System, Role=Admin →  Tüm Firm'ler, tüm Site'lar
User=Veli, ContextType=Firm(X), Role=Acct → Firm X → X altındaki tüm Site'lar
                                            (yalnızca muhasebe permission'ları)
User=Ayşe, ContextType=Site(Y), Role=Mgr  → Sadece Site Y
```

**Malik/Kiracı (Sakin):** Bu akışa dahil DEĞİLDİR. Onlar ayrı bir portalda
(`SiteHub.ResidentPortal`) çalışır ve Management Portal'a giremez.

**Resident Portal özellikleri** (detay ADR-0014 §1.a):
- Login: TCKN + şifre + (opsiyonel) 2FA
- Cross-tenant view: Ali Kemal'in birden fazla yönetim firmasında (tenant'ta)
  daireleri varsa **hepsini tek listede** görür
- Context: `TenantContextType.Resident` + `ResidentPersonId = Ali.PersonId`
- Erişim: sadece kendi `Residency`'leri, kendi `Invoice`'ları, kendi `Payment`'ları
- Yönetim personeline ait görünüm (raporlar, karar defteri, diğer sakinler) **YOK**
- Ödeme akışı: daire seçer → o site'nin PSP/Open Banking sayfasına yönlenir

**Örnek:**
```
Ali Kemal login olur. RezidentPortal dashboard:

🏠 Benim Daireler (3)
┌──────────────────────────────────────────────────────┐
│ ABC Yönetim → Yıldız Sitesi → 3 no'lu daire (Malik)  │
│   Borç: 850 TL  [Öde]                                 │
├──────────────────────────────────────────────────────┤
│ XYZ Yönetim → Güneş Apartmanı → 12 no'lu (Kiracı)   │
│   Borç: 0 TL                                          │
├──────────────────────────────────────────────────────┤
│ ABC Yönetim → Deniz Kompleksi → B-7 (Malik, işyeri)  │
│   Borç: 2.300 TL  [Öde]                              │
└──────────────────────────────────────────────────────┘
```

### 2. Available Contexts Query

Kullanıcı login olduğunda backend şu akışı işletir:

```
1. Kullanıcının tüm Membership'leri alınır
2. Her Membership için alt seviyelere traversal:
   - System Membership  → Tüm Firm'ler + her birinin altındaki Site'lar
   - Firm Membership    → O Firm + altındaki tüm Site'lar
   - Site Membership    → Sadece o Site
3. Sonuç: AvailableContexts listesi (flat, kullanıcıya özgü)
4. Bu liste UI'daki context combobox'ını besler
```

Query Application katmanında: `GetAvailableContextsForUserQuery`.

### 3. Login Akışı (Management Portal)

Kullanıcı login olduğunda **context seçimi YAPTIRILMAZ**:

```
1. TCKN/VKN/YKN + şifre + (opsiyonel) 2FA
2. Cookie yazılır (authentication)
3. Doğrudan Dashboard açılır — varsayılan context = en yüksek seviye
   Örn: System Membership'i varsa "System"; yoksa ilk Firm; yoksa ilk Site
4. AppBar'daki combobox ile context değiştirilebilir
```

### 4. Active Context Yönetimi (Multi-Tab İzolasyon)

**Sorun:** Bir tarayıcıda birden fazla sekme açıkken her sekme FARKLI bağlamda
olabilmeli, birbirlerine karışmadan.

**Çözüm:** Active context üç katmanda tutulur:

| Katman           | Scope            | Amaç                                           |
|------------------|------------------|------------------------------------------------|
| URL path segment | Sekme           | Deep-link, yenileme sonrası survival, paylaşım |
| sessionStorage   | Sekme           | URL yokken default (örn. ilk dashboard girişi) |
| Circuit state    | SignalR circuit | Blazor bileşenlerinden erişim (runtime cache)  |

**URL Şeması:**
```
/c/{contextType}/{contextId}/...
örn: /c/firm/550e8400-.../sites
     /c/site/a3f1.../invoices/new
```

**Blazor Server'ın avantajı:** Her sekme kendi SignalR circuit'ini açar, dolayısıyla
`Scoped` bir `IActiveContextAccessor` servisi doğal olarak sekme-başı izoledir.

### 5. Güvenlik (Kritik)

UI'daki context seçimi UX içindir — **güvenlik değildir**. Her backend operasyonu:

1. CQRS command/query içinde `ActiveContextType` ve `ActiveContextId` alır
2. MediatR `ContextAuthorizationBehavior` pipeline'ı şunu kontrol eder:
   - Kullanıcının Membership hiyerarşisinde bu context'e erişimi var mı?
   - Bu operasyon için gerekli permission'a sahip mi?
3. Geçemezse → `ForbiddenException`
4. Tüm denetim denemeleri loglanır (ADR-0006)

Global EF Core query filter'ı da eklenir: tenant-scoped entity'ler yalnızca
aktif context'e ait kayıtları döndürür. Bu, "WHERE filtresini unutma" riskini
tamamen ortadan kaldırır.

### 6. Context Combobox UX

AppBar'da sağ üstte:

```
[🏢 Firm: ABC Yönetim ▼]  (mevcut bağlam)
  Sistem
  ─────────────────
  Kiracılar
    ABC Yönetim
    DEF Yönetim
  ─────────────────
  Siteler (ABC Yönetim altında)
    Yıldız Sitesi
    Güneş Sitesi
  ─────────────────
  Siteler (DEF Yönetim altında)
    Deniz Sitesi
```

- Seçim değişince URL güncellenir, sayfa `StateHasChanged()` ile tazelenir
- Menüler yeni permission'lara göre yeniden render edilir
- Breadcrumb bilgisi güncellenir

## Sonuçları

**Olumlu:**
- Sistem admin'lerin her kiracıda ayrı hesap açmasına gerek yok
- UX: GitHub/Stripe/Linear benzeri tanıdık bir pattern
- Sekme izolasyonu Blazor Server ile doğal olarak sağlanır
- Hiyerarşik erişim model backend'de tek noktada denetlenir

**Olumsuz / Dikkat:**
- Context hiyerarşisi değiştiğinde (örn. Site başka Firma'ya devredilirse)
  Membership model'ine göre erişim otomatik güncellenir — denetim logları gerekli
- `AvailableContextsQuery` her oturumda çalıştırıldığı için cache'lenmeli (Redis)
- URL-based context ile "yanlış link paylaşma" riski — denetim kontrolü her zaman
  backend'dedir, link üzerinden izinsiz erişim imkansız

## Uygulama Sırası (sonraki adımlar)

1. `IActiveContextAccessor` (scoped) Application katmanında interface
2. Infrastructure'da `CircuitActiveContextAccessor` implementasyonu
3. MediatR `ContextAuthorizationBehavior`
4. EF Core global query filter
5. `ContextSwitcher` MudBlazor component'i (combobox + dropdown menü)
6. `AvailableContextsQuery` + Redis cache

## Referanslar

- Stripe API: `Stripe-Account` header pattern
- GitHub: Organization switching
- Blazor Server circuit lifetime dokümantasyonu
