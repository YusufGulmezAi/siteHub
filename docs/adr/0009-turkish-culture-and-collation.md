# ADR-0009: Türkçe Dil Desteği ve Collation Stratejisi

**Durum:** Kabul Edildi
**Tarih:** 2026-04-19

## Bağlam

SiteHub Türkiye'deki yönetim firmaları için bir SaaS. Kullanıcı girdilerinin
büyük çoğunluğu Türkçe — firma adları, kişi isimleri, adresler, notlar,
arama sorguları. Türkçe'nin İngilizce'den **farklı kuralları** vardır ve bu
kuralları doğru uygulamamak sessiz bug'lara yol açar:

1. **I/ı/İ/i dört harf sorunu:**
   - `"Istanbul".ToUpper()` → İngilizce'de `"ISTANBUL"`, Türkçe'de `"İSTANBUL"`
   - `"INDEX".ToLower()` → İngilizce'de `"index"`, Türkçe'de `"ındex"`
   - Bu sessiz farklar production'da random hata doğurur

2. **Sıralama (collation):**
   - Türkçe alfabe: ... C, Ç, D, ... O, Ö, P, ... S, Ş, T, ... U, Ü, V, ...
   - C.UTF-8 / ASCII sıralama: Ç, Ş, Ü ya sonda ya başta — karışık
   - Kullanıcı "Çankaya Sitesi" ile "Çağlayan Sitesi" arasında doğru sıra bekler

3. **Arama davranışı:**
   - Kullanıcı mobilden "atasehir" yazıp "Ataşehir" bulmak ister (diacritic-insensitive)
   - "ISTANBUL" yazıp "İstanbul" bulmak ister (case-insensitive + TR-aware)
   - "ALİ" arar, "ali" de "Ali" de bulsun

4. **Benzersizlik ve doğrulama:**
   - VKN karşılaştırma byte-exact olmalı (ord yol)
   - Ama firma adında "ABC Yönetim" ile "abc yönetim" aynı sayılmalı mı? Bu kararlar
     collation'a göre verilir.

## Değerlendirilen Seçenekler

### Seçenek 1: Her şeyi kod tarafında yönet (CultureInfo)
- Tüm karşılaştırmalar `StringComparer.Create(trTR, ignoreCase)` ile yapılır
- DB'de collation varsayılan (`C.UTF-8`)
- ❌ Dezavantaj: DB seviyesinde `SELECT ... ORDER BY name` yanlış sıralar
- ❌ Dezavantaj: `UNIQUE INDEX` uniqueness'ı ordinal byte ile kontrol eder — "ALİ" ve "ALi"
  farklı sayılır, beklenmedik duplicate giriş olur

### Seçenek 2: Her şeyi DB tarafında yönet (Postgres collation)
- Tüm metin kolonları TR collation kullanır
- Uygulamada özel bir şey yapmaya gerek yok
- ❌ Dezavantaj: Test'te, script'te, Excel export'ta DB dışına çıkan veri sorting'i
  yine C# tarafında yapmak gerekebilir — tutarsızlık

### Seçenek 3: Dört katmanda birden yönet (seçildi) ✓
- **Postgres collation** — query-level sorting/search doğru olsun
- **EF Core convention** — string kolonları otomatik TR collation'a sahip olsun
- **C# CultureInfo** — domain ve application kodunda string karşılaştırmalar
- **Blazor thread culture** — UI formatting, MudBlazor bileşenleri

## Karar

### 4 Katmanlı Strateji

#### Katman 1: PostgreSQL Collation (Storage + Query)

