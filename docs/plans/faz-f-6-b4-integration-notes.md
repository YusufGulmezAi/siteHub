# Faz F.6 B.4 — Organization Detail + Delete/Activate UI

**Tarih:** 2026-04-22
**Kapsam:** Detail sayfası + Delete (reason zorunlu) + Activate/Deactivate UI

## Eklenen Dosyalar (yeni 3)

### 1. `src/SiteHub.ManagementPortal/Components/Pages/Organizations/Detail.razor`
- Route: `/organizations/{Id:guid}`
- Readonly 3 kart: Firma Bilgileri / İletişim / Sistem Bilgileri
- Üst action bar: Listeye Dön / Düzenle / Aktif↔Pasif / Sil
- Durum chip'i: Aktif (yeşil) veya Pasif (turuncu)
- **Typed API client** (`IOrganizationsApi`) — `ff81635`'teki altyapı kullanılıyor
- **Prerender tuzağına karşı** `OnAfterRenderAsync(firstRender)` pattern (PROJECT_STATE §5 öğrenim 1)

### 2. `src/SiteHub.ManagementPortal/Components/Shared/Dialogs/ConfirmDialog.razor`
- Generic basit onay (Activate / Deactivate / ileride diğer reversible işlemler)
- Parametreler: `Message`, `ConfirmText`, `CancelText`, `ConfirmColor`, `ConfirmIcon`
- Return: `DialogResult.Ok(true)` veya `Cancel`

### 3. `src/SiteHub.ManagementPortal/Components/Shared/Dialogs/DeleteConfirmDialog.razor`
- **Zorunlu reason input** (MudTextField, min 5 karakter, anlık validation)
- "Sil" butonu input geçersizken **disabled**
- Warning alert: "Bu işlem soft delete'tir — audit log'a yazılır"
- Return: `DialogResult.Ok(string reason)` veya `Cancel`
- Site/Resident silme UI'larında da yeniden kullanılacak

## Kararlar

### Karar 1 — Delete reason **zorunlu** ✅
PROJECT_STATE §6 B.4 spec'ine uyuldu. Kullanıcı `MudTextField` ile
silme sebebini yazar (min 5 karakter), audit log'a düşer. Kısaltılmış veya
sahte reason'a karşı min karakter kontrolü var.

### Karar 2 — Detail "Silindi" state'i YOK
`OrganizationDetailDto`'da `DeletedAt` alanı yok; `GetByIdAsync` soft-delete
edilmiş kayıt için `null` döner. Yani Detail sayfasındaysan kayıt canlıdır.
Silinmiş bir kayda direkt URL ile erişilirse sayfa "bulunamadı" hatası gösterir.

### Karar 3 — Destructive actions sadece Detail'da
Listeden doğrudan Sil/Pasif yapılamaz; kullanıcı önce Detail'a girer.
Kazara silme riski düşer. Liste'de sadece Detay + Düzenle satır action'ları.

### Karar 4 — ConfirmDialog vs DeleteConfirmDialog ayrı
Basit onaylarda (Activate/Deactivate) Shared/Dialogs/ConfirmDialog.razor
yeterli. Delete için reason input gerektiğinden ayrı component
(DeleteConfirmDialog.razor). İki component farklı return semantiği (bool vs string).

### Karar 5 — `DeleteOrganizationRequest` signature varsayımı
`new DeleteOrganizationRequest(reason)` — positional constructor ile çağırılıyor.
Eğer Contracts tarafında farklıysa (ör. `{ Reason = reason }` init), build hatası
düzeltme gerekebilir. Build logunu paylaşın.

## List.razor — Gerekli Patch

`Components/Pages/Organizations/List.razor` dosyasındaki göz ikonunu
Edit'ten Detail'e yönlendirin.

**Bulup değiştirin** (muhtemel mevcut hâli):

```razor
<MudTooltip Text="Düzenle">
    <MudIconButton Icon="@Icons.Material.Filled.Visibility"
                   Size="Size.Small"
                   OnClick="@(() => Nav.NavigateTo($"/organizations/{item.Id}/edit"))" />
</MudTooltip>
```

**Yeni hâli (iki ayrı button):**

```razor
<MudTooltip Text="Detay">
    <MudIconButton Icon="@Icons.Material.Filled.Visibility"
                   Size="Size.Small"
                   Color="Color.Primary"
                   OnClick="@(() => Nav.NavigateTo($"/organizations/{item.Id}"))" />
</MudTooltip>
<MudTooltip Text="Düzenle">
    <MudIconButton Icon="@Icons.Material.Filled.Edit"
                   Size="Size.Small"
                   Color="Color.Default"
                   OnClick="@(() => Nav.NavigateTo($"/organizations/{item.Id}/edit"))" />
</MudTooltip>
```

**Not:** `@inject NavigationManager Nav` zaten List.razor'da olmalı (B.3'te
"geçici edit'e yönlendirme" için eklenmişti).

## Build + Smoke Test

### Build

```powershell
cd D:\Projects\sitehub
Unblock-File C:\Users\<sen>\Downloads\sitehub-f6-b4-v2.zip
Expand-Archive -Path "C:\Users\<sen>\Downloads\sitehub-f6-b4-v2.zip" `
    -DestinationPath "D:\Projects\sitehub\" -Force
