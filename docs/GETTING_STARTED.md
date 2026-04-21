# Başlangıç Rehberi — İlk Çalıştırma

Bu rehber SiteHub walking skeleton'ını makinende nasıl ayağa kaldıracağını
adım adım gösterir. Herhangi bir adımda hata alırsan, hatanın tam metnini
kopyalayıp Claude'a sor — çözmene yardımcı olur.

---

## 1. Ön Gereksinimler

İndirilmesi gerekenler:

- **.NET 10 SDK** → https://dotnet.microsoft.com/download
  Kurulumdan sonra terminalde doğrula:
  ```bash
  dotnet --version
  # 10.0.xxx görmelisin
  ```

- **Docker Desktop** → https://www.docker.com/products/docker-desktop
  Doğrula:
  ```bash
  docker --version
  docker compose version
  ```

- **Bir IDE:**
  - Visual Studio 2026 (Windows, ücretsiz Community edition yeterli)
  - JetBrains Rider (platform-bağımsız, ücretli ama kişisel kullanımda ücretsiz)
  - VS Code + C# Dev Kit eklentisi (platform-bağımsız, ücretsiz)

---

## 2. Projeyi Aç

Zip'i çıkarıp `sitehub/` klasörüne git:

```bash
cd sitehub
ls -la
# README.md, SiteHub.sln, src/, tests/, docker/, docs/ görmelisin
```

---

## 3. Altyapı Servislerini Başlat (Docker)

PostgreSQL, Redis, Seq, MailHog, MinIO'yu tek komutla ayağa kaldır:

```bash
cd docker
cp .env.example .env
docker compose up -d
```

Doğrulama:

```bash
docker compose ps
# Hepsi "running" durumda olmalı

# Browserdan açabildiğin servisler:
# - Seq   (log viewer):     http://localhost:5341
# - MailHog (e-posta test): http://localhost:8025
# - MinIO (dosya depolama): http://localhost:9001
```

Tekrar kök dizine dön:
```bash
cd ..
```

---

## 4. NuGet Paketlerini Yükle

Solution kök dizinindeyken:

```bash
dotnet restore
```

> **Not:** Directory.Packages.props içindeki bazı paket versiyonları zamanla
> güncellenmiş olabilir. Hata alırsan versiyon uyuşmazlığı olabilir — söylersen
> düzeltiriz.

---

## 5. Projeyi Derle

```bash
dotnet build
```

`Build succeeded` mesajı görmelisin. Hata varsa kopyalayıp bana sor.

---

## 6. Domain Testlerini Çalıştır

`NationalId` value object'inin doğru çalıştığını doğrula:

```bash
dotnet test tests/SiteHub.Domain.Tests/SiteHub.Domain.Tests.csproj
```

Tüm testler yeşil geçmeli. (Test sayılarını bkz: NationalIdTests.cs)

---

## 7. Yönetici Portalını Çalıştır

Yeni bir terminal aç ve:

```bash
dotnet run --project src/SiteHub.ManagementPortal
```

Terminal log'unda şunu göreceksin:
```
info: SiteHub Management Portal hazır. URL: https://localhost:5001, http://localhost:5000
```

Browser'da aç: **https://localhost:5001**

> İlk açılışta browser SSL sertifikası uyarısı gösterebilir. Development
> sertifikasını güvenilir olarak işaretlemek için:
> ```bash
> dotnet dev-certs https --trust
> ```

Başarılı açılırsa:
- MudBlazor tasarımıyla "Hoş Geldin" sayfası göreceksin
- Sağ üstte dark/light tema geçiş butonu
- Solda menü (daraltılabilir)
- Ortada "Tıkla" butonu — Etkileşim testi: butonla sayı artıyorsa SignalR circuit çalışıyor

---

## 8. Malik/Sakin Portalını Çalıştır

BAŞKA BİR terminal açıp:

```bash
dotnet run --project src/SiteHub.ResidentPortal
```

Browser'da aç: **https://localhost:5101**

Farklı bir layout ve yeşil tema göreceksin (sakin-odaklı tasarım).

---

## 9. Log'ları İzle

Seq UI'a git: **http://localhost:5341**

İki portal da buraya structured log basar. Her HTTP request'in detaylarını,
hata varsa stack trace'ini burada izleyebilirsin. Production'da Serilog'u
farklı bir sink'e (Grafana Loki, Elasticsearch) yönlendirmen yeterli.

---

## Her Şey Çalıştı — Sırada Ne Var?

Walking skeleton ayakta. Bundan sonra eklenecekler (sıralı):

1. **EF Core DbContext + İlk Migration**
   - PostgreSQL'e bağlanma
   - Schema-per-tenant altyapısı
   - `dotnet ef migrations add InitialCreate`

2. **ASP.NET Core Identity Kurulumu**
   - `ApplicationUser` (NationalId ile)
   - Login sayfası, kayıt sayfası
   - 2FA (TOTP) aktivasyonu

3. **Multi-Context Membership Modeli**
   - User × Context × Roles
   - Kimlik değiştirme akışı

4. **İlk İş Modülleri**
   - Firma Tanımlama
   - Site Tanımlama (yeni tenant schema provisioning ile)
   - Malik/Sakin Tanımlama
   - Bütçe, Tahakkuk, Tahsilat

Her biri için ayrı ADR + kod + test yazacağız. Bir sonraki mesajda "EF Core +
Identity kurulumu ile devam edelim" dersen oradan başlarız.

---

## Sorun Giderme

**Port çakışması (5001/5101 kullanılıyor hatası):**
`Properties/launchSettings.json` dosyasında farklı port seç.

**Docker compose başlamıyor:**
Windows'ta Docker Desktop'un WSL2 backend'ini kontrol et.

**"dotnet restore" NU1101 hatası:**
Paket versiyonu mevcut değil. Directory.Packages.props'ta ilgili paketin
son versiyonunu NuGet'ten bakıp güncelle.

**Blazor sayfası beyaz geliyor:**
Browser console'u aç (F12). MudBlazor CSS/JS dosyaları yüklenmiyor olabilir.
Hata mesajını paylaş.

**SSL sertifikası uyarısı:**
```bash
dotnet dev-certs https --trust
```

Daha fazla hata için dotnet diagnostic log'larını kullan:
```bash
export Logging__LogLevel__Microsoft=Information
dotnet run --project src/SiteHub.ManagementPortal
```
