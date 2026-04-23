# Faz F.6 C.5a — Site Detail (Tab İskeleti + Ana Bilgiler Full)

**Tarih:** 2026-04-23
**Scope:** 1 yeni dosya (Detail.razor) + 1 değişen dosya (List.razor).

## Kapsam

C.5'in ilk aşaması — tab iskeletini kuruyoruz, **Ana Bilgiler full**, diğer 5 tab placeholder. Gerçek tab içerikleri (Banka & Hesaplar, Belgeler vb.) sonraki C.5b, C.5c... seanslarında eklenir.

## Tab Yapısı

| # | Tab | Durum | İleride |
|---|---|---|---|
| 1 | Ana Bilgiler | **FULL** | Sözleşme bölümü C.5b'de eklenecek |
| 2 | Banka & Hesaplar | Placeholder | Akordiyon + CRUD |
| 3 | Muhasebe | Placeholder | — |
| 4 | Finans | Placeholder | — |
| 5 | İK | Placeholder | Akordiyon, ilk Bordro |
| 6 | Belgeler | Placeholder | 6 akordiyon + soft delete + geri al |

## Dosyalar

### Yeni
- `Components/Pages/Sites/Detail.razor`
  - Route: `/organizations/{OrganizationId}/sites/{SiteId}`
  - Organization Detail pattern (breadcrumb + kartlar + MudSimpleTable)
  - MudTabs ile 6 tab
  - URL manipulation koruması (Site.OrganizationId ≠ URL.OrganizationId)
  - Geography adları (İl/İlçe) sessiz resolve

### Değişen
- `Components/Pages/Sites/List.razor`
  - **Yeni:** İşlemler kolonu (Detay göz + Düzenle kalem)
  - Her ikon `HasPermission` ile sarılı:
    - Detay → `Permissions.Site.Read` + Site context
    - Düzenle → `Permissions.Site.Update` + Site context
  - "Yeni Site" butonu → `Permissions.Site.Create` + Organization context
  - Mevcut kolonlar değişmedi

## Tasarım Kararları

### Detail sayfasında global aksiyon butonu YOK
Kullanıcı kararı (C): Detay sayfası sadece okuma. Düzenle/Sil/Aktif-Pasif **liste'deki** kalem/buton üzerinden yapılır. "Listeye Dön" butonu navigasyon için var.

### Placeholder tab'larda HasPermission YOK
5 placeholder tab görünür kalıyor — kullanıcı ileride ne geleceğini görsün. Gerçek permission'lar (`site.bank.read`, `site.document.read` vb.) sonraki seanslarda eklenecek, o zaman her tab kendi permission'ına sarılacak.

### Geography adları
SiteDetailDto'da `ProvinceId` + `DistrictId` var (Guid), ad yok. `IGeographyApi.GetProvincesAsync` çağrısı ile 81 il yüklenir, siteye ait olan seçilir. İlçe için ek bir çağrı. Başarısız olursa tire gösterilir (sayfa göçmez).

## Uygulama

```powershell
cd D:\Projects\sitehub

Unblock-File C:\Users\<sen>\Downloads\sitehub-f6-c5a.zip
Expand-Archive -Path "C:\Users\<sen>\Downloads\sitehub-f6-c5a.zip" `
    -DestinationPath "D:\Projects\sitehub\" -Force

