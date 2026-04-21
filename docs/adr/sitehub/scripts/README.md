# SiteHub Dev Scripts

## `dev-infra` — Altyapı Yöneticisi

PowerShell 7+ gerekir (Windows / macOS / Linux hepsinde çalışır).

### ⚠️ Windows Kullanıcıları — İlk Kurulum

Windows güvenlik politikaları PowerShell script'lerini varsayılan olarak engeller.
Aşağıdaki adımları **TEK SEFER** yapmanız gerekir:

```powershell
# 1) Zip'ten açılan dosyaların "internet'ten geldi" işaretini kaldır
Get-ChildItem -Path . -Recurse -File | Unblock-File

# 2) Execution policy'yi gevşet (sadece kullanıcı kapsamında, güvenli)
Set-ExecutionPolicy -Scope CurrentUser -ExecutionPolicy RemoteSigned -Force

# 3) PowerShell 7 yüklü değilse yükle
winget install --id Microsoft.PowerShell --source winget
```

**İPUCU:** Windows'ta Başlat menüsünden "PowerShell 7" uygulamasını açın
(eski "Windows PowerShell" değil). Terminal komutu: `pwsh`.

### İki Çağırma Yöntemi

Her iki yöntem de aynı işi yapar — `.cmd` / `.sh` wrapper'ı execution policy ve
sürüm sorunlarını otomatik halleder:

```powershell
# Windows (cmd veya powershell.exe):
scripts\dev-infra.cmd setup

# PowerShell 7 (pwsh) — manuel çağırma:
./scripts/dev-infra.ps1 setup

# macOS / Linux (bash):
./scripts/dev-infra.sh setup
```

### İlk Kurulum

```powershell
# Her şey tek komutla — docker up + migrate
scripts\dev-infra.cmd setup
```

### Günlük Kullanım

```powershell
scripts\dev-infra.cmd up            # Docker servisleri başlat
scripts\dev-infra.cmd down          # Durdur (veri korunur)
scripts\dev-infra.cmd restart       # Hızlı down + up
scripts\dev-infra.cmd status        # Durum + URL'ler
scripts\dev-infra.cmd logs postgres # Bir servisin loglarını izle
scripts\dev-infra.cmd psql          # PostgreSQL'e shell aç
```

### Güncelleme & Sıfırlama

```powershell
# İmajları son sürüme güncelle (veri korunur)
scripts\dev-infra.cmd pull

# ⚠️ Reset — veriyi sil + son imajları çek + baştan kur
scripts\dev-infra.cmd reset
# Onay için 'RESET' yazmak gerekir

# ☢️ Nuke — her şeyi sıfırla (dev cache, imajlar, container, volume, tool cache)
# Projeyi SIFIRDAN, temiz bir şekilde ayağa kaldırır.
scripts\dev-infra.cmd nuke
# Onay için 'NUKE' yazmak gerekir
```

### Migration İşlemleri

```powershell
# Yeni migration ekle
scripts\dev-infra.cmd add-migration InitialCreate

# Pending migration'ları veritabanına uygula
scripts\dev-infra.cmd migrate
```

## Nasıl Çalışır?

1. Script repo kökündeki `.env` dosyasını yükler → process-level environment
   variable'lar olur
2. Docker compose'a `--env-file` ile aktarır (compose dosyası bu değişkenleri kullanır)
3. `dotnet` komutları aynı process'ten başlatıldığı için env var'ları otomatik alır
4. ASP.NET Core `ConnectionStrings__Postgres` gibi env var'ları
   `Configuration["ConnectionStrings:Postgres"]` olarak görür

## Önemli Kurallar

- **`.env` dosyasını ASLA commit etme** (gitignore'da engellenmiş)
- **Şifreleri LOG'lara yazma** — Serilog destructuring policy otomatik maskeliyor
- **Production'da `.env` KULLANILMAZ** — Azure Key Vault / K8s Secrets olur
  (detay: ADR-0008)

## Sorun Giderme

**"Docker çalışmıyor" hatası:**
Docker Desktop'ı başlat, 30 sn bekle, tekrar dene.

**"dotnet-ef bulunamadı":**
Script otomatik `dotnet tool restore` yapar. Başarısız olursa:
```powershell
dotnet tool restore
```

**Migration uygulanmıyor / "PostgreSQL hazır değil":**
```powershell
./scripts/dev-infra.ps1 logs postgres
# Postgres başlayana kadar bekle, sonra tekrar:
./scripts/dev-infra.ps1 migrate
```

**Port çakışması (5432, 6379, vb. kullanımda):**
`.env`'de PORT değerlerini değiştir (örn: POSTGRES_PORT=15432) ve
`docker-compose.yml`'deki port mapping'leri güncelle.
