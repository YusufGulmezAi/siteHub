# F.6 Madde 3 — Detail = Form Birleştirme

**Tarih:** 2026-04-24
**Scope:** 1 yeni dosya (overwrite Detail.razor), 1 değişen dosya (List.razor), **1 dosya SİLİNECEK** (Form.razor).

## Amaç

Kullanıcı "Site için ayrı Create/Edit sayfası gereksiz — Detail içinde yapılabilir" dedi. Form.razor kaldırılır, Detail.razor tek sayfa olarak hem Create hem Edit mantığını içerir.

## Davranış

### Create mode (`/sites/new`)
- Sadece **Ana Bilgiler** tab'ı görünür (7 ek tab gizli)
- Alanlar editable (boş form)
- Altta: [İptal] [Oluştur]
- Kaydedince → `/organizations/{orgId}/sites/{yeniId}` redirect → Edit mode'a düşer

### Edit mode (`/sites/{siteId}`)
- **8 tab** görünür (Ana Bilgiler + 7 placeholder)
- Ana Bilgiler editable (yüklü veriyle)
- Durum badge üstte (Aktif/Pasif)
- Sistem Bilgileri info toggle (default kapalı)
- Altta: [Sil (kırmızı, sol)] ... [İptal] [Kaydet]
- Sil → DeleteConfirmDialog (reason zorunlu) → API → List'e redirect
- Kaydet → sayfada kal, snackbar, snapshot refresh
- İptal → dirty check + ConfirmDialog → List'e redirect

### Site List
- **İşlemler kolonu KALDIRILDI** (göz + kalem yok)
- Satıra tıklayınca `/sites/{siteId}` → Detail Edit mode
- "+ Yeni Site" butonu → `/sites/new` → Detail Create mode

## Dosya Listesi

### Yeni/Overwrite (1)
- `Components/Pages/Sites/Detail.razor` — **tamamen yeniden yazıldı**. Form.razor mantığı dahil.

### Değişen (1)
- `Components/Pages/Sites/List.razor` — İşlemler kolonu silindi, `RowClick` eklendi

### SİLİNECEK (1) — MANUEL
- `Components/Pages/Sites/Form.razor` — zip içinde DEĞİL. Uygulandıktan sonra elle silinir.

## Uygulama

```powershell
cd D:\Projects\sitehub

# 1. Zip aç (Detail + List overwrite)
Unblock-File C:\Users\<sen>\Downloads\sitehub-f6-m3.zip
Expand-Archive -Path "C:\Users\<sen>\Downloads\sitehub-f6-m3.zip" `
    -DestinationPath "D:\Projects\sitehub\" -Force

# 2. Form.razor'u sil (route çakışması olur yoksa: "/sites/new" iki yerde tanımlı)
Remove-Item src\SiteHub.ManagementPortal\Components\Pages\Sites\Form.razor

# 3. Build
dotnet build
```

**KRİTİK:** Form.razor silinmeden build yapılırsa **route çakışması** hatası alınır — hem Form hem Detail `/sites/new` route'unu iddia ediyor.

## Smoke Test

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --project src\SiteHub.ManagementPortal
```

### Test 1 — Site List
1. Login, `/organizations` → bir org'un Apartment ikonu
2. Site listesi görünür
3. **İşlemler kolonu YOK**, sadece 7 kolon (Kod/Ad/TicariÜnvan/Adres/VKN/IBAN/Durum)
4. Listede bir satıra tıkla → `/organizations/{orgId}/sites/{siteId}` URL'ine gider

### Test 2 — Edit mode (var olan site)
1. Site List'te satıra tıkla → Detail açılır
2. Breadcrumb: Organizasyonlar › [Org] › Siteler › [Site Adı]
3. Durum badge görünür (Aktif/Pasif)
4. **8 tab** görünür. Ana Bilgiler seçili
5. Alanlar doldurulmuş + **editable**
6. Alt: [Sil] sol kırmızı, [İptal] [Kaydet] sağda
7. Alanı değiştir, Kaydet → snackbar "Güncellendi"
8. İptal → değişiklik varsa dialog, yoksa direkt List'e döner