dotnet build
```

Build temiz olmalı. Test etkilenmez (UI only).

## Smoke Test

```powershell
dotnet run --project src\SiteHub.ManagementPortal
```

### Test 1 — Liste'den Detay'a geçiş
1. Admin ile giriş
2. `/organizations` → bir organizasyon için Apartment ikonu
3. Site listesi açılır. Her satırda **Durum** sağında iki ikon:
   - Göz (Detay)
   - Kalem (Düzenle)
4. Göz ikonuna tıkla → URL `/organizations/{orgId}/sites/{siteId}` olmalı
5. Detay sayfası açılır

### Test 2 — Detail sayfası yapısı
1. Breadcrumb: **Organizasyonlar › [Org Adı] › Siteler › [Site Adı]** (ilk üçü linkli)
2. Başlık: Site adı + yeşil/amber durum badge
3. Sağ üstte: **Listeye Dön** butonu (global aksiyon yok)
4. Tab bar: **Ana Bilgiler** (seçili), Banka & Hesaplar, Muhasebe, Finans, İK, Belgeler
5. İkonlar her tab'da (Info, AccountBalance, Calculate, TrendingUp, People, Folder)

### Test 3 — Ana Bilgiler tab (full)
Üç kart görünmeli:
1. **Site Bilgileri**: Ad, Ticari Ünvan, Kod, VKN
2. **Adres ve Finans**: Adres, İl, İlçe, IBAN
3. **Sistem Bilgileri**: Oluşturma tarihi, oluşturan, (varsa) güncelleme bilgisi

İl/İlçe dolu görünmeli (C.3'te doğru kaydedildiyse).

### Test 4 — Placeholder tablar
Diğer 5 tab'a sırayla tıkla:
- Her biri ortalanmış icon + başlık + açıklama + "Bu bölüm sonraki fazlarda gelecek" gösteriyor
- Sayfa yapısı kırılmamalı, tab değişimi smooth

### Test 5 — URL manipulation koruması
1. Manuel URL: `/organizations/YANLIS-ORG-ID/sites/MEVCUT-SITE-ID`
2. "Bu site seçili organizasyona ait değil" hata alert'i çıkmalı
3. "Site Listesine Dön" butonu çalışmalı

### Test 6 — Permission (admin ile tüm butonlar görünür)
Admin = SystemAdmin = tüm permission'lara sahip.
- Liste'de Detay + Düzenle ikonları her satırda görünmeli
- "+ Yeni Site" butonu görünmeli

## Commit Önerisi

```
Faz F.6 C.5a: Site Detail (Tab iskeleti + Ana Bilgiler full)

Site detay sayfasi, MudTabs ile 6 tab:
1. Ana Bilgiler - FULL (Organization Detail pattern)
2. Banka & Hesaplar - placeholder
3. Muhasebe - placeholder
4. Finans - placeholder
5. IK - placeholder
6. Belgeler - placeholder

Her placeholder tab "Bu bolum sonraki fazlarda gelecek" mesaji
gosteriyor. Gercek tab icerikleri ve per-tab permission'lari
C.5b+ seanslarinda eklenecek.

Yeni:
- Components/Pages/Sites/Detail.razor
  Route: /organizations/{orgId}/sites/{siteId}
  Breadcrumb, basliik + durum badge, tab bar.
  Ana Bilgiler: 3 kart (Site, Adres/Finans, Sistem).
  Geography API ile Il/Ilce adlari resolve edilir.
  URL manipulation korumasi (Site.OrganizationId != URL.OrgId).
  Global aksiyon butonu YOK (C karari) - Duzenle List'teki kalem
  ikonu ile yapiliyor.

Degisen:
- Components/Pages/Sites/List.razor
  Islemler kolonu eklendi (Detay goz + Duzenle kalem).
  Her ikon HasPermission ile sarildi (site.read/site.update +
  Site context). Yeni Site butonu da HasPermission'a sarildi
  (site.create + Organization context).

Test: Build temiz, 146 test yesil.
Smoke: Liste -> Detay navigasyon, 3 kart gorunur, 5 placeholder
       tab gorunur, URL manipulation korumasi calisir.
```

## Kapsam Dışı (C.5b+)

- Sözleşme bölümü (Ana Bilgiler'e eklenecek — site.contract.read/manage)
- Banka & Hesaplar tab içeriği (akordiyon + CRUD)
- Belgeler tab içeriği (6 akordiyon + soft delete + geri al + MinIO)
- Diğer tab'lar (Muhasebe, Finans, İK)
- Per-tab permission sabitleri (`site.bank.read`, `site.document.read` vb.)
- PermissionSynchronizer'ın yeni sabitleri DB'ye sync etmesi
- SystemRolesSeeder güncellemeleri (rol başına yeni permission'lar)
