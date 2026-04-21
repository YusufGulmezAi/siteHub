# SiteHub

Toplu yapı (site, AVM, apartman) yönetim firmaları için SaaS platformu.

> İsim geçici — projeni istediğin gibi yeniden adlandırabilirsin. "SiteHub" yerine
> tercih ettiğin bir isim seçtiğinde, bu repo'da global find & replace yeterli.

## Teknoloji Yığını

- **.NET 10** (LTS, 3 yıl destek)
- **Blazor Server** + **MudBlazor 9.x** (Interactive Server render mode)
- **PostgreSQL 17** (schema-per-tenant multi-tenancy)
- **ASP.NET Core Identity** (cookie auth + 2FA)
- **MediatR** + **CQRS** (Application katmanında)
- **EF Core 10** (ORM)
- **Serilog** + **OpenTelemetry** (logging & observability)
- **Docker** (local dev ve production deployment)

## Mimari

**Modular Monolith + Clean Architecture + Vertical Slice**

Her özellik (feature) kendi içinde handler + validator + endpoint + UI olarak tek bir
klasörde toplanır. Katmanlar arası bağımlılık yönü tektir:

```
Domain  <---  Application  <---  Infrastructure  <---  Web (Blazor)
                   ^
                   |
                 Shared (cross-cutting)
```

Detaylı mimari kararlar için `docs/adr/` altındaki ADR (Architecture Decision Record)
dosyalarına bak.

## Mimari Kararlar (ADR'lar)

Tüm önemli mimari kararlar `docs/adr/` altında kayıtlıdır:

| No   | Başlık                                                                |
|------|-----------------------------------------------------------------------|
| 0001 | Modular Monolith + Clean Architecture + Vertical Slice                |
| 0002 | Multi-Tenancy: Schema-per-Tenant (PostgreSQL)                         |
| 0003 | Kimlik & Yetkilendirme: ASP.NET Identity + Cookie + Custom Membership |
| 0004 | Blazor Render Modları (Interactive Server + Static SSR hibrit)        |
| 0005 | Bağlam Geçişi, Hiyerarşik Erişim ve Sekme İzolasyonu                  |
| 0006 | Kapsamlı Loglama ve PII Maskeleme                                     |
| 0007 | Tarih/Saat ve Zaman Dilimi Yönetimi (UTC sakla, TR göster)            |

## Kritik Davranış Kuralları

1. **Zaman:** Kodun hiçbir yerinde `DateTime.Now` **YOK**. UTC ile çalışılır
   (`TimeProvider.GetUtcNow()`), UI'da `ITurkeyClock` ile gösterilir.

2. **Loglama:** Her HTTP request ve her CQRS command/query otomatik loglanır
   (Kim/IP/Endpoint/Süre/Sonuç). Hassas alanlar framework seviyesinde
   maskelenir (`SensitiveFields` policy).

3. **Bağlam (Context):** Management Portal'da login sonrası bağlam
   SEÇİLMEZ; AppBar'daki combobox'tan değiştirilir. Bağlam değişimi UI
   seviyesinde UX içindir — yetki her zaman backend'de (`ContextAuthorizationBehavior`)
   denetlenir.

4. **Sekme izolasyonu:** Aynı tarayıcıda farklı sekmeler farklı bağlamlarda
   çalışabilir. Blazor Server circuit per-tab olduğundan `IActiveContextAccessor`
   scoped servisi bunu doğal olarak sağlar.

5. **Hiyerarşik erişim:** Bir seviyedeki Membership, o seviyenin altındaki tüm
   bağlamlara otomatik erişim verir. System → tüm Firm/Site; Firm → altındaki
   tüm Site; Site → sadece o Site. Ayrı kullanıcı tanımlamaya gerek yok.

## İki Ayrı Portal — Farklı Hedef Kitle

Bu proje **iki bağımsız Blazor Server uygulaması** olarak inşa edilir:

### Management Portal (Yönetici Portalı)
- **Kullanıcılar:** Sistem, Kiracı (Yönetim Firması), Site yönetici ve personeli
- **Tasarım:** AdminLTE-inspired koyu sidebar, profesyonel yönetim arayüzü
- **Özellikler:** Menüde arama, context switcher (1200+ site arasında), hiyerarşik erişim
- **URL:** `https://localhost:5001`

### Resident Portal (Malik/Sakin Portalı)
- **Kullanıcılar:** Malik ve kiracı (ev kiracıları) — son kullanıcılar
- **Tasarım:** Görsel, estetik, basit, kullanıcı dostu; mobile-first
- **Özellikler:** Aidat görüntüleme, online ödeme, talep/şikayet, duyurular
- **URL:** `https://localhost:5101`
- **Durum:** İskelet mevcut; detaylı tasarım ayrı bir aşamada yapılacak

İki portal aynı backend'i (Domain, Application, Infrastructure) kullanır ama ayrı auth
cookie'leri, ayrı layout'ları, ayrı deployment'ları vardır.

## Proje Yapısı

