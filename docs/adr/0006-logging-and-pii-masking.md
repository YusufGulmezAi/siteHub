# ADR-0006: Kapsamlı Loglama ve PII Maskeleme Stratejisi

**Durum:** Kabul Edildi
**Tarih:** 2026-04-19

## Bağlam

SiteHub'da denetlenebilirlik (auditability), güvenlik incelemesi, KVKK uyumu ve
ileride olay müdahalesi için **tüm sistem faaliyetleri** loglanmalıdır. Ancak
hassas verilerin (şifre, TCKN, kart numarası) loglanması başlı başına bir güvenlik
ihlalidir. İki gereksinim birbirinin tersi gibi görünse de doğru katmanlama ile
birlikte çözülür.

## Karar

### 1. Loglama Teknoloji Yığını

- **Serilog**: Structured logging framework
- **Seq** (Dev/Test), **Grafana Loki** (Prod): Log storage + arama
- **OpenTelemetry**: Distributed tracing (correlation ID)
- **Correlation ID**: Her HTTP request için unique, tüm log satırlarına enrich edilir

### 2. Loglanacak Olaylar

Her log satırı şu alanları içerir (zorunlu):

| Alan             | Kaynak                              | Örnek                                |
|------------------|-------------------------------------|--------------------------------------|
| Timestamp        | `DateTimeOffset.UtcNow`             | `2026-04-19T10:23:45.123Z`          |
| Level            | Serilog seviyesi                    | `Information`, `Warning`, `Error`    |
| CorrelationId    | Request ID (OTel trace)             | `0HMVH...`                           |
| Application      | Enrich.WithProperty                 | `SiteHub.ManagementPortal`           |
| Environment      | Enrich.WithProperty                 | `Development`, `Production`          |
| UserId           | Authenticated user claim            | `usr_abc...` veya `anonymous`        |
| UserNationalId   | Authenticated user (**maskelenir**) | `123***8901`                         |
| ActiveContext    | Aktif bağlam (varsa)                | `Firm:550e...` / `Site:a3f1...`      |
| ClientIP         | X-Forwarded-For veya Connection    | `88.12.34.56`                        |
| UserAgent        | Request header                      | `Mozilla/5.0 ...`                    |

### 3. HTTP Request/Response Loglama

Her HTTP request için (`UseSerilogRequestLogging` + custom middleware):

```
[Info] HTTP POST /api/invoices →
  User: usr_abc... (TCKN: 123***8901)
  ActiveContext: Firm:550e...
  IP: 88.12.34.56
  UserAgent: Mozilla/5.0 ...
  RequestBody: { Amount: 1500, Description: "Aidat Ocak 2026" }  // masked
  → Response: 201 Created, Duration: 47ms
  → ResponseBody: { InvoiceId: "inv_..." }
```

Maskelenen alanlar REQUEST BODY'de (destructuring policy ile):
- Password, CurrentPassword, NewPassword, PasswordConfirmation
- NationalId, Tckn, Vkn, Ykn (ilk 3 + son 4 dışı `*`)
- CardNumber, Pan (ilk 6 + son 4 dışı `*`)
- Cvv, Cvc, SecurityCode → `***`
- OtpCode, TotpCode, SmsCode → `***`
- Token, AccessToken, RefreshToken, ApiKey, SecurityStamp → `***`
- Iban (ilk 4 + son 4 dışı `*`)

Response body loglaması: yalnızca hata durumunda (4xx/5xx) veya belirli
endpoint'lerde (audit-critical). Her response body'yi loglamak performans
problemi yaratır.

### 4. CQRS Pipeline Loglama

MediatR `LoggingBehavior` her command/query için:

```
[Info] [MediatR] CreateInvoiceCommand başlatılıyor
  Request: { Amount: ..., UnitId: ... }  // masked
  UserId: usr_abc...

[Info] [MediatR] CreateInvoiceCommand tamamlandı (42ms)
  Result: Success
```

Hata durumunda stack trace + input (masked) otomatik loglanır.

### 5. Güvenlik Olayları (Audit Log)

Ayrıca bir **ayrı denetim tablosu** (`audit.events`) tutulur — sadece text log değil:

- Login başarılı/başarısız
- Context değişimi
- Permission değişimi
- Hassas veri görüntüleme (örn: kişisel bilgi listeleme)
- CRUD işlemleri (hangi entity, eski/yeni değer — hassas alanlar maskelenmiş)
- 2FA etkinleştirme/devre dışı bırakma
- Şifre değişimi
- Yetki reddedildi (ForbiddenException)
- Tenant provisioning

Bu tablo immutable'dır (append-only). Periodik olarak WORM (write-once-read-many)
depolamaya arşivlenir.

### 6. PII Maskeleme İmplementasyonu

Serilog `Destructure.ByTransforming<T>` ile transformasyon kayıtlı nesne tipinde
otomatik çalışır. Örnek:

```csharp
// NationalId value object'i loglandığında otomatik maskelenir
Log.Logger = new LoggerConfiguration()
    .Destructure.ByTransforming<NationalId>(id => new
    {
        Type = id.Type,
        Value = MaskMiddle(id.Value, keepStart: 3, keepEnd: 4)  // "123****8901"
    })
    // ... diğer maskelemeler
    .CreateLogger();
```

**Anonim nesneler için**: Custom `IDestructuringPolicy` implementasyonu, alan
adına göre (regex veya attribute) otomatik maskeleme yapar.

**Kural:** Hassas alanları logdan çıkarmak geliştiricinin sorumluluğu değil —
framework seviyesinde zorlanır. Geliştirici "maskelemeyi unutabilir", framework
unutmaz.

### 7. Performans & Maliyet

- Log seviyesi Prod'da `Information`, Dev'de `Debug`
- Verbose seviyesindeki loglar sadece özel `LogSource` etiketiyle aktif edilir
- Log sampling: aynı tip error 1 dakikada 100'den fazla gelirse örnekleme aktif
- Serilog `Async` sink ile HTTP isteklerini bloke etmez
- 30 gün hot storage, 1 yıl cold storage (gzip), sonra arşiv

### 8. Günlük İnceleme Araçları

- **Seq** (Dev): structured query, basit kullanım
- **Grafana + Loki** (Prod): aramalar, dashboards, alerting
- **Audit tablosu**: DB içinde sorgulanabilir, admin UI'ında görünebilir

## Sonuçları

**Olumlu:**
- Her olay izlenebilir (kim/ne/ne zaman/nereden)
- PII maskeleme framework seviyesinde zorunlu — unutulması imkansız
- KVKK uyumu için güçlü denetim kaydı
- İncident response'ta dakikalar içinde "bu hesaba ne oldu" sorusu cevaplanır

**Olumsuz / Dikkat:**
- Storage maliyeti (log retention politikası şart)
- Yeni bir hassas alan tipi eklendiğinde maskeleme kuralı da eklenmeli
- Her request için I/O overhead (async sink ile minimize edilir)
- Audit tablosuna yazma transaction'a bağlı olmalı (iş başarısız olursa log
  da yazılmaz — tutarsızlık olmaması için)

## Referanslar

- Serilog Destructuring: https://github.com/serilog/serilog/wiki/Structured-Data
- OWASP Logging Cheat Sheet
- KVKK Veri Güvenliği Tedbirleri rehberi
