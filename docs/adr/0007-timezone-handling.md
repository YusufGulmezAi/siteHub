# ADR-0007: Tarih/Saat ve Zaman Dilimi Yönetimi

**Durum:** Kabul Edildi
**Tarih:** 2026-04-19

## Bağlam

SiteHub Türkiye merkezli bir üründür — kullanıcıların büyük çoğunluğu
Türkiye'de (`Europe/Istanbul`, UTC+3). Ancak sistem gelecekte farklı
zaman dilimlerinde kullanılabilir (yurtdışı Türk toplulukları, yaz/kış saati
geçişleri vb.). Ayrıca bir sunucu farklı bölgede çalıştırılırsa da
tutarlılık gerekir.

**Yaygın hata:** Veritabanında yerel saatle kayıt tutmak. Yaz saati
geçişlerinde kayıtlar "kaybolur" veya çakışır; sunucu başka bölgeye
taşındığında tüm geçmiş veriler 1-3 saat kayar.

## Karar

### Altın Kural

> **Veri UTC saklanır, UI kullanıcının zaman diliminde (varsayılan Europe/Istanbul) gösterir.**

### Backend / Veritabanı

1. **Tüm `DateTime` kolonları**: PostgreSQL `timestamp with time zone` (timestamptz).
   UTC'ye çevrilmiş halde yazılır, UTC olarak saklanır.

2. **C# tarafında**: `DateTimeOffset` kullanılır (asla çıplak `DateTime`).
   `DateTimeOffset` zaman dilimini kendisiyle taşır.

3. **Şu anın zamanı**: Hiçbir zaman `DateTime.Now` veya `DateTimeOffset.Now` YOK.
   Onun yerine **.NET 8+ TimeProvider** abstraction'ı kullanılır:

   ```csharp
   public class CreateInvoiceHandler(TimeProvider timeProvider) { ... }
   // timeProvider.GetUtcNow() → DateTimeOffset (UTC)
   ```

   Avantaj: test edilebilir (FakeTimeProvider) + ortam-bağımsız.

4. **EF Core**: Npgsql provider UTC conversion'ı otomatik yapar (bkz:
   `EnableLegacyTimestampBehavior = false`).

5. **Loglar**: UTC timestamp ile yazılır. Seq/Grafana UI'ında Türkiye saatiyle
   gösterilir (viewer ayarı).

### UI / Display

6. **`ITurkeyClock` servisi** Shared katmanında:

   ```csharp
   public interface ITurkeyClock
   {
       TimeZoneInfo Zone { get; }              // Europe/Istanbul
       DateTimeOffset Now { get; }             // UTC+3 (veya yaz saati)
       DateTimeOffset ToLocal(DateTimeOffset utc);
       DateTime ToLocalDateTime(DateTimeOffset utc);
       string Format(DateTimeOffset utc, string? format = null);
   }
   ```

   Varsayılan implementasyon: `TurkeyClock` → `Europe/Istanbul` IANA zaman dilimi.

7. **Blazor / MudBlazor**:
   - Tüm `MudDatePicker`, `MudTimePicker`, `MudDateRangePicker` bileşenleri
     `ITurkeyClock` ile bağlanır
   - Custom `<LocalTime />` component'i: UTC DateTimeOffset alır, Türkiye saatiyle
     gösterir (`19.04.2026 14:23`)
   - Varsayılan format: `dd.MM.yyyy HH:mm` (Türk okuma alışkanlığı)

8. **API / JSON**: `DateTimeOffset` ISO 8601 formatında serialize edilir:
   `"2026-04-19T14:23:45.000+03:00"`. Frontend zaman dilimini bizzat görür.

### Çapraz Kesen Sorunlar

9. **Gün/hafta/ay sınırı olayları** (örn: "bu ayın aidatları"):
   - Kullanıcının zaman diliminde hesaplanır
   - Query tarafında `StartOfMonthInTurkey().ToUtcRange()` pattern'i kullanılır
   - Örnek: "Nisan 2026 aidatları" sorgusu:
     - Türkiye'de: 2026-04-01 00:00 → 2026-05-01 00:00
     - UTC'de:     2026-03-31 21:00 → 2026-04-30 21:00
     - Query UTC aralıkla çalışır ama sınırlar Türkiye saatine göre belirlenir

10. **Background jobs / cron**:
    - Cron ifadeleri Türkiye saatine göre tanımlanır
    - Hangfire / Quartz schedule Türkiye TZ'sinde kurulur

11. **User preference (ileride)**:
    - `User` entity'sine `PreferredTimeZone` kolonu eklenebilir (varsayılan
      `Europe/Istanbul`)
    - `ITurkeyClock` yerine `IUserClock` (kullanıcı-başı) scope servisine geçilir

## Sonuçları

**Olumlu:**
- Yaz saati uygulaması olsa bile veri tutarlı kalır
- Sunucu farklı bölgeye taşındığında kayıtlar etkilenmez
- Frontend doğrudan `DateTimeOffset` alarak kendi mantığını kurabilir
- Test edilebilirlik: `FakeTimeProvider` ile zamanı kontrol edebiliriz

**Olumsuz / Dikkat:**
- Kod seviyesinde disiplin gerekir — hiçbir zaman `DateTime.Now` yazılmamalı
- `DateTime.Now` kullanımını engellemek için Roslyn analyzer veya architecture
  test eklenmeli (`NetArchTest`)
- Ay/hafta başı hesaplamaları "Türkiye'de şu an" ile yapılırsa UTC sınırları
  farklılaşır — dikkatli yazılmalı

## Referanslar

- Microsoft Docs: TimeProvider (.NET 8+)
- Npgsql: Date and Time Handling
- Jon Skeet: "Storing UTC is not a silver bullet"
