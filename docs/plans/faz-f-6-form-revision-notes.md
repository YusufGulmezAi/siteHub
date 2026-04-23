# Faz F.6 Form Revizyonu — Organization Form + Detail breadcrumb

**Tarih:** 2026-04-22
**Scope:** UI iyileştirmesi — Form sayfasını Detail ile aynı düzende yapmak, breadcrumb ekleme, "Vazgeç" butonu + dirty check, label düzeltme.

## Değişen 2 Dosya

| Dosya | Değişiklik |
|---|---|
| `Components/Pages/Organizations/Detail.razor` | Breadcrumb label: "Firmalar" → "Organizasyonlar" (1 kelime) |
| `Components/Pages/Organizations/Form.razor` | Tam revizyon: layout + breadcrumb + icons + Vazgeç + dirty check |

## Form.razor Değişiklikleri (detay)

### 1. Layout — Detail ile hizalı
- Eski: `MudPaper Style="max-width:960px"` (inline style, ortalı değil)
- Yeni: `MudContainer MaxWidth="MaxWidth.Large"` + `mt-4` (Detail.razor ile birebir)
- Tüm içerik breadcrumb + başlık + form kartı düzeninde, ekran ortasında

### 2. Breadcrumb eklendi
```
Organizasyonlar › Yeni        (Create mode)
Organizasyonlar › Düzenle     (Edit mode)
```
"Organizasyonlar" tıklanabilir → `/organizations` listesine gider. MudBlazor 9.0.0
silent render fail'ı için primitif bileşenlerle (MudLink + MudIcon + MudText) —
Detail.razor ile aynı pattern.

### 3. Input'lara ikon eklendi
| Alan | Icon |
|---|---|
| Firma Adı | `Icons.Material.Filled.Business` |
| Ticari Ünvan | `Icons.Material.Filled.Gavel` (yasal/resmi vurgusu) |
| VKN | `Icons.Material.Filled.Badge` |
| Telefon | `Icons.Material.Filled.Phone` |
| E-posta | `Icons.Material.Filled.Email` |
| Adres | `Icons.Material.Filled.LocationOn` |

Hepsi `Adornment.Start` — input'un başında.

### 4. Spacing standartlaştı
- Eski: `Margin.Dense` (sıkışık, dev tarafı)
- Yeni: `Margin.Normal` (Material Design standart, rahat ama israf değil)

Bu pattern tüm gelecek form sayfalarında (Site Form, Unit Form, Residency Form vb.) aynı kalacak.

### 5. "İptal" → "Vazgeç" (Warning/amber)
- Eski: `Variant.Text` + gri "İptal" + direkt navigate
- Yeni: `Variant.Filled` + `Color.Warning` + `Icon.Cancel` + "Vazgeç" + **dirty check'li** navigate

**Warning palette** tema uyumlu — MudTheme navy'de `--mud-palette-warning` zaten amber/kehribar tonunda render oluyor. Hardcoded hex yok, tema değişse yine uyumlu.

### 6. Dirty Check mekaniği [YENİ ÖZELLİK]

**FormModel.Clone() + EqualsByFields()** ile:
- Load sonrası veya create mode başlangıcında `_initialSnapshot` alınır
- "Vazgeç" tıklanınca `IsDirty()` → snapshot ≠ current model
- **Değişiklik yok** → direkt `/organizations`'a git (onay sormaz)
- **Değişiklik var** → `ConfirmDialog` açılır:
  - Title: "Sayfadan ayrılmak mı istiyorsunuz?"
  - Message: "Yaptığınız değişiklikler kaydedilmeyecek. Devam edilsin mi?"
  - ConfirmText: "Evet, Vazgeç" (Warning renk)
  - CancelText: "Hayır, Devam Et"
  - Evet → navigate, Hayır → formda kal

**Edge case:** Kullanıcı bir alana girdi yazıp sonra silerse, snapshot'a eşit olur
→ dirty değil, onaysız çıkar. Bu iyi UX.

**Edge case 2:** Browser geri butonu / sekme kapatma dirty check'ten geçmez —
bu daha karmaşık (beforeunload + JS interop). Şu an kapsam dışı.

### 7. Tarih formatı
- Eski: `_loadedDetail.CreatedAt.ToString("dd.MM.yyyy HH:mm")` (UTC gösterir)
- Yeni: `_loadedDetail.CreatedAt.LocalDateTime.ToString("dd.MM.yyyy HH:mm")` (TR local)

ADR-0007 ile tutarlı (UTC storage, TR display).

## Dokunulmayan Özellikler (önemli)

