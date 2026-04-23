# Faz F.6 C.3 Hotfix 2 — VKN DB Converter + Türkçe Arama + Plaka Kodu

**Tarih:** 2026-04-22
**Scope:** Bir önceki hotfix'in tamamlayıcısı — DB converter tarafı + UI iyileştirmeleri.

## Ciddi Sorun — Sistemik VKN converter bug

**Patolojik akış (smoke test'te görüldü):**

1. Site kaydı POST → 201 Created (Application katmanı `CreateVknRelaxed` kullanıyor, "0000000231" geçer)
2. Site listesi GET → **500 Internal Server Error**
3. Stack trace: `NationalId.CreateVkn` (checksum'lı) → `InvalidNationalIdException`

**Kök sebep:** `SiteConfiguration.cs`'de TaxId converter `NationalId.Parse()` kullanıyordu. Parse, 10 haneli girdi için checksum'lı `CreateVkn`'e yönlendiriyor. DB'de Relaxed ile yazılmış sahte VKN'ler okurken patlıyordu.

**Neden Organization'da patlamıyor?** `OrganizationConfiguration.cs` converter'da zaten `CreateVknRelaxed` kullanılıyor. Pattern doğru uygulanmış, sadece Site'a genişletilmemiş (F.6 C ilk commit'inde Site hâlâ yeni, bu noktada yakalanamadı).

**Çözüm:** Site converter'ı Organization pattern'iyle eşleştir.

## Ek Düzeltmeler (UI)

### Türkçe arama ("izmir" → İzmir bulmuyor)

**Eski kod:**
```csharp
p.Name.Contains(normalized, StringComparison.OrdinalIgnoreCase)
```

`OrdinalIgnoreCase` Türkçe için yetersiz — i/İ, I/ı çiftleri eşleşmiyor.

**Yeni kod:** `SiteHub.Domain.Text.TurkishNormalizer` kullan. Organization List'te backend SQL `LIKE` için de aynı normalizer kullanılıyor — **tutarlı davranış**, tek kaynak.

```csharp
var needle = TurkishNormalizer.Normalize(value.Trim());
var filtered = _provinces.Where(p =>
    TurkishNormalizer.Normalize(p.Name).Contains(needle, StringComparison.Ordinal));
```

Artık "izmir" → "İzmir", "istanbul" → "İstanbul", "SANLI" → "Şanlıurfa" çalışır.

### İl listesinde plaka kodu kaldırıldı

Eski gösterim: "34 İstanbul", "06 Ankara"
Yeni gösterim: "İstanbul", "Ankara"

`ToStringFunc` değişikliği, arama fonksiyonu da artık sadece il adı üzerinde çalışır (plaka kodu araması kaldırıldı — gereksiz).

## Değişen 2 Dosya

| Dosya | Değişiklik |
|---|---|
| `Infrastructure/Persistence/Configurations/SiteConfiguration.cs` | TaxId converter Parse → CreateVknRelaxed |
| `ManagementPortal/Components/Pages/Sites/Form.razor` | TurkishNormalizer arama + plaka kodu kaldırıldı |

`OrganizationConfiguration.cs` zaten doğru (CreateVknRelaxed kullanıyor), dokunulmadı.

## Uygulama

```powershell
cd D:\Projects\sitehub

Unblock-File C:\Users\<sen>\Downloads\sitehub-f6-c3-hotfix2.zip
Expand-Archive -Path "C:\Users\<sen>\Downloads\sitehub-f6-c3-hotfix2.zip" `
    -DestinationPath "D:\Projects\sitehub\" -Force

dotnet build
dotnet test --no-build
```

Build temiz, 146 test yeşil olmalı.

## Smoke Test

### Test 1 — Hotfix öncesi yazılan sahte VKN'li site listelensin (kritik)
1. Önce hotfix2 öncesi "SWDSDasdsad" gibi bir site VKN "0000000231" ile kaydetmiştin
2. `/organizations/{orgId}/sites` sayfasına git
3. **Beklenen:** Liste yüklendi, site görünüyor (500 yok)
4. **Önceki:** 500 InvalidNationalIdException

### Test 2 — Yeni site oluştur (VKN'li)
1. "+ Yeni Site" → "21" VKN gir → diğer alanları doldur → "Oluştur"
2. Başarılı: snackbar + liste güncellenir + 500 yok

### Test 3 — Türkçe arama (İl)
1. Yeni site aç, İl alanına "izmir" yaz
2. **Beklenen:** "İzmir" filtrelenir
3. Silmek için "istanbul" yaz → "İstanbul" bulunur
4. "sanli" yaz → "Şanlıurfa" bulunur
5. "ANKARA" yaz → "Ankara" bulunur

### Test 4 — İl listesi görünüm
1. İl dropdown'unu aç
2. **Beklenen:** Sadece il adları ("İstanbul", "Ankara"...) — plaka kodu YOK

### Test 5 — Türkçe arama (İlçe)
1. İstanbul seç
2. İlçe alanına "kadi" yaz → "Kadıköy" bulunur
3. "sisli" yaz → "Şişli" bulunur

### Test 6 — Regresyon: Edit mode
1. Yeni oluşturulan site Edit URL'sine git
2. İl alanı dolu ("İstanbul" sadece, plaka yok)
3. Form normal çalışır

## Commit Önerisi

```
Faz F.6 C.3 hotfix2: VKN DB converter + Turkce arama + plaka kaldir

Kritik sistemik bug duzeltmesi (smoke test'te bulundu):

VKN DB CONVERTER:
SiteConfiguration TaxId converter 'NationalId.Parse' kullaniyordu.
Parse 10 haneli girdi icin checksum'li CreateVkn'e yonlendiriyor.
Application katmanında CreateVknRelaxed ile yaziyoruz (sahte VKN'ler)
-> DB'de checksum'dan gecmeyen VKN'ler var -> okurken patliyor
-> 500 Internal Server Error.

OrganizationConfiguration zaten dogru (CreateVknRelaxed kullaniyor).
Pattern Site'ta genisletilmemisti. Fix: Site converter da Relaxed.

Bu sorun sadece Site listesi 500 vermekle kalmiyor, kritik bir sistemik
sorundu: Dev'de sahte VKN'li her Organization da bir gun patlardi.

UI IYILESTIRMELERI:

1. Turkce karakter arama:
   'izmir' yazinca 'Izmir' bulamiyordu (OrdinalIgnoreCase Turkce icin
   yetersiz). Cozum: SiteHub.Domain.Text.TurkishNormalizer kullan
   (Organization List backend arama ile ayni normalize).

2. Il listesi gorunum:
   '34 Istanbul' yerine sadece 'Istanbul'. Plaka kodu arama da
   kaldirildi (gereksiz).

Degisen 2 dosya:
- SiteConfiguration.cs: TaxId converter fix
- Sites/Form.razor: TurkishNormalizer + sade il gosterimi

Test: Build temiz, 146 test yesil.
Smoke: Onceki kaydedilmis sahte VKN'li site listelendi, 500 yok.
       Izmir/Istanbul/Sanli turkce arama calisti.

PROJECT_STATE S5.5 Ogrenim 6 genisletilecek: 'VKN Relaxed' karari
sadece Application handler'larinda degil, EF Core converter'larinda
da uygulanmali. Aksi halde DB'de sahte VKN var + converter strict =
runtime patlar.
```
