# Faz F.6 C.3 — Site Form (Create + Edit) + Cascading IL/İlçe

**Tarih:** 2026-04-22
**Scope:** UI — Yeni Site oluşturma ve düzenleme sayfası + Site List'te alt metin sadeleştirme.

## Değişen 1 Dosya + Yeni 1 Dosya

| Dosya | Durum | Açıklama |
|---|---|---|
| `Components/Pages/Sites/List.razor` | Değişti | Alt metin kaldırıldı + "+ Yeni Site" butonu eklendi |
| `Components/Pages/Sites/Form.razor` | **YENİ** | Create + Edit tek sayfa, cascading IL/İlçe + dirty check |

## Routes

```
/organizations/{OrganizationId}/sites/new              → Create mode
/organizations/{OrganizationId}/sites/{SiteId}/edit    → Edit mode
```

Her iki route'ta OrganizationId URL'den. Edit mode'da Site ayrıca doğru
OrganizationId'ye ait mi kontrol ediliyor (URL manipulation koruması).

## Form Alanları (Site domain'ine göre)

**Zorunlu:**
| Alan | Icon | Validation |
|---|---|---|
| Site Adı | `Apartment` | Required, max 200 |
| İl | `Map` | Required, dropdown (IL listesi) |
| Adres | `LocationOn` | Required, max 1000, 2 lines |