dotnet build
```

Build hatası beklenen iki olası nokta:
- `DeleteOrganizationRequest` positional constructor yoksa → `{ Reason = reason }` ile çağırın
- `_Imports.razor` içinde `@using MudBlazor` yoksa sayfanın başına ekleyin

### Smoke Test (9 adım)

1. **Login** → `/organizations`
2. Aktif bir firmanın **göz (Detay)** ikonuna tıkla → Detail sayfası açılmalı
3. **Breadcrumb** görünür: Ana Sayfa → Firmalar → Detay
4. **Chip** yeşil "Aktif" olmalı; 3 kart dolu (ad, VKN, adres, CreatedByName, tarih)
5. **"Pasif Yap"** butonuna tıkla → ConfirmDialog → "Pasif Yap" → snackbar + chip turuncu "Pasif"
6. **"Aktif Yap"** → onay → chip tekrar yeşil
7. **"Düzenle"** → `/organizations/{id}/edit` formuna gitmeli (B.3 formu)
8. Geri dön, **"Sil"** → DeleteConfirmDialog açılır:
   - Boş reason ile "Sil" butonu **disabled** olmalı
   - 4 karakter yazınca hâlâ disabled ("en az 5" mesajı)
   - 5+ karakter → buton enabled → "Sil" tıkla → snackbar + `/organizations` redirect
9. Listede silinen firma **görünmemeli** (soft delete filter).
   Direkt URL `/organizations/{silinenId}` → "Aranan firma bulunamadı" hatası + "Listeye Dön"

### Regresyon kontrolü
- Listede "Detay" + "Düzenle" ikonları yan yana görünmeli (iki ayrı buton)
- Göz ikonuna tıklayınca Edit'e değil **Detail**'e gitmeli
- Kalem ikonuna tıklayınca Edit'e gitmeli (eskisi gibi)

## Bilinen İşler / İleri Planlar

- [ ] **ConfirmDialog / DeleteConfirmDialog** Site UI'da (F.6 C) aynen kullanılacak
- [ ] **Impersonation banner** (ADR-0014) — admin başka org'a geçtiğinde Detail'da uyarı
- [ ] **Yetki kontrolü** — Delete/Deactivate sadece belirli rollere açık olacak (Faz F+)
- [ ] **Cascade uyarısı** — Firma silinmeden önce altındaki Site sayısı gösterilsin
  (backend DTO'ya `siteCount` eklendiğinde; şu an yok)
- [ ] **Timezone** — ADR-0007 prod sunucuda Europe/Istanbul veya UTC şart

## PROJECT_STATE Güncellemesi (commit öncesi manuel)

`PROJECT_STATE.md`'de şu satırları güncelle:

```diff
- **Son güncelleme:** 2026-04-22 (F.6 A.1 → F.6 B.3 tamamlandı — Organization UI)
+ **Son güncelleme:** 2026-04-22 (F.6 B.4 tamamlandı — Organization Detail + Delete/Activate UI)
```

§5 Tamamlanan Fazlar tablosuna:
```
| F.6 B.4 | Organization Detail + Delete/Activate UI | ✅ | <yeni-hash> |
```

§6'da "Sıradaki İş" başlığını değiştir:
```
## 6. Sıradaki İş — Faz F.6 C: Site CRUD UI
```

§7'de checkbox işaretle:
```
- [x] **F.6 B.4** Organization Detail + Delete/Activate UI → <yeni-hash>
```

§13 "Bilinen İşler" altından bu üç satırı **sil** (artık bitti):
```
- [ ] Organization Detail sayfası
- [ ] Organization Delete UI
- [ ] Organization Activate/Deactivate toggle
```

## Commit Önerisi

```
Faz F.6 B.4: Organization Detail sayfasi + Delete/Activate UI

Yeni:
- Components/Pages/Organizations/Detail.razor
  * Readonly 3 kart (Firma/Iletisim/Sistem), action bar, durum chip'i
  * Typed IOrganizationsApi kullanimi (F.6 A.2 altyapisi)
  * OnAfterRenderAsync pattern (prerender auth tuzagina karsi)
- Components/Shared/Dialogs/ConfirmDialog.razor
  * Generic onay (Activate/Deactivate icin)
- Components/Shared/Dialogs/DeleteConfirmDialog.razor
  * Zorunlu reason input (MudTextField, min 5 karakter, anlik validation)
  * Soft delete uyari alert'i

Degisen:
- Components/Pages/Organizations/List.razor
  * Goz ikonu: /organizations/{id}/edit -> /organizations/{id}
  * Yeni kalem ikonu /edit icin eklendi (iki ayri buton)

Kararlar:
- Reason zorunlu (PROJECT_STATE spec'ine uygun, audit trail icin)
- Detail sayfasi 'Silindi' state'i yok (DTO'da DeletedAt yok, 404 ile handle)
- Destructive action'lar sadece Detail sayfasinda (kazara silme riski dusurur)

Site/Resident CRUD UI'larinda ConfirmDialog + DeleteConfirmDialog aynen
yeniden kullanilacak.
```
