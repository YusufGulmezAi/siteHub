# F.6 Madde 9 — Global 401 Handler

**Tarih:** 2026-04-24
**Scope:** 4 yeni dosya + 1 değişen dosya (MainLayout) + manuel Program.cs snippet.

## Amaç

Uzun süre açık kalan portal ekranında session timeout olunca API 401 dönüyor. Şu an sadece snackbar hatası gösteriliyor, kullanıcı ne olduğunu anlamıyor. Bu özellikle:

- 401 yakalanır (HttpClient handler)
- "Oturumunuz sona erdi" dialog'u 5 saniye countdown ile gösterilir
- Otomatik veya butonla `/auth/login`'e force redirect

## Mimari

```
API call → Primary HttpClient
   ↓
CookieForwardingHandler (cookie ekle)
   ↓
UnauthorizedResponseHandler ← (yeni)
   ↓
Response gelir
   ↓
401 ise → IAuthenticationEventService.RaiseSessionExpiredAsync()
   ↓
MainLayout dinliyor → DialogService.ShowAsync<SessionExpiredDialog>()
   ↓
Dialog: 5 sn countdown → Nav.NavigateTo("/auth/login", forceLoad: true)
```

**Tek-tetikleme:** `AuthenticationEventService` içinde `Interlocked.Exchange` flag — paralel 401'lerde dialog bir kez açılır.

**Path filtering:** `/auth/login`, `/auth/logout`, `/auth/verify-2fa` URL'lerinde 401 yoksayılır.

## Dosya Listesi

### Yeni (4)

| Dosya | Açıklama |
|---|---|
| `Services/Authentication/IAuthenticationEventService.cs` | Event publisher abstraction |
| `Services/Authentication/AuthenticationEventService.cs` | Scoped event publisher (tek-tetikleme) |
| `Services/Api/UnauthorizedResponseHandler.cs` | DelegatingHandler, 401 yakalar |
| `Components/Shared/Dialogs/SessionExpiredDialog.razor` | 5 sn countdown dialog |

### Değişen (1)

| Dosya | Değişiklik |
|---|---|
| `Components/Layout/MainLayout.razor` | `IAuthenticationEventService`'e subscribe, `SessionExpired` → dialog aç |

## Program.cs Manuel Değişiklikler

Mevcut `Program.cs`'te **iki** yer değişmeli. Zip `Program.cs`'i DAHİL ETMİYOR — elle eklemen gerek (daha güvenli, diğer servisleri yanlışlıkla bozmayalım).

### 1. DI kayıtları — C.2 Permission service'in hemen altına ekle

Şu bloğun altına:
```csharp
builder.Services.AddScoped<SiteHub.Application.Abstractions.Authorization.ICurrentUserPermissionService,
    SiteHub.ManagementPortal.Services.Authorization.CurrentUserPermissionService>();
```

Şunu ekle:

```csharp
    // ─── F.6 Madde 9: Global 401 handler + event service ──────────────
    builder.Services.AddScoped<
        SiteHub.ManagementPortal.Services.Authentication.IAuthenticationEventService,
        SiteHub.ManagementPortal.Services.Authentication.AuthenticationEventService>();
    builder.Services.AddTransient<SiteHub.ManagementPortal.Services.Api.UnauthorizedResponseHandler>();
```

### 2. 3 HttpClient kaydına `UnauthorizedResponseHandler` ekle

**Sıra önemli!** `UnauthorizedResponseHandler` **sonradan** eklenir ki pipeline'da **önce** çalışsın:
- HttpClient pipeline'ı: handler register sırası ile **ters** çalışır
- Biz 401'i en dışta yakalamak istiyoruz — Cookie'yi eklemeden önce yakalasak mantıksız (cookie olmadan gönderdik zaten 401 alır)
- Doğru: **Cookie ekle → sonra 401 kontrol et** → bu yüzden register sırası: Cookie önce, Unauthorized sonra

`GeographyApi`, `SitesApi`, `OrganizationsApi` kayıtlarının HER BİRİNE `UnauthorizedResponseHandler` satırı eklenecek:

**Mevcut pattern:**
```csharp
builder.Services.AddHttpClient<...IGeographyApi, ...GeographyApi>(client =>
    {
        client.BaseAddress = new Uri(apiBaseAddress);
    })
    .AddHttpMessageHandler<SiteHub.ManagementPortal.Services.Api.CookieForwardingHandler>();
```

**Yeni hâli:**
```csharp
builder.Services.AddHttpClient<...IGeographyApi, ...GeographyApi>(client =>
    {
        client.BaseAddress = new Uri(apiBaseAddress);
    })
    .AddHttpMessageHandler<SiteHub.ManagementPortal.Services.Api.CookieForwardingHandler>()
    .AddHttpMessageHandler<SiteHub.ManagementPortal.Services.Api.UnauthorizedResponseHandler>();
```

**3 HttpClient için aynı ekle:** Geography, Sites, Organizations.

## Uygulama Akışı

```powershell
cd D:\Projects\sitehub

# 1. Zip'i aç (4 yeni dosya + MainLayout)
Unblock-File C:\Users\<sen>\Downloads\sitehub-f6-m9.zip
Expand-Archive -Path "C:\Users\<sen>\Downloads\sitehub-f6-m9.zip" `
    -DestinationPath "D:\Projects\sitehub\" -Force