- Tüm validation logic (TaxId, Phone, Email) — aynen kaldı
- Submit logic (Create/Update) — aynen kaldı
- VKN padleft (`NormalizeTaxId`) — aynen kaldı
- Telefon mask + CleanDelimiters — aynen kaldı
- Prerender tuzağı için `OnAfterRenderAsync(firstRender)` — aynen kaldı
- MudForm Validation + IsValid check — aynen kaldı

## Uygulama

```powershell
cd D:\Projects\sitehub

Unblock-File C:\Users\<sen>\Downloads\sitehub-f6-form-revision.zip
Expand-Archive -Path "C:\Users\<sen>\Downloads\sitehub-f6-form-revision.zip" `
    -DestinationPath "D:\Projects\sitehub\" -Force

dotnet build
```

Build temiz geçmeli. Test kırılma beklenmiyor (UI değişikliği, domain/logic yok).

## Smoke Test (manuel)

Portal'ı başlat, `/organizations` listesine git:

### Test 1 — Detail breadcrumb
1. Bir firmanın göz ikonuna tıkla
2. Breadcrumb "**Organizasyonlar** › Detay" olmalı (eski "Firmalar" yerine)
3. "Organizasyonlar" tıklanabilir, `/organizations`'a döner

### Test 2 — Form layout (Create)
1. "+ Yeni Organizasyon" tıkla
2. Sayfa ortalı (MaxWidth Large, Detail ile aynı)
3. Üstte breadcrumb: "Organizasyonlar › Yeni"
4. Form kartında 6 alan, hepsi sol tarafında ikon var
5. Alt sağda 2 buton: amber "Vazgeç" + mavi "Oluştur"

### Test 3 — Form layout (Edit)
1. Kalem ikonuna tıkla
2. Sayfa ortalı, breadcrumb: "Organizasyonlar › Düzenle"
3. Alanlar dolu, kod + oluşturma tarihi başlıkta gösteriliyor
4. Alt sağda 2 buton: amber "Vazgeç" + mavi "Kaydet" (ikon Save)

### Test 4 — Dirty check (Edit mode)
1. Düzenle sayfasına git, hiçbir alanı değiştirme
2. "Vazgeç" tıkla → dialog YOK, direkt listeye döner ✅
3. Tekrar gir, "Firma Adı"na bir karakter ekle
4. "Vazgeç" tıkla → dialog ÇIKAR
5. "Hayır, Devam Et" → formda kal, değişiklik korunur ✅
6. Tekrar "Vazgeç" → "Evet, Vazgeç" → listeye döner, değişiklik kaydedilmez ✅

### Test 5 — Dirty check (Create mode)
1. Yeni Organizasyon sayfasını aç, hiçbir şeye yazma
2. "Vazgeç" tıkla → dialog YOK, direkt döner ✅
3. Tekrar aç, "Firma Adı"na bir şey yaz
4. "Vazgeç" → dialog ÇIKAR
5. "Hayır" → formda kal ✅

### Test 6 — Edge case (girdi + sil)
1. Düzenle sayfasında bir alana "x" yaz
2. "x"i sil (alanı orijinal haline döndür)
3. "Vazgeç" tıkla → dialog YOK, direkt döner ✅ (snapshot eşleşir)

## Commit Önerisi

```
Faz F.6 Form Revizyon: Detail ile hizali layout + Vazgec + dirty check

Detail.razor:
- Breadcrumb label: "Firmalar" -> "Organizasyonlar" (tutarli terminoloji)

Form.razor (buyuk revize):
- MudContainer Large + ortalanmis (Detail ile ayni duzen)
- Breadcrumb: Organizasyonlar > Yeni/Duzenle
- Input'larda icon: Business, Gavel, Badge, Phone, Email, LocationOn
- Margin.Dense -> Margin.Normal (Material Design standart spacing)
- "Iptal" (Text gri) -> "Vazgec" (Filled Warning/amber)
- Dirty check: form'a dokunulmadi veya degisiklik geri alindi -> onaysiz
  cikar; degisiklik var -> ConfirmDialog ile onay al
- Tarih gosterimi UTC -> LocalDateTime (ADR-0007)

Kalan ayni:
- Validation logic (TaxId, Phone, Email) - dokunulmadi
- Submit logic (Create/Update)
- VKN padleft, phone mask, prerender handling

Tasarim standardi:
Bu pattern tum form sayfalarinda (Site Form, Unit Form, vb.)
aynen kullanilacak - tutarli UX.

Test: Build temiz. Manuel smoke test 6 senaryoda yesil.
```
