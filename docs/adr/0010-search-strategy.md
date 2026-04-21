# ADR-0010: Arama Stratejisi (Partial Match + Normalize)

**Durum:** Kabul Edildi
**Tarih:** 2026-04-19

## Bağlam

ADR-0009 Türkçe collation stratejisini tanımladı: `tr_ci_ai` (non-deterministic,
case+accent insensitive) varsayılan. Bu collation **equality** ve **ORDER BY**
için mükemmel — ama PostgreSQL'in kısıtı nedeniyle `LIKE`/`ILIKE` **çalışmaz**:

> `42804: nondeterministic collations are not supported for ILIKE`

Müşteri UX ihtiyacı: kullanıcı mobilde "şiş" yazınca anlık dropdown'da
**"Şişli Yönetim A.Ş."** görmeli. Partial match zorunlu.

## Değerlendirilen Seçenekler

### 1. `COLLATE "default"` ile query seviyesinde override
```sql
WHERE name COLLATE "default" ILIKE '%şiş%'
```
- ❌ Türkçe karakter hassasiyeti kaybolur ("Şişli" yazılınca "ŞİŞLİ" bulmaz)
- ❌ Her sorguda unutulabilir

### 2. Uygulama tarafında normalize + ayrı `search_text` kolonu
- ✅ Tek yerde kural (TurkishNormalizer), tutarlı
- ✅ Deterministic collation → ILIKE sorunsuz + index çalışır
- ✅ Hem C# hem DB seviyesinde aynı normalize → ILIKE partial çalışır
- ❌ Ek kolon, ek disk alanı

### 3. `pg_trgm` extension + GIN index
- ✅ En güçlü (tipo toleransı, similarity)
- ❌ Overkill for MVP, extension bağımlılığı

## Karar

**Seçenek 2** — `SearchableAggregateRoot` pattern'i.

### Uygulama

**`TurkishNormalizer.Normalize(string)`** kuralı:
1. Türkçe kültürle `ToLower` — `İ→i`, `I→ı` doğru uygulanır
2. Baş/son boşluk kırpılır, iç çoklu boşluk teke indirilir
3. **Diacritic KORUNUR** — "şiş" → "şiş" (s olmaz)

**Örnekler:**
```
"Şişli YÖNETİM A.Ş."   → "şişli yönetim a.ş."
"ISTANBUL"             → "ıstanbul"
"İSTANBUL"             → "istanbul"
"  ATAŞEHİR  kiracı "  → "ataşehir kiracı"
```

**`SearchableAggregateRoot<TId>`** — `AuditableAggregateRoot`'tan türer,
`SearchText` alanı ekler:

```csharp
public abstract class SearchableAggregateRoot<TId> : AuditableAggregateRoot<TId>
    where TId : struct
{
    public string SearchText { get; private set; } = string.Empty;

    protected void UpdateSearchText(params string?[] fields)
    {
        SearchText = TurkishNormalizer.Combine(fields);
    }
}
```

**DB tarafında `search_text` kolonu:**
```csharp
builder.Property(o => o.SearchText)
    .HasColumnName("search_text")
    .HasMaxLength(2000)
    .UseCollation(SiteHubDbContext.TurkishCsAs)  // DETERMINISTIC → ILIKE OK
    .IsRequired();

builder.HasIndex(o => o.SearchText);
```

**Kullanım:**
```csharp
// Kullanıcı "şiş" yazdı:
var q = TurkishNormalizer.Normalize(userInput);  // "şiş"

var results = db.Organizations
    .Where(o => EF.Functions.ILike(o.SearchText, $"%{q}%"))
    .OrderBy(o => o.Name)  // Name kolonu hâlâ tr_ci_ai — doğru sıralar
    .Take(20)
    .ToList();
```

### Entity'de Ne Zaman Güncellenir?

State-değiştiren **her** davranışta `UpdateSearchText(...)` çağrılır:

```csharp
public static Organization Create(string name, string title, ...)
{
    var org = new Organization(...);
    org.RecomputeSearchText();  // ← unutma
    return org;
}

public void Rename(string newName, ...)
{
    Name = newName;
    RecomputeSearchText();      // ← unutma
}

public void UpdateContact(...)
{
    Email = ...;
    RecomputeSearchText();      // ← unutma
}

private void RecomputeSearchText()
    => UpdateSearchText(Name, CommercialTitle, TaxId?.Value, Phone, Email);
```

**Neden otomatik değil?** Çünkü:
- Hangi alanların aranabilir olduğu **iş kararı** (Address'i aramayı istiyor muyuz?)
- Aggregate'in her property set'inde değil, **davranış bittiğinde** rekompüte edilmeli

## Sonuçları

### Olumlu
- Kullanıcı "şiş", "Şiş", "ŞİŞ" yazar — "Şişli Yönetim" bulunur ✓
- ILIKE sorunsuz, index çalışır, binlerce kayıtta hızlı arama
- `SearchText` kolonunda VKN, telefon, e-posta dahil — hepsi aranabilir
- MudAutocomplete, MudDataGrid.QuickFilter gibi bileşenler doğal çalışır

### Olumsuz / Dikkat
- Her state değişikliğinde `RecomputeSearchText` unutulmamalı (aggregate sorumluluğu)
- `search_text` kolonu ~3x Name boyutu (5 alan birleşimi) — disk maliyeti minimum ama var
- Diacritic-free arama DESTEKLENMİYOR ("sisli" yazan "Şişli" bulmaz) — iş gereği bu
- Yeni aranabilir alan eklenirse `RecomputeSearchText` güncellenmeli → test bunu yakalar

### İleride (v2+)
- `pg_trgm` ile similarity arama (tipo toleransı)
- Diacritic-free mod (kullanıcı tercihi: "tam eşleşme" vs "gevşek arama")
- Tam-metin arama (PostgreSQL `tsvector` + TR dictionary)

## Referanslar

- ADR-0009: Türkçe Collation
- PostgreSQL docs: https://www.postgresql.org/docs/current/collation.html
  (nondeterministic collation limitations)