**Opsiyonel:**
| Alan | Icon | Validation |
|---|---|---|
| Ticari Ünvan | `Gavel` | Max 500 |
| İlçe | `Place` | Cascading (İl'e göre yüklenir), Clearable |
| VKN | `Badge` | 10 hane, sadece rakam, tamamı 0 olamaz |
| IBAN | `AccountBalance` | TR + 24 rakam, boşluk otomatik normalize |

## Cascading IL/İlçe Mekaniği

```
Sayfa açılır
  ↓
Paralel: OrganizationsApi.GetByIdAsync + GeographyApi.GetProvincesAsync
  ↓
Edit mode ise: SitesApi.GetByIdAsync → detail.ProvinceId → GetDistrictsByProvinceAsync
  ↓
Kullanıcı il değiştirir
  ↓
_model.DistrictId = null (eski seçim temizlenir)
_districts.Clear()
GeographyApi.GetDistrictsByProvinceAsync(newId) → _districts yeniden yüklenir
```

**URL manipulation koruması:** Edit mode'da yüklenen Site'ın `OrganizationId`'si
URL'deki OrganizationId ile eşleşmiyorsa hata alert'i + navigate away.

## Yeni Buton Standartları (burada uygulandı)

Senin kararın (UX standart):
- **Kaydet/Oluştur** → **onay YOK**, direkt submit (form validation zaten yeterli)
- **Vazgeç** → `Color.Warning` (amber) + dirty check
  - Değişiklik yoksa direkt navigate
  - Değişiklik varsa ConfirmDialog: "Sayfadan ayrılmak mı istiyorsunuz?"
    - "Evet, Vazgeç" (Warning amber) + "Hayır, Devam Et"

**Dirty check:** Organization Form pattern'iyle aynı — `_initialSnapshot + Clone + EqualsByFields`.

## Site List.razor Revizyonu

- **Kaldırıldı:** Alt metin ("ABC Yonetim — yönetilen apartman/kompleks listesi")
- **Eklendi:** "+ Yeni Site" butonu → `/organizations/{OrganizationId}/sites/new`
  - Organization yüklenmemişse disabled (guard)

## Uygulama

```powershell
cd D:\Projects\sitehub

Unblock-File C:\Users\<sen>\Downloads\sitehub-f6-c3.zip
Expand-Archive -Path "C:\Users\<sen>\Downloads\sitehub-f6-c3.zip" `
    -DestinationPath "D:\Projects\sitehub\" -Force

dotnet build
```

Build temiz olmalı. Test etkilenmez (UI only).

## Smoke Test (kapsamlı)

```powershell
dotnet run --project src\SiteHub.ManagementPortal
```

### Test 1 — Site List revizyon
1. `/organizations` → Apartment ikonu → Site List sayfası
2. Başlık sadece "Siteler" olmalı (alt metin YOK)
3. Sağda "**+ Yeni Site**" butonu görünmeli

### Test 2 — Create mode açılış
1. "+ Yeni Site" → URL `/organizations/{orgId}/sites/new` olmalı
2. Breadcrumb: **Organizasyonlar** › **ABC Yonetim** › **Siteler** › **Yeni**
3. 3 link (Organizasyonlar, ABC Yonetim, Siteler) tıklanabilir
4. Form alanları boş, İlçe dropdown **disabled** ("Önce il seçiniz.")
5. Başlık: "Yeni Site"

### Test 3 — Cascading IL/İlçe (Create)
1. İl dropdown'u açılmalı — alfabetik (81 il)
2. "06 Ankara" seç → İlçe dropdown aktifleşmeli, Ankara ilçeleri yüklenmeli
3. "Çankaya" seç → DistrictId set olur
4. İl'i "34 İstanbul" olarak değiştir
5. **İlçe seçimi SIFIRLANMALI** ve İstanbul ilçeleri yüklenmeli (Ankara'nın ilçeleri gitmelí)

### Test 4 — Create submit
1. Tüm zorunlu alanları doldur:
   - Site Adı: "Deneme Sitesi"
   - İl: "34 İstanbul"
   - Adres: "Test Mahallesi, Test Sokak No:1"
2. Opsiyoneller isteğe bağlı: Ticari Ünvan, İlçe, VKN, IBAN
3. "Oluştur" tıkla → onay SORULMAMALI
4. Başarı snackbar "Site oluşturuldu. Kod: XXXXXX"
5. Site List sayfasına otomatik dönüş
6. Liste güncellenmiş, yeni site en üstte

### Test 5 — Edit mode açılış
1. Liste'de yeni oluşturduğun site üzerine git
2. Manuel URL ile aç: `/organizations/{orgId}/sites/{siteId}/edit` (C.5 Detail gelmeden liste'de Düzenle ikonu yok)
3. Form alanları dolu gelmelı
4. İlçe dropdown'u dolu (Site'ın il'inin ilçeleri)
5. Başlık: "Site Düzenle" + Kod + Oluşturma tarihi
6. Breadcrumb'da "Düzenle" sonda

### Test 6 — Edit + Dirty Check (Warning dialog)
1. Form'da hiçbir şey değiştirme
2. "Vazgeç" → **dialog YOK**, direkt Site List'e dön
3. Tekrar aç, "Site Adı"na bir karakter ekle
4. "Vazgeç" → **dialog ÇIKMALI** (amber "Evet, Vazgeç" + "Hayır, Devam Et")
5. "Hayır, Devam Et" → formda kal
6. "Vazgeç" tekrar → "Evet, Vazgeç" → Site List'e dön

### Test 7 — Validation
1. Yeni Site aç
2. Hiçbir alan doldurmadan "Oluştur" → 3 hata mesajı (Ad, İl, Adres zorunlu)
3. VKN'ye "0000000000" yaz → "VKN tamamen sıfır olamaz" hatası
4. VKN'ye "abc" yaz → "VKN sadece rakam içermeli" hatası
5. IBAN'a "1234" yaz → "IBAN 26 karakter olmalı (TR + 24 rakam)" hatası
6. IBAN'a "TR330006100519786457841326" yaz → hata yok, submit'e hazır

### Test 8 — URL manipulation koruması
1. Manuel URL: `/organizations/FARKLI-ORG-ID/sites/MEVCUT-SITE-ID/edit`
2. "Bu site seçili organizasyona ait değil" hata alert'i
3. "Site Listesine Dön" butonu çalışmalı

## Commit Önerisi

```
Faz F.6 C.3: Site Form (Create + Edit) + cascading IL/Ilce

Yeni:
- Components/Pages/Sites/Form.razor
  URL'ler:
    /organizations/{orgId}/sites/new
    /organizations/{orgId}/sites/{siteId}/edit

  Alanlar: Ad (req), Ticari Unvan (opt), Il (req dropdown),
  Ilce (opt cascading), Adres (req), VKN (opt), IBAN (opt).

  Cascading IL/Ilce:
  - Sayfa acildiginda paralel: Organization adi + Il listesi
  - Edit mode: Site detay + il'e gore ilce listesi
  - Il degisince ilce seciminin sifirlanmasi + yeni ilceler
  - IGeographyApi.GetProvincesAsync + GetDistrictsByProvinceAsync

  URL manipulation korumasi: Edit mode'da Site.OrganizationId !=
  URL.OrganizationId ise hata + exit.

  Validation:
  - VKN opsiyonel (Organization'dan farkli), 10 hane, rakam, != 0000000000
  - IBAN opsiyonel, TR + 24 rakam, auto-normalize (uppercase, space kaldir)

  Buton standartlari (yeni kural):
  - Kaydet/Olustur: onay YOK, direkt submit
  - Vazgec: Warning (amber) + dirty check (form degismediyse onaysiz cik)

Degisen:
- Components/Pages/Sites/List.razor
  Alt metin kaldirildi (sade baslik)
  '+ Yeni Site' butonu eklendi -> /organizations/{orgId}/sites/new
  Organization yuklenmediyse disabled

Dokunulmayan: Domain logic, backend endpoints, test dosyalari.

Test: Build temiz, 146 test yesil.
Smoke: 8 senaryo (list rev, create acilis, cascading, create submit,
       edit acilis, dirty check, validation, url manipulation).
```