```
sitehub/
├── src/
│   ├── SiteHub.Domain/              # Entities, Value Objects, Domain Events
│   ├── SiteHub.Shared/              # Cross-cutting (Result, Error, abstractions)
│   ├── SiteHub.Application/         # CQRS handlers, validators, business logic
│   ├── SiteHub.Infrastructure/      # EF Core, Identity, external services
│   ├── SiteHub.ManagementPortal/    # Blazor Server — yönetici portalı
│   └── SiteHub.ResidentPortal/      # Blazor Server — malik/sakin portalı
├── tests/
│   ├── SiteHub.Domain.Tests/        # Domain birim testleri
│   ├── SiteHub.Application.Tests/   # Handler testleri
│   └── SiteHub.Integration.Tests/   # Uçtan uca testler (Testcontainers)
├── docs/
│   └── adr/                         # Architecture Decision Records
└── docker/
    └── docker-compose.yml           # Local dev altyapısı (Postgres, Redis, Seq)
```

## Başlangıç (Getting Started)

### Gereksinimler

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop)
- **PowerShell 7+** (Windows'ta yerleşik, macOS/Linux'ta: `brew install powershell` / `apt install powershell`)
- Bir IDE: Visual Studio 2026 / JetBrains Rider / VS Code + C# Dev Kit

### Kurulum — 3 Adım

```bash
# 1. Repo'yu klonla
git clone <repo-url> sitehub
cd sitehub

# 2. .env dosyasını hazırla (şifreler burada tutulur, git'e commit EDİLMEZ)
cp .env.example .env
# Windows PowerShell:  Copy-Item .env.example .env

# 3. İlk kurulum (Docker başlat + migration'ları uygula)
# Windows:
scripts\dev-infra.cmd setup

# macOS / Linux:
./scripts/dev-infra.sh setup
```

> **Windows PowerShell'de dotnet run için .env yükleme:**
> `appsettings.Development.json` **artık connection string içermiyor** — tüm credential'lar `.env`'den gelir. Her yeni PowerShell oturumunda uygulamayı çalıştırmadan önce env'i yüklemeli:
>
> ```powershell
> .\env.ps1   # Script env var'ları process-level set eder — dot-source gerekmez
> dotnet run --project src/SiteHub.ManagementPortal
> ```
>
> Env'in yüklendiğini doğrulamak için: `echo $env:ConnectionStrings__Postgres`. Execution policy hatası alırsan: `Set-ExecutionPolicy -Scope CurrentUser RemoteSigned -Force`.

> **Windows'ta ilk kez çalıştırıyorsanız:** PowerShell script'leri güvenlik nedeniyle
> engelli olabilir. Detaylı çözüm: `scripts/README.md` içinde "Windows Kullanıcıları"
> bölümüne bakın. Kısaca: `Get-ChildItem -Path . -Recurse -File | Unblock-File` ve
> `Set-ExecutionPolicy -Scope CurrentUser RemoteSigned -Force`.

Kurulum komutu şunu yapar:
- `.env` yoksa `.env.example`'dan oluşturur (dev şifreleri güvenli default)
- Docker compose ile PostgreSQL, Redis, Seq, MailHog, MinIO başlatır
- PostgreSQL hazır olana kadar bekler
- EF Core migration'larını uygular (varsa)

Sonra uygulamayı çalıştır:

```powershell
# Windows PowerShell
.\env.ps1
dotnet run --project src/SiteHub.ManagementPortal    # http://localhost:5000
dotnet run --project src/SiteHub.ResidentPortal      # http://localhost:5100
```

### Günlük Kullanım

```bash
scripts\dev-infra.cmd status    # Durumu gör
scripts\dev-infra.cmd down      # Bilgisayarını kapatırken: servisleri durdur
scripts\dev-infra.cmd up        # Yarın sabah: tekrar başlat
scripts\dev-infra.cmd reset     # Temiz başlangıç istediğinde (veri silinir!)
```

Komutların tam listesi: `scripts/README.md`

### Servis URL'leri (Local Dev)

| Servis             | URL                      | Not                                |
|--------------------|--------------------------|------------------------------------|
| Management Portal  | https://localhost:5001   | Yönetici / site personeli          |
| Resident Portal    | https://localhost:5101   | Malik / sakin                      |
| PostgreSQL         | localhost:5432           | user: sitehub, pass: sitehub       |
| Seq (log viewer)   | http://localhost:5341    | Structured log UI                  |
| MailHog (SMTP)     | http://localhost:8025    | Test e-postaları                   |
| MinIO (S3 uyumlu)  | http://localhost:9001    | Dosya depolama (ileride)           |
| Redis              | localhost:6379           | Cache / SignalR backplane          |

## Ortamlar (Environments)

| Ortam       | Amaç                            | URL (örnek)                      |
|-------------|---------------------------------|----------------------------------|
| Development | Geliştirici lokali              | localhost                        |
| Test        | Otomatik testler + QA           | test.sitehub.tr                  |
| Demo        | Demo / pre-prod                 | demo.sitehub.tr                  |
| Production  | Canlı                           | app.sitehub.tr                   |

Her ortam için `appsettings.{Environment}.json` dosyası kullanılır. Hassas bilgiler
(connection string, API key) **asla** kod repository'sine girmez — secret manager veya
environment variable üzerinden okunur.

## Katkı Sağlama

Bu proje solo başlatıldı. Ekibe yeni arkadaşlar katıldığında şu kurallar geçerlidir:

1. Her özellik bir branch'te geliştirilir (`feature/...`).
2. Pull request zorunludur; ana branch'e direkt push yok.
3. Yeni modül/önemli teknik karar → yeni ADR.
4. Test yazılmamış kod merge edilmez.
5. `.editorconfig` kuralları zorunludur (CI'da kontrol edilir).

## Lisans

TBD.
