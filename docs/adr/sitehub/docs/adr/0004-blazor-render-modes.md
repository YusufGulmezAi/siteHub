# ADR-0004: Blazor Render Modu Stratejisi

**Durum:** Kabul Edildi
**Tarih:** 2026-04-19

## Bağlam

.NET 10'da Blazor Web App modeli dört render modu sunar:

1. **Static SSR** — sunucuda HTML üretilir, tam etkileşimsiz. Hızlı, SEO dostu.
2. **Interactive Server** — SignalR circuit, anlık UI güncellemeleri, sunucuda
   çalışır.
3. **Interactive WebAssembly** — tarayıcıda .NET çalışır.
4. **Interactive Auto** — ilk Server, sonra WASM'a geçer.

Senaryomuzda:
- Finansal ekranlar (tahakkuk, tahsilat, ödeme talimatı) **anlık** UI tepkisi
  gerektiriyor; kullanıcı aksiyonu → anında ekranda yansıma
- 1000+ site × kullanıcı ölçeğinde olacağız (SignalR scale-out gerekir)
- SEO kritik değil (internal admin + sakin portal)

## Karar

### Yönetici Portalı (Management Portal)
- **Varsayılan:** `InteractiveServer` (per-page basis)
- **Finansal ekranlar:** kesinlikle `InteractiveServer` (circuit state tutar)
- **Liste / rapor / arama ekranları:** `StreamRendering` ile Static SSR + enhanced
  navigation — circuit açmadan hızlı yükleme
- **Login sayfası:** Static SSR (form submit)

### Resident Portalı
- **Varsayılan:** `InteractiveAuto` (ilk Server, sonra WASM)
  - Sakinler mobilde kullanacak; WASM'a geçince sunucu kaynağı harcamıyoruz
  - İlk açılışta Server kadar hızlı, sonra client-side performans
- **Login + 2FA:** Static SSR

### Altyapı Kararları

1. **Redis SignalR backplane** zorunlu (çoklu instance deployment için):
   ```csharp
   builder.Services.AddSignalR()
       .AddStackExchangeRedis(redisConnectionString);
   ```

2. **Load balancer:** sticky session (ARR Affinity veya cookie-based).

3. **Circuit reconnection:** `ReconnectionUI` custom component ile kullanıcıya
   "bağlantı koptu, tekrar bağlanılıyor" gösterimi.

4. **`[PersistentState]`** attribute'u (.NET 10 yenisi) prerender+interactive
   arasında veri çift-yüklemeyi önlemek için kullanılacak.

5. **Prerender** aktif kalacak (ilk paint hızı için), ama data-fetch'li
   component'larda `[PersistentState]` ile persistence zorunlu.

## Sonuçları

**Olumlu:**
- Kritik UX (anlık finansal ekran) Interactive Server ile sağlanır
- Sakin portalı WASM'a geçerek sunucu yükünü azaltır
- SEO ihtiyacı olmayan bir sistemde Blazor'un tam gücünü kullanmış oluruz

**Olumsuz / Dikkat:**
- Her instance için RAM limitleri (her circuit ~50-100KB): pod başına 10-20K
  eşzamanlı kullanıcı civarı sınır
- Sticky session olmadan circuit kopar → LB konfigürasyonu kritik
- WASM bundle boyutunun .NET 10'da optimize olduğu bildirildi, yine de trimming
  ve AOT compilation açılacak

## Referanslar

- Microsoft Learn: ASP.NET Core Blazor render modes
- .NET 10 What's New: [PersistentState], WebAssembly bundle optimization