İki collation tanımlıyoruz (migration'da otomatik oluşur):

```sql
-- Arama ve kullanıcıya gösterim için (varsayılan)
CREATE COLLATION tr_ci_ai (
    provider = icu,
    locale = 'tr-TR-u-ks-level1',  -- case + accent insensitive
    deterministic = false
);

-- Benzersizlik ve exact-match için (unique index'lerde)
CREATE COLLATION tr_cs_as (
    provider = icu,
    locale = 'tr-TR-x-icu',
    deterministic = true
);
```

**Kural:** Varsayılan kolon collation'ı `tr_ci_ai`. Unique constraint / index
gerekenler için `tr_cs_as` override.

#### Katman 2: EF Core Convention

`SiteHubDbContext.ConfigureConventions`:
```csharp
configurationBuilder
    .Properties<string>()
    .UseCollation(TurkishCiAi);
```

Bu satır sayesinde `Firm.Name` gibi bir kolon için manuel `.UseCollation()`
yazmaya gerek kalmaz. **Unique index istendiğinde** (örn. `Firm.TaxId`):
```csharp
b.Property(f => f.TaxId).UseCollation(SiteHubDbContext.TurkishCsAs);
```

#### Katman 3: C# CultureInfo

Domain ve application kodu string karşılaştırmalar için **açıkça** Turkish culture
kullanmalı. Implicit culture fetching YASAK:

```csharp
// YANLIŞ — culture bağımlı
a.ToUpper().Contains(b.ToUpper());
a == b;

// DOĞRU — niyet belli
a.Equals(b, StringComparison.OrdinalIgnoreCase);         // internal identifier
a.ToLower(CultureInfo.GetCultureInfo("tr-TR"));          // user-facing
CompareInfo.IndexOf(haystack, needle, CompareOptions.IgnoreCase);
```

**Kılavuz:**

| Durum | Yöntem |
|-------|--------|
| Internal ID, enum adı, dosya yolu, JSON key | `StringComparison.Ordinal` |
| Güvenlik kritik (şifre hash, token) | `StringComparison.Ordinal` |
| Kullanıcı metni — case-insensitive karşılaştırma | Turkish culture + `OrdinalIgnoreCase` DEĞİL, `CompareInfo` |
| Kullanıcıya sıralı listeleme (C# tarafında) | `StringComparer.Create(trTR, ignoreCase: true)` |
| Arama (diacritic-insensitive) | Custom `Normalize(s)` (ToLower TR + ş→s, ğ→g vb.) |

#### Katman 4: Blazor Thread Culture

`Program.cs` başında:
```csharp
var turkishCulture = CultureInfo.GetCultureInfo("tr-TR");
CultureInfo.DefaultThreadCurrentCulture = turkishCulture;
CultureInfo.DefaultThreadCurrentUICulture = turkishCulture;
```

Bu satır sayesinde:
- `DateTime.Now.ToString()` → `"19.04.2026 14:00:00"` (TR format)
- `decimal.ToString()` → `"1.234,56"` (TR para formatı)
- MudBlazor DatePicker'da ay isimleri Türkçe
- `Request-ID` gibi tech field'lar yine culture-agnostic çünkü kodda `CultureInfo.InvariantCulture` kullanılır

### Diacritic-Insensitive Arama İçin Özel Fonksiyon

"atasehir" yazıp "Ataşehir" bulmak için:

```csharp
private static readonly CultureInfo TR = CultureInfo.GetCultureInfo("tr-TR");

private static string Normalize(string s)
{
    var lowered = s.ToLower(TR);  // İ→i, I→ı — TR kuralları
    return lowered
        .Replace('ı', 'i')
        .Replace('ş', 's')
        .Replace('ğ', 'g')
        .Replace('ü', 'u')
        .Replace('ö', 'o')
        .Replace('ç', 'c');
}
```

Hem client-side (Blazor arama component'ları) hem server-side (database dışı
fuzzy search) kullanılır.

## Sonuçlar

### Olumlu
- Türkçe kullanıcı en doğal deneyimi alır — her yerde aynı davranış
- DB seviyesinde doğru sıralama: `ORDER BY name` gerçek alfabetik sıra
- `UNIQUE` constraint doğru çalışır: "ABC" ve "abc" duplicate sayılır (case-insensitive collation)
- Test ortamında da kültür garantili — flaky test riski yok

### Olumsuz / Dikkat
- Non-deterministic collation **hash join**'da daha yavaş olabilir (nadir, çoğu durumda önemsiz)
- Non-deterministic collation üzerine doğrudan **unique index** atılamaz (override gerekir)
- Bir kolon önce tr_ci_ai ile oluşup sonra tr_cs_as'a çevrilirse duplicate veri çıkabilir —
  migration'da dikkat (test etmeden prod'a atma)
- **Opsiyonel uluslararasılaştırma** (EN/AR): Bu ADR tek-dil'e (tr-TR) kilitler.
  Çoklu dil gerekirse thread culture request per-request değiştirilmeli, collation
  stratejisi yeniden tasarlanmalı (muhtemelen `und-x-icu` default).

### İleride
- Tam-metin arama: `pg_trgm` extension + GIN index + TR unaccent
- TR-spesifik sözlük (PostgreSQL `text search` — `turkish_stem`)
- Arapça, Kürtçe gibi ek diller istenirse: i18n framework'e geçiş

## Referanslar

- PostgreSQL ICU Collations: https://www.postgresql.org/docs/current/collation.html
- Npgsql EF Core Collations: https://www.npgsql.org/efcore/modeling/collations.html
- .NET Culture and String Handling: https://learn.microsoft.com/dotnet/standard/base-types/best-practices-strings
- CA1310, CA1305, CA1304 — ilgili code analyzer kuralları