# 2. Program.cs'i elle düzenle (yukarıdaki 2 değişiklik)
notepad src\SiteHub.ManagementPortal\Program.cs

# 3. Build
dotnet build
```

## Smoke Test

Build temizse:

```powershell
dotnet run --project src\SiteHub.ManagementPortal
```

### Test 1 — Normal kullanım (dialog açılmamalı)
1. Admin login
2. `/organizations` sayfasında normal gez
3. Dialog **açılmamalı**. Hata olmadığında hiçbir değişiklik hissedilmiyor olmalı.

### Test 2 — Session expired senaryosu (dialog açılmalı)
**Yöntem A — Redis'i manuel temizle (önerilen):**
1. Admin login + `/organizations`
2. Redis CLI: `redis-cli` → `FLUSHDB` (dev Redis'te tüm session silinir)
3. Sayfada bir link tıkla veya filtreyi değiştir
4. API 401 döner → dialog açılmalı:
   - Sarı uyarı ikonu + "Oturumunuz Sona Erdi" başlığı
   - Countdown: "5 saniye sonra..." → "4 saniye sonra..." → ...
   - Progress bar sağdan sola iner
   - "Giriş Ekranına Git" butonu sağ altta
5. 5 saniye sonra otomatik `/auth/login`'e gitmeli

**Yöntem B — appsettings ile TTL kısalt** (daha yavaş ama doğal test):
- Session TTL'i 1 dakikaya çek, 1 dk bekle, sayfayı kullan.

### Test 3 — Multiple 401 (dialog bir kez açılmalı)
1. Test 2'yi yap ama hızlıca paralel birkaç request tetikle (arama + filtre)
2. Dialog **bir** kez açılmalı, log'da `SessionExpired tekrar tetiklendi, yoksayıldı` görünür.

### Test 4 — Login sayfasında 401 yoksayılır
1. Logout yap
2. `/auth/login`'de yanlış şifre dene (401 dönecek)
3. Dialog **açılmamalı** — normal yanlış-şifre hatası görünmeli.

## Commit Önerisi

```
Faz F.6 Madde 9: Global 401 handler + session expired dialog

Uzun s\u00fcre a\u00e7\u0131k kalan portal'da session timeout oldu\u011funda API 401
d\u00f6n\u00fcyor ve kullan\u0131c\u0131 kar\u0131\u015f\u0131yor. Bu commit cross-cutting bir 401 handler
ekliyor; dialog + 5 sn countdown + otomatik /auth/login redirect.

Yeni:
- Services/Authentication/IAuthenticationEventService.cs
  Event publisher abstraction (SessionExpired).

- Services/Authentication/AuthenticationEventService.cs
  Scoped impl. Interlocked.Exchange ile tek-tetikleme garantili.

- Services/Api/UnauthorizedResponseHandler.cs
  DelegatingHandler. Response 401 ise event raise eder.
  /auth/login, /auth/logout, /auth/verify-2fa yoksay\u0131l\u0131r.

- Components/Shared/Dialogs/SessionExpiredDialog.razor
  5 sn countdown + progress bar + 'Giri\u015f Ekran\u0131na Git' butonu.
  Timer ile otomatik redirect; BackdropClick ve ESC kapal\u0131.

De\u011fi\u015fen:
- Components/Layout/MainLayout.razor
  IAuthenticationEventService'e subscribe, event'te dialog a\u00e7\u0131yor.
  IAsyncDisposable ile unsubscribe (memory leak \u00f6nlemi).

Program.cs manuel de\u011fi\u015fiklik:
- 2 DI kayd\u0131 eklendi (AuthenticationEventService + Handler)
- 3 HttpClient'a AddHttpMessageHandler<UnauthorizedResponseHandler>
  eklendi (Cookie handler'dan SONRA -> pipeline'da \u00d6NCE \u00e7al\u0131\u015f\u0131r)

Test: Build temiz.
Smoke: Redis FLUSHDB -> dialog a\u00e7\u0131l\u0131r -> 5 sn sonra /auth/login.
       /auth/login'de 401 yoksay\u0131l\u0131r.
       Paralel 401'lerde dialog bir kez a\u00e7\u0131l\u0131r.

Kapsam d\u0131\u015f\u0131: 403 Forbidden ayr\u0131 dialog (ileride),
auto-reconnect SignalR circuit'i, retry policy.
```

## Bilinmesi Gerekenler

### DI scope: Scoped vs Singleton

`IAuthenticationEventService` **Scoped** seçildi. Blazor Server'da scope = Circuit.
- Aynı kullanıcının HttpClient handler ve MainLayout'u aynı instance'ı görür ✓
- Farklı kullanıcıların event'leri birbirini etkilemez ✓

Handler `Transient` ama `HttpClientFactory` registration'ı Blazor Server'da scoped lifetime ile çalışır (request/circuit scope içinde). Bu pattern'in çalışacağı varsayımı büyük — test edip doğrulayacağız.

### Event bazlı pattern neden seçildi

Alternatif: `IDialogService` enjekt etmek doğrudan handler'a. **Kötü fikir** — handler HttpClient pipeline'ında, UI katmanına direkt bağımlı olamaz (separation of concerns). Event pattern zayıf bağlı.

### 403 Forbidden
403 (yetki var ama izin yok) ayrı bir durum — kullanıcı login'li ama bu sayfaya yetkisi yok. Bu senaryo için ayrı dialog gerekir (C.5 sonrası ayrı madde).
