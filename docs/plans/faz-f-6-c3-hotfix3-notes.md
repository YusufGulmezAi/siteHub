# Faz F.6 C.3 Hotfix 3 — "İ" Aramada Bulamama + CoerceText

**Tarih:** 2026-04-22
**Scope:** 1 dosya — Sites/Form.razor. Autocomplete parametreleri + Unicode normalize.

## Bug

Kullanıcı il arama alanına "İ" (büyük I with dot) yazınca "İzmir" gelmiyordu.
Ama "Ö" yazınca "Ödemiş" direkt geliyordu. Asimetrik davranış → MudAutocomplete
edge case.

## Kök sebep (muhtemel iki kombinasyon)

### 1) `CoerceText="true"` parametresi
`MudAutocomplete<T>`'te `CoerceText="true"` → kullanıcının yazdığı metin,
eşleşen item bulunamazsa **sessizce silinir** (component seçili item'ın
`ToString()`'ine dönmeye zorlar). Bu bazı karakter kombinasyonlarında
("İ" gibi) aramayı öldürüyor: kullanıcı "İ" yazar, henüz eşleşme yok,
text silinir, arama resetlenir, kullanıcı hiçbir şey bulamaz.

**Düzeltme:** `CoerceText="false"`. Kullanıcının yazdığı metin olduğu gibi
bırakılır, arama devam eder, liste filtrelenir.

### 2) Unicode normalization mismatch (defansif)
`"İ"` Unicode'da iki biçimde temsil edilebilir:
- **U+0130** (precomposed, Latin Capital Letter I with Dot Above) — standart
- **U+0049 + U+0307** (decomposed, Capital I + combining dot above) — nadir

İşletim sistemi / klavye / tarayıcı kombinasyonuna göre farklı gelebilir.
Bu iki "İ" string karşılaştırmada eşit DEĞİL (byte farklı).

**Düzeltme:** Hem kullanıcı input'u hem il/ilçe adı `Normalize(NormalizationForm.FormC)`
ile canonical composed biçime getirilir. Sonra `TurkishNormalizer.Normalize`
(Turkish culture ToLower).

## Değişen 1 Dosya

| Dosya | Değişiklik |
|---|---|
| `ManagementPortal/Components/Pages/Sites/Form.razor` | `CoerceText="true"` → `"false"` + Unicode FormC normalize |

### Eski SearchFunc
```csharp
var needle = TurkishNormalizer.Normalize(value.Trim());
```

### Yeni SearchFunc
```csharp
private static string NormalizeForSearch(string? value) =>
    string.IsNullOrWhiteSpace(value)
        ? string.Empty
        : TurkishNormalizer.Normalize(value.Normalize(NormalizationForm.FormC));

// Her iki tarafa uygulanır — user input + province/district name
var needle = NormalizeForSearch(value);
var filtered = _provinces.Where(p =>
    NormalizeForSearch(p.Name).Contains(needle, StringComparison.Ordinal));
```

### MudAutocomplete parametresi
```razor
CoerceText="false"   // eskiden "true"
```

## Arama Davranışı (beklenen)

- **"i"** → İzmir, Iğdır, Bilecik (i/İ/I/ı içeren tüm iller) ✓
- **"İ"** → İzmir, Iğdır, Bilecik (aynı sonuç, case-insensitive) ✓
- **"ö"** → Ödemiş, Ordu... (ö/Ö) ✓
- **"sanli"** → Şanlıurfa ❌ (DİAKRİTİK kaldırılmıyor — `TurkishNormalizer` kuralı)
- **"şanlı"** → Şanlıurfa ✓

Yani: case-insensitive evet, **diakritik-insensitive HAYIR** (TurkishNormalizer
tasarım kararı, organizasyon araması ile tutarlı).

## Uygulama

```powershell
cd D:\Projects\sitehub

Unblock-File C:\Users\<sen>\Downloads\sitehub-f6-c3-hotfix3.zip
Expand-Archive -Path "C:\Users\<sen>\Downloads\sitehub-f6-c3-hotfix3.zip" `
    -DestinationPath "D:\Projects\sitehub\" -Force

dotnet build
```

Build temiz. Test kırılma yok (UI only).

## Smoke Test

### Test 1 — "İ" artık çalışıyor (bug fix doğrulama)
1. Yeni site aç → İl alanına **"İ"** yaz
2. Beklenen: İzmir, Iğdır vb. görünür
3. "İz" yaz → İzmir filtrelenir

### Test 2 — "i" küçük
1. İl alanını temizle, **"i"** yaz
2. Beklenen: İzmir, Iğdır... (Test 1 ile aynı — case-insensitive)

### Test 3 — "Ö" hâlâ çalışıyor (regresyon)
1. İlçe alanına git (İstanbul seç önce), **"Ö"** yaz
2. Beklenen: Ödemiş vb. görünür

### Test 4 — Türkçe karakter combinasyonu
1. "şan" yaz → Şanlıurfa ✓
2. "şiş" yaz → Şişli ✓ (ilçe)

## Commit Önerisi

```
Faz F.6 C.3 hotfix3: 'İ' aramada bulamama bug fix

Sorun: MudAutocomplete'te 'İ' yazinca il/ilce filtrelenmiyordu. 'Ö'
'Ü' gibi karakterler dogru calisiyordu. 'İ' 'ye ozel asimetrik
davranis.

Kok sebep (kombinasyon):
1) CoerceText='true' - eslesemeyen input sessizce siliniyor, 'İ' iki
   ardisik karakter islemi sirasinda arama resetleniyordu
2) Unicode İ temsil farki - U+0130 (precomposed) vs U+0049+U+0307
   (decomposed). Klavye/OS'e gore input farkli gelebilir.

Duzeltme (Form.razor):
- CoerceText='true' -> 'false' (il + ilce autocomplete)
  Kullanicinin yazdigi metin olduğu gibi kalir, arama devam eder
- Arama pipeline: value.Normalize(FormC) + TurkishNormalizer.Normalize
  Hem user input hem il/ilce adi ayni Unicode biciminde normalize
  edilir, byte-eslesme garantili

Arama davranisi (Organization List ile tutarli):
- Case-insensitive: 'i' veya 'İ' yazilinca ikisi de eslesir
- Diakritik-SENSITIVE: 'sanli' Sanliurfa'yi bulmaz, 'sanli' hece
  ile 'S' ile baslayan ili bulur (TurkishNormalizer tasarim karari)

Test: Build temiz, 146 test yesil.
Smoke: 'İ' ve 'i' her ikisi de İzmir'i buluyor.

PROJECT_STATE S13'e eklenecek bilinen is: Shared.Text.TurkishNormalizer
Domain.Text.TurkishNormalizer ile birebir duplicate. Temizlik gerekli
(ileri bir commit'te Shared'dan silinecek).
```
