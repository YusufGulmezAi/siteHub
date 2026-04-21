# ADR-0003: Kimlik & Yetkilendirme Stratejisi

**Durum:** Kabul Edildi
**Tarih:** 2026-04-19

## Bağlam

SiteHub'da bir kişi birden çok bağlamda (context) farklı rollerde yer alabilir:

> Ali Yılmaz:
> - X Yönetim Firması'nda **Muhasebeci**
> - A Sitesi'nde **Yönetim Kurulu Üyesi**
> - B Sitesi / 12 numara'da **Malik**
> - C Sitesi / 5 numara'da **Kiracı**

Kullanıcı login olduğunda hangi kimlikle çalıştığını seçmelidir (GitHub'da org
switch, Slack'te workspace switch gibi). Her bağlamda yetkileri farklıdır.

Ek gereksinimler:
- Kullanıcı adı olarak **TCKN / VKN / YKN** kullanılacak (checksum doğrulamalı)
- 2FA opsiyonel (kullanıcı açar): TOTP (Authenticator), SMS OTP, E-posta OTP
- İleride e-Devlet entegrasyonu düşünülebilir (federation)
- Solo dev başlatıyor — ops basit olmalı
- Lisans bütçesi: sıfır

## Değerlendirilen Seçenekler

### Seçenek 1: Duende IdentityServer
- Artıları: .NET-native, olgun, OAuth2/OIDC tam uyumlu
- Eksileri: **Ticari lisans** (~$1.5K-$6K/yıl, ciro bazlı); solo dev için gereksiz
  karmaşıklık (üçüncü taraf istemcilere kimlik sağlamıyoruz)

### Seçenek 2: Keycloak (self-hosted)
- Artıları: Açık kaynak, olgun, SSO yetkin
- Eksileri: Ayrı bir Java servis (operasyon yükü); öğrenme eğrisi; custom user
  modeli (multi-context) için özel eklentiler gerekiyor

### Seçenek 3: OpenIddict
- Artıları: Ücretsiz, .NET-native, OAuth2/OIDC tam uyumlu, ASP.NET Identity
  üzerine oturur
- Eksileri: Tam bir OAuth2 sunucusu — senin senaryonda (sadece iki Blazor Server
  app) bu kadar karmaşıklığa gerek yok

### Seçenek 4: ASP.NET Core Identity + Cookie Auth + Custom Membership Model ✅
- Artıları: Microsoft'un maintained kütüphanesi, ücretsiz, basit; şifre hash'leme,
  token üretimi, 2FA (TOTP/SMS/Email), lockout, passkey (FIDO2) yerleşik;
  iki Blazor Server uygulamamız aynı backend'i paylaşıyor, cookie auth yeterli;
  custom `Membership` ve `Context` modellerini ekleyerek multi-context akışı
  tamamen bizim kontrolümüzde kurarız
- Eksileri: Üçüncü taraf istemciye (mobil app, partner API) kimlik sağlamak
  istediğimizde OAuth2 sunucusu (OpenIddict) eklemek gerekecek — ama o zaman
  kadar Identity veri modelini değiştirmeden üstüne ekleyebiliriz

## Karar

**Seçenek 4** benimsendi:

- **Authentication:** ASP.NET Core Identity + Cookie Auth.
  - İki portal için **iki ayrı cookie scheme** (`MgmtAuth`, `ResidentAuth`).
  - Passkey (WebAuthn/FIDO2) desteği .NET 10'da yerleşik — ileride aktif edilir.
- **Authorization:** Custom veri modeli + policy-based authorization.
- **Kullanıcı adı:** `NationalId` value object (TCKN/VKN/YKN checksum doğrulamalı).
- **2FA:** Opsiyonel; TOTP (varsayılan) + SMS + E-posta seçenekleri.
- **Gelecek:** İhtiyaç doğduğunda OpenIddict eklenir (Identity store aynen korunur).

### Veri Modeli (özet)

```
User
  Id, NationalId (TCKN/VKN/YKN), NationalIdType, DisplayName,
  Email, Phone, PasswordHash, SecurityStamp, TwoFactorEnabled, ...

Membership  (User × Context × Roles)
  Id, UserId, ContextType (Firm|Site|Unit|System),
  ContextId, GrantedAt, Status, Roles[]

Role (context-scope'lu, seed edilir)
  Id, Name, ContextType, Permissions[]

Permission
  Key (örn: "invoice.create"), Description
```

### Login Akışı

1. Kullanıcı TCKN/VKN/YKN + şifre girer
2. (Varsa) 2FA doğrulanır
3. Kullanıcının tüm `Membership`'leri listelenir → "Hangi kimlikle devam edeceksin?"
4. Seçim yapılır → cookie'ye `ActiveContext` claim'leri yazılır
5. Sonraki istekler bu context'e göre yetkilendirilir
6. UI'da "Kimlik değiştir" butonu her zaman aktiftir

## Sonuçları

**Olumlu:**
- Sıfır lisans maliyeti
- Tek process, ops basit
- Multi-context mantığı tamamen bizim kontrolümüzde
- Gelecekte OIDC sunucusuna geçiş kolay (OpenIddict)

**Olumsuz / Dikkat:**
- Mobil uygulama gelirse OAuth2 sunucusu eklenmeli (ama bu kritik bir engel değil)
- Cookie-auth iki portalda ayrı scheme'lerle dikkatli kurulmalı (XSS/CSRF koruması)
- `NationalId` checksum doğrulaması domain'de value object olarak uygulanmalı

## Referanslar

- Microsoft Docs: ASP.NET Core Identity
- Microsoft Docs: Passkey Authentication in .NET 10
- TCKN algoritması: NVİ resmi dokümantasyonu
