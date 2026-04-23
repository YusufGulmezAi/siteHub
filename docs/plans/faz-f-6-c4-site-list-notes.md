# Faz F.6 C.4 — Site Listesi (Nested, Organization Context Altında)

**Tarih:** 2026-04-22
**Scope:** UI — Organization List'e "Siteler" action + yeni nested Site List sayfası.

**ÖNKOŞUL:** C.1 cleanup commit'lenmiş olmalı (`sitehub-f6-c1-cleanup.zip` uygulandı).

## Mimari (kullanıcı kararı)

Site işlemleri daima Organization context altında:
- Nav menüde "Siteler" girişi YOK
- Organizasyonlar → bir org'a tıkla → "Siteler" ikonu → nested sayfa
- URL pattern: `/organizations/{orgId}/sites`
- SuperAdmin da bu akışı kullanır (RLS bypass + normal navigation)

Gelecekte ADR-0005 tam context switching'e (`/c/org/{id}/...`) migration yapılabilir,
URL search/replace ile — internal logic doğru, sadece URL süsleme.

## Değişen 1 Dosya + Yeni 1 Dosya

| Dosya | Durum | Açıklama |
|---|---|---|
| `Components/Pages/Organizations/List.razor` | Değişti | "Siteler" action ikonu (`Apartment`) eklendi, göz ve kalemin yanına |
| `Components/Pages/Sites/List.razor` | **YENİ** | `/organizations/{orgId}/sites` sayfası (salt okunur, C.4) |

## C.4'ün Kapsamı — Sadece Listeleme

Bu ilk Site UI dosyası **sadeye kadar** sade:

**YOK:**
- "+ Yeni Site" butonu (C.3 Site Form sonrası gelir)
- Satırlarda Detay/Düzenle/Sil ikonları (C.3 ve C.5 bitince gelir)
- "İşlemler" kolonu bile yok — action yokken başlık göstermek yanıltıcı

**VAR:**
- Breadcrumb: "**Organizasyonlar** › **[Org Adı]** › Siteler" (ikisi de tıklanabilir)
- Başlık: "Siteler" + alt metin "[Org Adı] — yönetilen apartman/kompleks listesi"
- Toolbar: Arama (kod/ad/ticari ünvan/adres/VKN/IBAN) + Aktif/Hepsi switch
- SiteHubDataGrid: Kod | Ad | Ticari Ünvan | Adres | VKN | IBAN | Durum
- Durum badge: div + palette (MudChip workaround, §5.5 Öğrenim 9)
- OnAfterRenderAsync(firstRender) pattern (prerender tuzağı, §5.5 Öğrenim 1)
- Error state: Organization yüklenemezse alert + "Organizasyonlara Dön"
- Empty state: "Bu organizasyonda gösterilecek site yok"

## Organization List Değişiklik Detayı

Mevcut "İşlemler" kolonunda 2 ikon vardı:
1. Göz (Detail)
2. Kalem (Edit)

Yeni 3. ikon:
3. **Apartment** (Siteler) → `NavigateToSites(id)` → `/organizations/{id}/sites`

## Uygulama

```powershell
cd D:\Projects\sitehub

# Zip'i uygula
Unblock-File C:\Users\<sen>\Downloads\sitehub-f6-c4.zip
Expand-Archive -Path "C:\Users\<sen>\Downloads\sitehub-f6-c4.zip" `
    -DestinationPath "D:\Projects\sitehub\" -Force

# Build
dotnet build

# Test (opsiyonel, UI değişikliği, test kırılmamalı)
dotnet test --no-build
```

Build temiz olmalı. Test 146/146 yeşil kalacak (domain/application dokunulmadı).

## Smoke Test (portal)

Portal'ı başlat:
```powershell
dotnet run --project src\SiteHub.ManagementPortal
```

### Test 1 — Organization List'ten Siteler'e geçiş
1. `/organizations` → bir firmada 3 ikon görüyor musun? (Göz, Kalem, **Apartment**)
2. Apartment (Siteler) ikonuna tıkla
3. URL `/organizations/{orgId}/sites`'a gitti mi?

### Test 2 — Site List sayfası
1. Breadcrumb görünüyor mu? "Organizasyonlar › **ABC Yonetim** › Siteler"
2. "Organizasyonlar" tıklanabilir mi? → `/organizations`'a döner
3. "ABC Yonetim" tıklanabilir mi? → `/organizations/{orgId}` Detail'ına gider
4. Başlık "Siteler" + alt metin Organization adını içeriyor mu?
5. "+ Yeni Site" butonu **YOK** mu? ✓ (C.3 bitince gelecek)

### Test 3 — Tablo + veri
1. Tablo başlıkları: Kod | Ad | Ticari Ünvan | Adres | VKN | IBAN | Durum — tam mı?
2. "İşlemler" kolonu **YOK** mu? ✓
3. 2 kayıt gelmeli: Güneş Apartmanı (680690), Yıldız Sitesi (619806)
4. Durum badge: yeşil "Aktif" (pasifleştirdiysek turuncu "Pasif")

### Test 4 — Filtreleme
1. Aramaya "güneş" yaz → sadece Güneş Apartmanı kalmalı
2. Aramayı temizle, "Aktif" switch'ini kapat → "Hepsi" moduna geçer

### Test 5 — Edge case
1. URL'e el ile `/organizations/00000000-0000-0000-0000-000000000000/sites` yaz
2. "Organizasyon yüklenemedi" error alert'i çıkmalı
3. "Organizasyonlara Dön" butonu çalışmalı

## Commit Önerisi

```
Faz F.6 C.4: Site listesi (nested, org context URL'den)

Mimari: Site islemleri daima Organization context altinda. Kullanici
Organization listesinden 'Siteler' ikonuna tiklayarak o org'un
sitelerine iner. Nav menude 'Siteler' girisi yok.

Yeni:
- Components/Pages/Sites/List.razor
  URL: /organizations/{OrganizationId}/sites
  - Breadcrumb: Organizasyonlar > [Org Adi] > Siteler (ikisi de linkli)
  - Baslik: 'Siteler' + alt metin org adi
  - SiteHubDataGrid: Kod|Ad|Ticari Unvan|Adres|VKN|IBAN|Durum
  - Durum badge (MudChip workaround, palette CSS variable)
  - Arama + Aktif/Hepsi switch
  - Error state (org yuklenemedi) + empty state

Degisen:
- Components/Pages/Organizations/List.razor
  Islemler kolonuna 3. ikon: Apartment (Siteler)
  NavigateToSites(id) -> /organizations/{id}/sites

Sade tutuldu (C.4 kapsami): '+ Yeni Site' butonu yok, satir ikonlari
yok. C.3 Site Form + C.5 Site Detail gelince eklenir.

Kullanilan API: ISitesApi.GetByOrganizationAsync (nested endpoint).
C.1 cleanup'tan sonra flat GetAllAsync yok — planli.

Test: Build temiz, 146 test yesil.
Smoke: Apartment ikonu -> nested sayfa -> breadcrumb + tablo +
arama calisir.
```

## Sonraki Adım — C.3 Site Form (yarınki seans)

Bu commit'ten sonra:
- "+ Yeni Site" butonu eklenecek (Site List'e)
- `/organizations/{orgId}/sites/new` Form sayfası
- IL/İlçe cascading dropdown (IGeographyApi)
- IBAN validation (TR + 24 rakam)
- VKN opsiyonel (Site için)
- Organization Form pattern'i + yeni buton standartları (Vazgeç Warning, dirty check)