### Test 3 — Create mode
1. Site List'te "+ Yeni Site" butonuna tıkla
2. `/organizations/{orgId}/sites/new` URL'ine gider
3. Breadcrumb son segment: "Yeni"
4. **Sadece 1 tab** (Ana Bilgiler) görünür — diğer 7 tab YOK
5. Form boş
6. Durum badge YOK (henüz kayıt yok)
7. Sil butonu YOK (edit mode'a özel)
8. Alt: [İptal] [Oluştur]
9. Alanları doldur, Oluştur → snackbar + URL `/organizations/{orgId}/sites/{yeniId}` olur
10. Sayfa Edit mode'a geçer (8 tab görünür), sitede kayıtlı veri görünür

### Test 4 — Sil
1. Edit mode'da Sil butonuna tıkla
2. DeleteConfirmDialog açılır, "Silme sebebi" alanı (min 5 karakter)
3. Sebep yaz, Sil'e bas
4. Snackbar "Site silindi" + List'e redirect
5. List'te o site artık yok (soft delete, includeInactive ile görünür)

### Test 5 — URL manipulation
1. Manuel URL: `/organizations/YANLIS-ORG-GUID/sites/MEVCUT-SITE-GUID`
2. Hata alert "Bu site seçili organizasyona ait değil." + "Site Listesine Dön" butonu

### Test 6 — Create → Edit geçişinde re-init
Kritik test: Blazor same-component navigation re-init.
1. `/sites/new` aç, form doldur, Oluştur
2. URL değişir → `/sites/{yeniId}`
3. Sayfa **yeniden yüklenmeli**: 8 tab görünür olmalı, veriler Edit pattern'inde
4. Boş form kalmaması gerek

`OnParametersSetAsync` bu senaryoyu ele alıyor — test edelim.

## Commit Önerisi

```
Faz F.6 Madde 3: Detail = Form birlestirme

Ayri Site Form sayfasini kaldirdik. Detail.razor tek sayfa olarak
hem Create (/sites/new) hem Edit (/sites/{id}) modlarini yonetiyor.

Davranis:
- Create mode: Sadece Ana Bilgiler tab gorunur (7 placeholder gizli).
- Edit mode: 8 tab gorunur. Ana Bilgiler editable + Kaydet/Iptal/Sil.
- Create kaydedilince -> /sites/{yeniId} redirect (Edit mode'a gecer).
- Silme: DeleteConfirmDialog + reason + SitesApi.DeleteAsync.

Silinen:
- Components/Pages/Sites/Form.razor (665 satir)
  Tum mantik (validation, cascading Il/Ilce, submit, dirty check,
  Unicode normalize, VKN/IBAN validation) Detail'e tasindi.

Yeni/overwrite:
- Components/Pages/Sites/Detail.razor (~750 satir)
  Routes: /sites/new + /sites/{siteId} (her iki page directive).
  SiteId? optional -> _isEditMode => SiteId.HasValue.
  OnParametersSetAsync ile same-component nav re-init (Create -> Edit).
  Form mantigi + tab yapisi + Sistem Bilgileri toggle birlesik.

Degisen:
- Components/Pages/Sites/List.razor
  Islemler kolonu kaldirildi (goz + kalem yok).
  RowClick="OnRowClick" -> navigation /sites/{id}.

Test: Build temiz, 146 test yesil.
Smoke:
  1. Site List satira tiklama -> Edit mode
  2. + Yeni Site -> Create mode -> kaydet -> Edit mode redirect
  3. Edit mode'da Sil -> dialog -> List'e redirect
  4. Iptal dirty check calisiyor

Kapsam disi:
- Feistel URL'leri (Madde 10, ayri seans): /sites/{code} yerine GUID
- Pasife cek / Aktife al (sonraki seans)
```

## Kapsam Dışı

- **Madde 10:** URL'lerde GUID yerine Feistel Code — ayrı seans
- **Pasife Çek / Aktife Al** toggle butonu — şu an sadece soft delete var, IsActive toggle yok
- **Aktif olmayan site'ı tekrar aktifleştirme UI** — yok (backend var: `SitesApi.ActivateAsync`)
