# ADR-0008: Secrets Management (Sır/Parola Yönetimi)

**Durum:** Kabul Edildi
**Tarih:** 2026-04-19

## Bağlam

Bağlantı dizgileri (connection strings), API anahtarları, şifreler gibi hassas
bilgilerin kod deposuna commit edilmesi kategorik olarak kabul edilemez bir
güvenlik hatasıdır. KVKK ve endüstri standartları (OWASP, NIST) açıkça bunu
yasaklar.

Bir junior geliştiricinin yanlışlıkla `appsettings.json`'a şifre yazıp commit
etmesi ihtimalini de framework seviyesinde engellemek isteriz.

## Değerlendirilen Seçenekler

### 1. `appsettings.json`'a yazmak
- ❌ Kabul edilemez: şifreler git'e girer, permanent olarak kayıtlı kalır.
  Geçmişte commit edildiyse silmek bile yetmez.

### 2. `dotnet user-secrets`
- ✅ Microsoft'un resmi dev yaklaşımı — kullanıcı dizini dışında dosya tutar
- ❌ `dotnet user-secrets set ...` komutlarıyla tek tek girilir, yeni dev için
  onboarding zahmetli; docker-compose ile entegre çalışmaz

### 3. `.env` dosyası (kök dizinde) + environment variables
- ✅ Tek dosya — hem docker-compose hem .NET uygulamaları buradan okur
- ✅ Yeni geliştirici için onboarding kolay: `.env.example`'ı kopyala, doldur, çalış
- ✅ `.gitignore`'da engellenir, ASLA commit edilmez
- ✅ ASP.NET Core environment variable'ları otomatik okur (`__` = section separator)
- ⚠️ Dev dosyası — prod'da kullanılmaz

### 4. Secret Store (Azure Key Vault / HashiCorp Vault / AWS Secrets Manager)
- ✅ Production için endüstri standardı
- ✅ Rotasyon, denetim, erişim kontrolü
- ❌ Dev için overkill

## Karar

### Üç Katmanlı Strateji

| Ortam           | Yöntem                                   | Örnek                        |
|-----------------|------------------------------------------|------------------------------|
| **Development** | Kök dizinde `.env` dosyası               | `POSTGRES_PASSWORD=...`      |
| **Test / CI**   | GitHub Actions Secrets / GitLab Variables | CI'da env var olarak sağlanır |
| **Production**  | Azure Key Vault / HashiCorp Vault        | Orkestratör (K8s/Aspire) enjekte |

### Dev Kuralları

1. **`.env` dosyası asla commit edilmez.** `.gitignore`'da listeli. İlk hat
   savunması: geliştiricinin dikkati. İkinci hat: pre-commit hook (ileride).

2. **`.env.example` commit edilir.** Tüm gerekli anahtarları gösterir, gerçek
   değer yerine placeholder ("sitehub_dev_pw_change_me") taşır.

3. **`appsettings.json` dosyalarında sır YOK.** Yalnızca yapı (structure) ve
   güvenli default'lar. Gerçek değerler environment variable'lardan gelir.

4. **PowerShell script (`./scripts/dev-infra.ps1`) `.env`'i yükler** ve docker
   compose ile dotnet komutlarına process-level env var olarak aktarır. Tek
   komutla tüm stack ayağa kalkar.

5. **ASP.NET Core configuration hiyerarşisi:**
   ```
   Environment Variables (en yüksek öncelik — .env'den)
        ↓
   appsettings.{Environment}.json  (Development/Production)
        ↓
   appsettings.json                (en düşük öncelik, güvenli defaults)
   ```

6. **`__` konvansiyonu:** Env var'ları iç içe yapıyı `__` ile ifade eder.
   - `ConnectionStrings__Postgres` → `Configuration["ConnectionStrings:Postgres"]`
   - `Serilog__MinimumLevel__Default` → `Configuration["Serilog:MinimumLevel:Default"]`

### Production Kuralları

7. **Prod'da `.env` DOSYASI KULLANILMAZ.** K8s Secret / Azure Key Vault /
   Service-level environment variables kullanılır.

8. **Rotasyon:** Prod şifreleri 90 günde bir rotasyona tabidir (politika
   ileride tanımlanacak).

9. **Log'lara sızmama:** Connection string'ler, API key'leri, token'lar
   Serilog destructuring policy ile otomatik maskelenir (ADR-0006).

10. **Development şifreleri PROD'A TAŞINMAZ.** Her ortamın kendi secret'ları
    vardır; `.env.example`'daki "dev_pw_change_me" zaten ismiyle uyarı verir.

## Uygulama

- `./scripts/dev-infra.ps1 setup` komutu .env yoksa `.env.example`'ı kopyalar
  ve uyarı yazdırır
- `.env` dosyası süreç başlatılırken yüklenir, docker compose bunu okur,
  dotnet processler ASP.NET Core configuration via env var okur
- CI'da `.env` yerine GitHub Actions Secrets kullanılır

## Sonuçları

**Olumlu:**
- Tek kaynak (.env) — yeni dev onboarding 5 dakika
- Sır commit riski minimum (gitignore + appsettings'te sır yok)
- Prod'a geçiş temiz: aynı env var isimleri, farklı kaynak

**Olumsuz / Dikkat:**
- `.env` kaybolursa kişinin tekrar doldurması gerek — ekip içinde paylaşılmaz
- Windows'ta `.env` dosyası başlangıçta gizli görünmez — `ls -Force` gerekir

## Referanslar

- OWASP Secrets Management Cheat Sheet
- The Twelve-Factor App: Config
- Microsoft Docs: Safe storage of app secrets in development
