# F.6 Madde 6 — Organization Detail = Form Birleştirme

**Tarih:** 2026-04-24
**Scope:** 1 yeni/overwrite (Detail.razor), 1 değişen (List.razor), **1 SİLİNECEK** (Form.razor).

## Amaç

Site Madde 3 pattern'i Organization için de uygulanıyor. Ayrı Create/Edit form sayfası kaldırıldı. Detail.razor tek sayfada Create ve Edit modlarını yönetir.

## Davranış

### Create mode (`/organizations/new`)
- Sadece **Ana Bilgiler** tab'ı görünür (2 ek tab gizli)
- Alanlar editable (boş form)
- Altta: [İptal] [Oluştur]
- Kaydedince → `/organizations/{yeniId}` redirect → Edit mode

### Edit mode (`/organizations/{OrganizationId}`)
- **3 tab:** Ana Bilgiler, Sözleşme & Hizmet (placeholder), Belgeler (placeholder)
- Ana Bilgiler editable (Firma Adı, Ticari Ünvan, VKN, Telefon, Email, Adres)
- Durum badge üstte (Aktif/Pasif)
- Sistem Bilgileri info toggle (default kapalı)
- Altta: [Sil (kırmızı, sol)] [MudSpacer] [İptal] [Kaydet]
- Sil → DeleteConfirmDialog başlığı **"Yönetim Firmasını Sil"** → reason (min 5 karakter) → API → List redirect
- Kaydet → sayfada kal, snackbar, snapshot refresh
- İptal → dirty check + ConfirmDialog → List redirect

### List
- **İşlemler kolonunda sadece "Siteler" (Apartment) ikonu** — Detay + Düzenle kaldırıldı
- Satıra tıklayınca `/organizations/{id}` → Detail (Edit mode)
- Apartment ikonu `@onclick:stopPropagation="true"` ile satır click'ini engelliyor (sadece Sites'a gider)

## Dosya Listesi

### Overwrite (1)
- `Components/Pages/Organizations/Detail.razor` — **tamamen yeniden yazıldı**. Form.razor mantığı + tab yapısı + Sistem Bilgileri toggle + Sil butonu birleşik.

### Değişen (1)
- `Components/Pages/Organizations/List.razor` — İşlemler kolonu sadeleşti (sadece Apartment), RowClick eklendi.

### SİLİNECEK (1) — MANUEL
- `Components/Pages/Organizations/Form.razor` — zip içinde DEĞİL. Uygulandıktan sonra elle silinir.

## Uygulama

```powershell
cd D:\Projects\sitehub

# 1. Zip aç
Unblock-File C:\Users\Yusuf\Downloads\sitehub-f6-m6.zip
Expand-Archive -Path "C:\Users\Yusuf\Downloads\sitehub-f6-m6.zip" -DestinationPath "D:\Projects\sitehub\" -Force

# 2. Form.razor'u sil (route çakışması olur yoksa: "/organizations/new" iki yerde tanımlı)
Remove-Item src\SiteHub.ManagementPortal\Components\Pages\Organizations\Form.razor

# 3. Build
dotnet build
```

**KRİTİK:** Form.razor silinmeden build yapılırsa **route çakışması** hatası alınır.

## Smoke Test

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --project src\SiteHub.ManagementPortal
```

### Test 1 — Organizations List
1. `/organizations` aç
2. İşlemler kolonunda **sadece Apartment (Siteler)** ikonu, göz + kalem **YOK**
3. Bir satıra tıkla → `/organizations/{id}` URL'ine gider
4. Apartment ikonuna tıkla → `/organizations/{id}/sites` URL'ine gider (satır click'i tetiklenmemeli)

### Test 2 — Edit mode
1. Satır tıkla → Detail açılır
2. Breadcrumb: Yönetim Firmaları › [Ad]
3. Durum badge görünür
4. **3 tab** görünür, Ana Bilgiler seçili, alanlar dolu + editable
5. Altta [Sil (kırmızı, sol)] [İptal] [Kaydet]
6. Sistem Bilgileri info ikonuna tıkla → açılır (Firma Kodu, CreatedAt, ...)
7. Alan değiştir, Kaydet → "Güncellendi" snackbar, sayfada kal
8. İptal → değişiklik varsa dialog, yoksa List'e döner

### Test 3 — Create mode
1. "+ Yeni Yönetim Firması" → `/organizations/new`
2. Breadcrumb: Yönetim Firmaları › Yeni
3. **Sadece 1 tab** (Ana Bilgiler) görünür
4. Form boş, Sil butonu **YOK**
5. Altta [İptal] [Oluştur]
6. Doldur + Oluştur → snackbar + URL `/organizations/{yeniId}` olur
7. Sayfa **Edit mode**'a geçer (3 tab görünür, Sil görünür)

### Test 4 — Sil
1. Edit mode → Sil
2. DeleteConfirmDialog başlığı: **"Yönetim Firmasını Sil"**
3. Reason min 5 karakter, yaz + Sil
4. Snackbar "Firma silindi" + List'e redirect

### Test 5 — URL manipulation
1. Manuel URL: `/organizations/YANLIS-GUID`
2. Hata alert "Firma yüklenemedi..." + "Listeye Dön" butonu

## Commit Önerisi

```
Faz F.6 Madde 6: Organization Detail = Form birlestirme

Site Madde 3 (c59a22c) pattern'inin Organization versiyonu. Ayri
Organizations Form sayfasini kaldirildi. Detail.razor tek sayfa olarak
hem Create (/organizations/new) hem Edit (/organizations/{id}) modlarini
yonetiyor.

Davranis:
- Create mode: Sadece Ana Bilgiler tab gorunur (2 placeholder gizli)
- Edit mode: 3 tab (Ana Bilgiler + Sozlesme&Hizmet + Belgeler)
- Create kaydedilince -> /organizations/{yeniId} redirect
- Silme: DeleteConfirmDialog "Yonetim Firmasini Sil" + reason

Silinen:
- Components/Pages/Organizations/Form.razor (500 satir)
  Tum mantik (validation, Phone mask, TaxId normalize, dirty check)
  Detail'e tasindi.

Yeni/overwrite:
- Components/Pages/Organizations/Detail.razor (~650 satir)
  Routes: /organizations/new + /organizations/{OrganizationId}
  OrganizationId? optional -> _isEditMode => OrganizationId.HasValue
  OnParametersSetAsync ile Create -> Edit same-component re-init
  Form mantigi + tab yapisi + Sistem Bilgileri info toggle birlesik
  Butonlar: Sil sol kirmizi (edit only), Iptal/Kaydet sagda

Degisen:
- Components/Pages/Organizations/List.razor
  Islemler kolonu sadelesti: Detay + Duzenle kaldirildi, sadece
  Apartment (Siteler) kaldi. RowClick="OnRowClick" -> /organizations/{id}
  Apartment ikonu @onclick:stopPropagation ile satir click'ini engelliyor.

Test: Build temiz.
Smoke: Satira tiklama -> Edit, + Yeni -> Create -> kaydet -> Edit redirect,
       Sil -> "Yonetim Firmasini Sil" dialog -> List, Iptal dirty check.

Kapsam disi:
- Madde 11: Organization domain genisletme (Sozlesme&Hizmet tabi burada
  placeholder, iceriği sonra gelecek)
- Madde 9: URL'lerde Feistel Code (GUID yerine)
```
