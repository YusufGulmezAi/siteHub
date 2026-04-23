# Faz F.6 C.3 Hotfix — VKN + İl/İlçe Autocomplete

**Tarih:** 2026-04-22
**Scope:** 2 backend + 1 frontend dosyada bug düzeltme.

## Sorunlar (smoke test'te bulunan)

### Sorun 1 — VKN 500 Internal Server Error

**Backend log:**
```
SiteHub.Domain.Identity.InvalidNationalIdException: Geçersiz VKN: 0000000021
   at NationalId.CreateVkn(String value)
   at NationalId.Parse(String value)
   at CreateSiteHandler.Handle()
```

**Kök sebep:** `CreateSiteHandler` ve `UpdateSiteHandler` VKN için
`NationalId.Parse()` kullanıyordu → bu **checksum'lı** `CreateVkn`'e
yönlendiriyor. Dev ortamında padleft edilen "0000000021" gibi sahte
VKN'ler checksum'dan geçmiyor. Exception `InvalidNationalIdException`
tipinde, handler'ın `catch (ArgumentException or FormatException)`
bloğunda yakalanmıyor → dışarı kaçıyor → 500 response (JSON değil
HTML) → UI "'S' is an invalid start of a value" diye parse hatası.

**Düzeltme:**
- `NationalId.Parse` → `NationalId.CreateVknRelaxed` (Organization ile
  tutarlı, PROJECT_STATE §5.5 Öğrenim 6)
- `InvalidNationalIdException` explicit yakalanır, proper `InvalidTaxId`
  failure code döner → UI Türkçe mesaj alır

### Sorun 2 — İl/İlçe dropdown aranabilir değil

81 il arasında scroll ile il bulmak zahmetli. Kullanıcı `MudSelect` yerine
**aranabilir + filtrelenebilir** combobox istiyor.

**Düzeltme:**
- `MudSelect<Guid?>` → `MudAutocomplete<ProvinceDto>` / `<DistrictDto>`
- `SearchFunc` ile case-insensitive filtreleme
- İl için plaka kodu ile de arama (örn "34" → İstanbul)
- `ToStringFunc` ile "34 İSTANBUL" / "Çankaya" gibi görünüm
- `OpenOnFocus="true"` — odaklanınca liste açılır
- `AnchorOrigin="Origin.BottomCenter"` + `TransformOrigin="Origin.TopCenter"` — combobox altında açılır

## Değişen 3 Dosya

| Dosya | Değişiklik |
|---|---|
| `Application/Features/Sites/CreateSiteCommand.cs` | VKN Parse → CreateVknRelaxed + exception handling |
| `Application/Features/Sites/UpdateSiteCommand.cs` | Aynı düzeltme |
| `ManagementPortal/Components/Pages/Sites/Form.razor` | MudSelect → MudAutocomplete (İl + İlçe) |

## Uygulama

```powershell
cd D:\Projects\sitehub

Unblock-File C:\Users\<sen>\Downloads\sitehub-f6-c3-hotfix.zip
Expand-Archive -Path "C:\Users\<sen>\Downloads\sitehub-f6-c3-hotfix.zip" `
    -DestinationPath "D:\Projects\sitehub\" -Force

dotnet build
dotnet test --no-build
```

Build temiz. 146 test yeşil kalmalı.

## Smoke Test (hotfix doğrulama)

### Test 1 — İl Autocomplete (yeni davranış)
1. Yeni Site aç, İl alanına **odaklan** → tüm 81 il liste olarak açılmalı
2. "ist" yaz → sadece İstanbul görünmeli
3. "34" yaz → İstanbul görünmeli (plaka kodu araması)
4. "ankara" yaz → Ankara görünmeli

### Test 2 — İlçe cascading (autocomplete)
1. İstanbul seç → İlçe alanı aktifleşmeli
2. İlçe alanına odaklan → İstanbul'un ilçeleri açılır
3. "kad" yaz → "Kadıköy" filtrelenmeli
4. İl'i Ankara'ya değiştir → İlçe **temizlenmeli**, Ankara'nın ilçeleri yüklenmeli

### Test 3 — VKN hata (backend düzeltmesi)
1. VKN alanına "21" yaz
2. Diğer zorunlu alanları doldur
3. "Oluştur" → Türkçe hata snackbar: "VKN 10 haneli olmalıdır: 0000000021"
4. Beklenmeyen: "'S' is an invalid start..." hatası OLMAMALI

### Test 4 — VKN başarılı
1. VKN: "1234567890" → "Oluştur" → başarılı
2. VKN boş → "Oluştur" → başarılı (opsiyonel)

### Test 5 — Edit mode regresyon
1. Yeni oluşturulan site'ın Edit URL'sine git
2. İl alanı dolu, İlçe alanı dolu (varsa)
3. Dirty check hâlâ çalışıyor mu?

## Commit Önerisi

```
Faz F.6 C.3 hotfix: VKN backend fix + Il/Ilce autocomplete

Sorun 1: Backend VKN icin NationalId.Parse() kullaniyordu, '0000000021'
gibi sahte VKN'ler checksum'dan gecmiyor, InvalidNationalIdException
handler'da yakalanmiyor -> 500 -> UI parse hatasi.

Duzeltme:
- Parse -> CreateVknRelaxed (Organization ile tutarli, S5.5 Ogrenim 6)
- InvalidNationalIdException explicit yakalanir -> InvalidTaxId failure

Sorun 2: 81 il dropdown'da scroll'la zor.

Duzeltme:
- MudSelect -> MudAutocomplete
- Il: ad veya plaka kodu ile aranabilir
- Ilce: il'e gore filtrelenir, il degisince otomatik temizlenir

Degisen 3 dosya:
- CreateSiteCommand.cs + UpdateSiteCommand.cs: VKN fix
- Sites/Form.razor: MudSelect -> MudAutocomplete

Test: Build temiz, 146 test yesil.
```
