# ADR-0001: Modular Monolith + Clean Architecture + Vertical Slice

**Durum:** Kabul Edildi
**Tarih:** 2026-04-19
**Karar Veren:** SiteHub Takımı

## Bağlam

SiteHub, toplu yapı yönetim firmaları için çoklu modülden oluşan bir SaaS platformudur.
Planlanan modüller: Identity & Tenancy, Site/Firma Yönetimi, Malik/Sakin,
Bütçe & Tahakkuk, Tahsilat, İletişim (Talep/Şikayet), Muhasebe (v2), Bordro (v2),
Satın Alma (v2), Beyanname (v2), vb.

Başlangıçta solo bir geliştirici tarafından yazılacak, sonra ekibe yeni üyeler
katılacaktır. Hedef: (1) hızlı teslimat, (2) yeni geliştiricilerin kolayca
katılabileceği temiz bir kod tabanı, (3) ileride büyüme/ölçeklenme esnekliği.

## Değerlendirilen Seçenekler

### Seçenek 1: Klasik Katmanlı Mimari (N-Tier)
- Artıları: Basit, tanıdık
- Eksileri: Domain mantığı katmanlara yayılır, büyük projede "spagetti" riski yüksek,
  modüller arası sınırlar belirsiz

### Seçenek 2: Microservices (mikroservisler)
- Artıları: Her modül bağımsız deploy, ölçeklenme esnekliği
- Eksileri: Operasyonel yük çok yüksek (solo dev için imkânsız), dağıtık sistem
  problemleri (eventual consistency, network failure), erken optimizasyon tuzağı

### Seçenek 3: Modular Monolith + Clean Architecture + Vertical Slice ✅
- Artıları: Tek deployment (solo dev'e uygun), modüller arası sınırlar net,
  gerektiğinde bir modül microservice'e çıkarılabilir (bounded context zaten net)
- Eksileri: Disiplin gerektirir (modüller birbirine bulaşmasın), modül iletişimi
  için in-process event/mediator altyapısı kurmak gerekir

## Karar

**Modular Monolith + Clean Architecture + Vertical Slice** benimsenecektir.

Yapı kuralları:

1. **Katmanlar** (bağımlılık yönü tek yönlü):
   ```
   Domain <── Application <── Infrastructure <── Web (Blazor)
                  ▲
                  └── Shared (kernel)
   ```

2. **Domain** saf C#'dir — hiçbir framework bağımlılığı yok.

3. **Application** CQRS+MediatR kullanır. Her use case için bir `Command`+`Handler`
   veya `Query`+`Handler` (Vertical Slice).

4. **Infrastructure** EF Core, Identity, dış servisler. Application'daki interface'leri
   implement eder.

5. **Modüller** (ileride): Application ve Domain içinde feature klasörleri olarak yer
   alır. Örn: `Application/Features/Billing/CreateInvoice/...`

6. Modüller arası iletişim: doğrudan referans değil, **MediatR Notification** ile.

7. Mimari kuralları test ile korunur: `NetArchTest.Rules` paketi ile yapısal testler.

## Sonuçları

**Olumlu:**
- Solo dev tek repo/tek deployment ile ilerler, ops basit
- Yeni geliştirici geldiğinde klasör yapısından modülü anlar
- "Hangi katmanda ne yazılır" sorusu net cevaplı
- Her modül future-proof: gerektiğinde microservice'e çıkar

**Olumsuz / Dikkat:**
- Modüller arasında "kestirme yol" yapmamak için disiplin şart
- In-process MediatR event'leri yerine ileride gerçek message broker (RabbitMQ/NATS)
  gerekirse geçiş yapılmalı
- Her yeni feature için daha fazla boilerplate (handler+validator+endpoint) yazılır

## Referanslar

- Robert C. Martin, *Clean Architecture*
- Kamil Grzybek, "Modular Monolith Primer"
- Jimmy Bogard, "Vertical Slice Architecture"
