# ADR-0011: Kimlik, Yetki ve Oturum Modeli

**Durum:** Taslak (onay bekliyor)
**Tarih:** 2026-04-19
**İlgili:** ADR-0003 (Identity stratejisi), ADR-0005 (Bağlam geçişi), ADR-0006 (Audit)

## Bağlam

SiteHub'da kimlik, yetki ve oturum yönetimi 5 sorumluluğa ayrılır:

1. **Person** — Kişi kaydı. TCKN/VKN/YKN sahibi gerçek/tüzel kişiler.
2. **LoginAccount** — Person'a opsiyonel olarak bağlı giriş hesabı.
3. **Role + Permission** — Dinamik roller, statik izinler.
4. **Membership** — LoginAccount'un hangi bağlamda hangi rolde olduğu.
5. **Session** — Aktif oturum (sıkı tek-IP/tek-cihaz kuralları).

Kararlar iş gerekleri tarafından zorlanıyor: hissedarlar Person olmalı (giriş yapmayabilir), rol yaratma dinamik (her organizasyon özel rol oluşturabilir), ama izinler kod-güvenli (kullanıcı yeni izin yaratamaz), oturum ciddi banka-seviyesi güvenlik gerektiriyor.

## Karar Özeti

| Konu | Karar |
|---|---|
| Person-LoginAccount | Ayrılır (KVKK + veri minimizasyon) |
| Rol | **Dinamik** — DB tablosu, kullanıcı yaratır |
| Permission | **Statik** — kod içinde const, deploy sırasında DB'ye senkronize |
| Permission atama | Yaratıcı **sadece kendi sahip olduğu** izinleri yeni role atayabilir (privilege escalation koruması) |
| Rol yaratma yetkisi | System: SystemAdmin • Org: SystemAdmin + OrgManager • Site: SystemAdmin + OrgManager • ServiceOrg: ServiceOrg Manager |
| Login | Tek input alanı (TCKN/VKN/YKN/Email/Cep — otomatik algılama) |
| LoginSchedule default | NULL/boş → 7/24 giriş serbest |
| 2FA | Opt-in, metot-bazlı (her metot önce doğrulanır); son metot pasifleşirse TwoFactorEnabled otomatik false |
| Oturum | Tek IP, tek cihaz, IP değişimi = kapat |
| Remember Me | MVP'de yok (v2 — mobil native app ile) |
| URL | Kod-tabanlı (GUID değil): `/c/org/{code}`, `/c/site/{code}/units/{unitCode}` |
| Cache | Redis, TTL 15 dk, permission değişince anında invalidate |
| Broadcast | SignalR ile rol/izin değişikliğini kullanıcıya push |
| Responsive | Desktop-first + responsive (mobil web tam kapasite) |

---

## 1. Person (Kişi Kaydı)

Sistemdeki her "insan" veya "tüzel kişi" kaydıdır. Giriş yapıp yapmadığına bakılmaz.

### Alanlar

| Alan | Tip | Zorunlu | Not |
|---|---|---|---|
| Id | Guid (v7) | ✓ | |
| NationalId | Value Object | ✓ | TCKN/VKN/YKN — sistem geneli UNIQUE, checksum validasyonu |
| PersonType | enum | ✓ | Individual (TCKN/YKN) veya Corporate (VKN) |
| FullName | string(300) | ✓ | Hissedarlar/kiracılar için "Tam Unvan" |
| MobilePhone | string(20) | ✓ | E.164 format (`+905xxxxxxxxx`) |
| Email | string(320) | ✗ | Talep üzerine login için tanımlanırsa zorunlu olur |
| KepAddress | string(320) | ✗ | Tüzel kişi için önerilir |
| ProfilePhotoUrl | string | ✗ | MinIO'da saklanır |
| NotificationAddressId | FK → addresses | ✗ | Tebligat adresi (Neighborhood FK + açık adres 1/2) |
| IsActive | bool | ✓ | Soft-deprecate için |

### NationalId Value Object

```csharp
public sealed record NationalId(string Value, NationalIdType Type)
{
    public static NationalId CreateTckn(string value);      // 11 hane, checksum
    public static NationalId CreateVkn(string value);       // 10 hane, checksum
    public static NationalId CreateYkn(string value);       // 11 hane, 99 ile başlar
    public static NationalId Parse(string value);           // format algılama
}
```

DB'de tek string kolon + `person_type` enum ile saklanır.

### Silme Politikası

Person **fiziksel silinmez**. Audit log'da ve tarihçede referans verilir. Gerektiğinde `IsActive = false` olur. Person'ı silmek:
- Hissedar tarihçesini bozar
- Audit log'daki denormalize user_name'i anlamsız hale getirir
- KVKK "silme hakkı" için özel bir "pseudonymize" işlemi olacak (v2)

---

## 2. LoginAccount (Giriş Hesabı)

Person'a bağlı **opsiyonel** bir giriş hesabı.

### Alanlar

| Alan | Tip | Zorunlu | Not |
|---|---|---|---|
| Id | Guid | ✓ | |
| PersonId | FK | ✓ | Bire bir (Person → 0..1 LoginAccount) |
| LoginEmail | string(320) | ✓ | Login için; Person.Email'den ayrı olabilir |
| PasswordHash | string | ✓ | ASP.NET Core Identity (PBKDF2) |
| IsActive | bool | ✓ | Default true |
| ValidFrom | datetimeoffset | ✗ | Bu tarihten önce login engellenir |
| ValidTo | datetimeoffset | ✗ | Bu tarihten sonra login engellenir |
| IpWhitelist | string[] (CIDR) | ✗ | Boş = tüm IP'ler |
| LoginSchedule | JSONB | ✗ | Gün × saat aralığı (detay altta) |
| LastLoginAt | datetimeoffset | ✗ | |
| LastLoginIp | string | ✗ | |
| FailedLoginCount | int | ✓ | Default 0 |
| LockoutUntil | datetimeoffset | ✗ | Brute-force koruma |

### LoginSchedule Format

**Varsayılan davranış:** `LoginSchedule` NULL veya boş ise kullanıcı **7/24 giriş yapabilir** (kısıtlama yok). Sadece özel kısıtlama istendiğinde dolar.

```json
// Özel kural örneği:
{
  "timezone": "Europe/Istanbul",
  "rules": [
    {
      "days": ["Mon", "Tue", "Wed", "Thu", "Fri"],
      "startTime": "08:00",
      "endTime": "18:00"
    },
    {
      "days": ["Sat"],
      "startTime": "09:00",
      "endTime": "13:00"
    }
  ]
}
```

Yukarıdaki örnekte Pazar günü ve belirtilen saat aralıkları dışında login engellenir.

**UI davranışı:** "Saat kuralı ekle" kutucuğu işaretsiz → `LoginSchedule = null` (7/24 çalışma). İşaretlendiğinde formda günler ve saat aralıkları girilir.

---

## 3. Login Akışı

### 3.1. Tek Input Alanı

Login ekranında **tek bir input** — kullanıcı TCKN/VKN/YKN/Email/Cep girebilir. Sistem otomatik algılar:

```
Girdi algılama algoritması (sırayla dener):
  1. @ içeriyor mu? → Email
  2. Sadece rakam mı?
     2a. 11 rakam, "99" ile başlıyor → YKN
     2b. 11 rakam, geçerli TCKN checksum → TCKN
     2c. 10 rakam, geçerli VKN checksum → VKN
     2d. 10-11 rakam, "+90" veya "0" ile başlıyor → MobilePhone
  3. Algılanamazsa "Girdi formatı tanınmadı" hatası
```

Algılandıktan sonra:
- **Email, TCKN, VKN, YKN** → Person/LoginAccount bulunur, şifre alanı açılır
- **MobilePhone** → SMS OTP gönderilir, kod alanı açılır

### 3.2. Kural Kontrolü (Sırayla)

Şifre/OTP doğruysa, login kabul edilmeden önce:

1. `IsActive == false` → `AccountInactive` event, engelleme
2. Şu an `ValidFrom <= now <= ValidTo` aralığında mı? Değilse → `AccountOutOfValidity` event, engelleme
3. `IpWhitelist` dolu mu + şimdiki IP içinde mi? Değilse → `IpNotAllowed` event, engelleme
4. `LoginSchedule` var mı + şimdiki zaman kurallara uyuyor mu? Uymuyorsa → `ScheduleBlocked` event, engelleme
5. `LockoutUntil > now` mı? Öyleyse → `AccountLocked` event, engelleme

Hepsi geçerse:
6. **Tek oturum kontrolü** (Bölüm 7'de detay): bu kullanıcının başka aktif session'ı var mı? Varsa → eski kapatılır, SignalR ile eski browser'a "logout" push
7. **2FA etkinse** → ikinci faktör talep edilir
8. Session açılır (Bölüm 7)

### 3.3. Audit

Her başarısız + başarılı login **`audit.security_events`**'e yazılır (Bölüm 9).

---

## 4. İki Faktörlü Doğrulama (2FA)

### 4.1. Metotlar

| Metot | Açıklama | Doğrulama |
|---|---|---|
| Email | E-posta kodu | E-posta adresine link/kod; tıklanınca `VerifiedAt` dolar |
| SMS | Telefon kodu | Cep telefonuna 6 haneli kod |
| TotpApp | Google Authenticator vb. | QR kod → app'te kayıt → kod ile onay |
| Push | Mobil uygulama onayı | v2 (MVP yok) |

### 4.2. two_factor_methods Tablosu

| Alan | Tip | Not |
|---|---|---|
| Id | Guid | |
| LoginAccountId | FK | |
| Method | enum | Email, Sms, TotpApp |
| Secret | string (encrypted) | TOTP için; Email/SMS'te null |
| VerifiedAt | datetimeoffset? | Null = metot doğrulanmadı |
| IsActive | bool | Kullanıcı deaktive edebilir |
| LastUsedAt | datetimeoffset? | Analiz için |

### 4.3. Akış

**Metot ekleme (profilden):**
1. Kullanıcı "SMS ile 2FA ekle" der
2. Telefona kod gönderilir
3. Kullanıcı kodu girer
4. Kod doğruysa → `VerifiedAt = now`, `IsActive = true`
5. `audit.security_events` yazılır (TwoFactorEnabled)

**Login sırasında:**
1. Kullanıcının `two_factor_methods` tablosunda `VerifiedAt != null && IsActive = true` olan metotları listelenir
2. Hiç yoksa → 2FA zaten opt-in, yoksa direkt login
3. Varsa → kullanıcı metot seçer (radyo butonlar)
4. Seçilen metot için kod üretilir/beklenir
5. Kod doğruysa login tamamlanır; `LastUsedAt = now`
6. `audit.security_events` yazılır (TwoFactorVerified)

**Metot kaldırma/yeniden doğrulama:**
- Pasifleştirme: `IsActive = false`
- Yeniden doğrulama: eski `VerifiedAt` null'lanır, akış baştan (QR yenileme)
- Her işlem loglanır

### 4.4. Hesap Seviyesinde 2FA Toggle

`LoginAccount` üzerinde bir `TwoFactorEnabled` bayrağı vardır (default: false).

**Aktifleştirme şartı:**
- Kullanıcı "2FA'yı aktifleştir" demeden önce **en az bir `VerifiedAt != null && IsActive = true`** metoda sahip olmalı
- Bu şart sağlanmadan `TwoFactorEnabled = true` yapılamaz (domain validation)
- UI: "2FA'yı aktifleştir" butonu metot yoksa disabled görünür; tooltip: "Önce bir metot ekleyip doğrulayın"

**Pasifleştirme:** her zaman serbest (kullanıcı kapatmak isterse kapatabilir).

**Otomatik kapanma:** Kullanıcı son aktif ve doğrulanmış metodunu pasifleştirirse (`IsActive = false`) veya sildiyse:
- Sistem otomatik olarak `LoginAccount.TwoFactorEnabled = false` yapar
- `audit.security_events` → `TwoFactorAutoDisabled` event yazılır
- Sonraki login'de 2FA sorulmaz (zaten metot yok)

**Invariant:** `TwoFactorEnabled = true` → mutlaka **en az bir** verified + active metot var. Bu kural DB constraint ile değil (karmaşık), domain method invariant'ı ile korunur.

---

## 5. Şifre Sıfırlama

### 5.1. Akış

1. Kullanıcı "Şifremi Unuttum" — tek input (TCKN/VKN/YKN/Email/Cep)
2. Sistem Person + LoginAccount bulur
3. **Reset kanalı** — kullanıcının **doğrulanmış** iletişim kanallarından:
   - Sadece Email doğrulanmış → direkt email
   - Sadece SMS (cep) doğrulanmış → direkt SMS
   - İkisi de → kullanıcı seçer
   - Hiçbiri yoksa → "destek ekibine başvurun" + güvenlik olayı log
4. Reset token üretilir:
   - 32 byte cryptographically secure random
   - 15 dakika TTL
   - Tek kullanımlık
5. Kanala gönderilir:
   - Email: `/reset?token=xxx` linki
   - SMS: 6 haneli kod
6. Kullanıcı token'ı kullanır → yeni şifre
7. `PasswordHash` güncellenir
8. **Tüm aktif session'lar kapatılır** (güvenlik — Bölüm 7.5)
9. `audit.security_events` (PasswordReset)

### 5.2. password_reset_tokens Tablosu

| Alan | Tip |
|---|---|
| Token | string (hashlenir, plaintext saklanmaz) |
| LoginAccountId | FK |
| Channel | Email / Sms |
| ExpiresAt | datetimeoffset |
| UsedAt | datetimeoffset? |

---

## 6. Rol ve İzin Modeli

### 6.1. Permission — Statik (Kod İçinde)

Her izin kod içinde sabit:

```csharp
public static class Permissions
{
    public static class System
    {
        public const string Read = "system.read";
        public const string Manage = "system.manage";
        public const string Impersonate = "system.impersonate";
    }

    public static class Organization
    {
        public const string Read     = "organization.read";
        public const string Create   = "organization.create";
        public const string Update   = "organization.update";
        public const string Delete   = "organization.delete";
        public const string Analytics = "organization.analytics";
        public const string BankManage   = "organization.bank.manage";
        public const string BranchManage = "organization.branch.manage";
        public const string ContractSign = "organization.contract.sign";
    }

    public static class Site
    {
        public const string Read     = "site.read";
        public const string Create   = "site.create";
        public const string Update   = "site.update";
        public const string Delete   = "site.delete";
        public const string Analytics = "site.analytics";
        public const string StructureEdit  = "site.structure.edit";
        public const string DocumentUpload = "site.document.upload";
        public const string BankManage     = "site.bank.manage";
    }

    public static class Period
    {
        public const string Read   = "period.read";
        public const string Create = "period.create";
        public const string Update = "period.update";
        public const string Close  = "period.close";
    }

    public static class Person
    {
        public const string Read   = "person.read";
        public const string Create = "person.create";
        public const string Update = "person.update";
        // Delete yok
    }

    public static class ServiceContract
    {
        public const string Read      = "service_contract.read";
        public const string Create    = "service_contract.create";
        public const string Update    = "service_contract.update";
        public const string Terminate = "service_contract.terminate";
    }

    public static class Approval
    {
        public const string Approve      = "approval.approve";
        public const string PolicyManage = "approval.policy.manage";
    }

    // ... yeni feature geldikçe kod genişler
}
```

Adlandırma: `{resource}.{action}` — resource küçük harf + snake_case, action küçük harf.

### 6.2. Permission Senkronizasyonu

**permissions tablosu** kod'daki sabitlerle senkronize tutulur:

| Alan | Tip |
|---|---|
| Id | Guid |
| Key | string(100) UNIQUE (örn. `site.read`) |
| Resource | string(50) (örn. `Site`) |
| Action | string(50) (örn. `Read`) |
| Description | string(500) — Türkçe açıklama |
| DeprecatedAt | datetimeoffset? |

Deploy sırasında **migration çalıştırıcı** (Bölüm 10):
1. Kod'daki tüm `Permissions.*` sabitlerini reflection ile okur
2. Her biri DB'de var mı kontrol eder
3. Yoksa ekler
4. Kod'da artık yoksa (constant silindi) → `DeprecatedAt = now` (silinmez, sadece deprecate)

### 6.3. Role — Dinamik (DB Tablosu)

**roles tablosu:**

| Alan | Tip | Not |
|---|---|---|
| Id | Guid | |
| Name | string(100) | "Site Yöneticisi", "ABC Muhasebe Sorumlusu" |
| Scope | enum | System, Organization, Site, ServiceOrganization |
| IsSystem | bool | Seed rol mü kullanıcı rolü mü |
| OrganizationId | FK? | Org-özel rolse hangi org'a ait |
| ServiceOrganizationId | FK? | Servis org-özel rolse |
| CreatedBy | FK (LoginAccount) | Yaratan |
| Description | string(500) | |

**Scope × Owner Matrix:**

| Scope | IsSystem | OrganizationId | ServiceOrganizationId | Yaratıcı |
|---|---|---|---|---|
| System | true | null | null | seed (SystemAdmin, SystemSupport...) |
| Organization | true | null | null | seed (OrganizationManager, OrganizationStaff...) |
| Organization | false | org-abc | null | OrganizationManager (ABC'nin) özel rol |
| Site | true | null | null | seed (SiteManager, SiteStaff...) |
| Site | false | org-abc | null | OrganizationManager (ABC site'lerinde kullanılabilir) |
| ServiceOrganization | false | null | xyz | XYZ'nin yöneticisi |

### 6.4. Role-Permission Bağı

**role_permissions tablosu:**

| Alan | Tip |
|---|---|
| RoleId | FK |
| PermissionId | FK |
| GrantedAt | datetimeoffset |
| GrantedBy | FK (LoginAccount) |

### 6.5. Privilege Escalation Koruması

Yaratıcı bir role **sadece kendi sahip olduğu izinleri** atayabilir.

```
Algoritma (rol yaratırken):
  creator = currentUser
  creatorPermissions = ComputePermissions(creator)  // tüm membership'leri üzerinden birleşik set

  for each permissionId in newRole.permissions:
    if permissionId not in creatorPermissions:
      THROW PermissionEscalationException
```

**Örnek:**
- Ahmet OrgManager. Org-scope'ta izinleri var: `organization.*`, `site.read/create/update`, `period.*`, `person.*`
- Ahmet "ABC Muhasebe" adında yeni rol yaratıyor
- İzin seçerken **sadece kendi izinlerini** görür (dropdown filtrelenir)
- `system.manage` seçmeye çalışırsa hiç göremez (UI filter) — arka uçta da reddedilir (defense-in-depth)

### 6.6. Varsayılan Sistem Rolleri (Seed)

| Rol | Scope | İzinler |
|---|---|---|
| Sistem Yöneticisi | System | Tüm izinler (wildcard, `*`) |
| Sistem Destek | System | `system.read`, `person.read`, `organization.read`, `site.read` |
| Organizasyon Sahibi | Organization | `organization.*`, `site.*` (kendi org'unun siteleri), `person.*`, `period.*` |
| Organizasyon Yöneticisi | Organization | `organization.read/update`, `organization.bank.manage`, `organization.branch.manage`, `site.*`, `person.*`, `period.*` |
| Organizasyon Muhasebeci | Organization | `organization.read`, `site.read`, `period.read`, `site.analytics` |
| Site Yöneticisi | Site | `site.read/update`, `site.structure.edit`, `site.document.upload`, `period.*`, `person.read/create/update` |
| Site Teknisyen | Site | `site.read`, `period.read` |
| Servis Firması Yöneticisi | ServiceOrganization | Servis kullanıcıları yönetir |

Kod içinde seed dosyası (`RoleSeed.cs`).

---

## 7. Oturum Yönetimi (Session)

### 7.1. sessions Tablosu / Redis

Her aktif oturum hem DB'de (kalıcı kayıt için) hem Redis'te (hızlı erişim için):

```
Redis key: session:{sessionId}
Value (JSON):
{
  "sessionId": "01908a...",
  "loginAccountId": "...",
  "personId": "...",
  "deviceId": "cookie-abc...",     // cookie'de de var — karşılaştırılır
  "userAgent": "Chrome/131...",
  "ipAddress": "85.x.x.x",
  "loginAt": "...",
  "lastActivityAt": "...",
  "isMobile": false,
  "permissions": { ... },          // Bölüm 8
  "memberships": [ ... ],
  "securityRules": { ... }
}
```

DB'deki `sessions` tablosu **özet bilgi** tutar (SessionId, LoginAccountId, IP, DeviceId, LoginAt, ClosedAt, CloseReason). Aktif session'lar için Redis ana kaynak. 15 dk TTL.

### 7.2. Tek IP, Tek Cihaz, Tek Oturum

Her request'te middleware:

1. Cookie'den `sessionId` oku
2. `session:{sessionId}` Redis'ten çek — yoksa → login ekranına
3. **IP karşılaştır** (`ipAddress` Redis'te ↔ `HttpContext.Connection.RemoteIpAddress`)
   - Farklıysa → session kapatılır, redirect login, `SecurityEvent: IpChanged`
4. **DeviceId karşılaştır** (cookie ↔ Redis)
   - Farklıysa → session kapatılır (cookie çalınma), `SecurityEvent: DeviceMismatch`
5. **Kullanıcının başka aktif session'ı var mı?** Redis'te `user:{loginAccountId}:sessions` set'inde bir SessionId daha varsa → eski kapatılır.
6. `lastActivityAt = now` (TTL uzat)

### 7.3. Yeni Login Geldiğinde

```
Login başarılı
  ↓
Redis'te "user:{id}:sessions" set'inden eski sessionId'leri al
  ↓
Her eski session için:
  - Redis'ten "session:{oldId}" sil
  - DB sessions tablosunda CloseReason = "NewLoginFromDifferentLocation"
  - SignalR broadcast: "user:{id} kanalına { type: 'forceLogout', reason: '...' }"
  ↓
Eski browser:
  SignalR mesajını alır → localStorage temizle → redirect /login
  ↓
Yeni session Redis'e yazılır, cookie döner
```

### 7.4. IP Değişimi (Zero Tolerance)

Ayşe laptop'ta oturum açık, VPN açtı, IP değişti. Bir sonraki request'te middleware IP farkını yakalar:
- Session kapatılır
- `SecurityEvent: IpChanged` yazılır
- Client'a 401 döner → login ekranı
- SignalR gerekmiyor çünkü zaten o request'te yakalanıyor

### 7.5. Parola Değiştiğinde

Kullanıcı şifresini değiştirdi (reset veya gönüllü). Tüm session'lar kapatılır:

```
Redis: "user:{id}:sessions" set'indeki her sessionId için session:{x} silinir
SignalR: user:{id} kanalına forceLogout broadcast
Result: tüm cihazlarda otomatik logout
```

### 7.6. Device ID

Her yeni login'de:
1. Sunucu random 32-byte deviceId üretir
2. `Set-Cookie: sitehub_device=xxx; HttpOnly; Secure; SameSite=Strict; Max-Age=31536000`
3. Redis'teki session kaydına deviceId yazılır
4. Her request'te cookie ↔ Redis karşılaştırması yapılır

Cookie çalınırsa (XSS/CSRF) → session'a başka bir şeyden (tarayıcı) girilmeye çalışılırsa deviceId farklı olur → yakalanır.

### 7.7. Aynı Tarayıcıda Çoklu Tab + Farklı Bağlam (Madde 4 onay)

- Tek oturum (tek cookie), ama URL-bazlı context
- URL şeması **kod-tabanlı** (GUID değil) — çünkü GUID URL'de çirkin görünür, paylaşılamaz:

```
YANLIŞ:  /c/site/018f3a2c-7b5d-4e8f-9a2b-3c1d5e8a9f0b/units
DOĞRU:   /c/site/234567/units
```

**URL şeması (genel):**

| URL | Ne gösterir |
|---|---|
| `/` | Home (context seçimi) |
| `/c/system/...` | System yönetim (SystemAdmin) |
| `/c/org/{orgCode}/...` | Org yönetim paneli (6 haneli kod) |
| `/c/site/{siteCode}/...` | Site yönetim paneli (6 haneli kod) |
| `/c/site/{siteCode}/units/{unitCode}/...` | BB detay (7 haneli kod) |
| `/c/site/{siteCode}/units/{unitCode}/periods/{periodId}` | Dönem detay (9 haneli banka referansı) |
| `/profile` | Kendi profilim (context-bağımsız) |
| `/login`, `/logout` | Auth sayfaları |

**Kod üretimi (ADR-0012'de detay):**
- Organization: 6 hane (100001-999999), rastgele, all-time unique
- Site: 6 hane (100001-999999), rastgele, all-time unique
- BB (Unit): 7 hane (1000001-9999999), rastgele, all-time unique
- BB Period: 9 hane (111111111-999999999), rastgele, all-time unique, banka tahsilat referansı

Tümü **rastgele** (tahmin edilemez) + **soft-delete dahil unique** (IgnoreQueryFilters ile check) + **retry logic** (çakışma olursa yeniden üretir).

**Güvenlik:** URL'de kod tahmin edilebilir sıralı olmayabilir ama yine de `/c/site/234567/` yazan kullanıcı bu siteye erişecek yetkiye sahip değilse → **authorization middleware 403 döner**. Defense in depth — URL gizliliği güvenlik stratejisi değil, erişim kontrolü güvenlik stratejisidir.

- `IActiveContextAccessor` circuit-scoped (Blazor Server'da)
- Kullanıcının **izinleri** aynı (cache'te), ama her tab kendi context'inde hangi izinleri **kullanıyor** farklı

### 7.8. Tarayıcı Kapandığında Logout (Madde 5)

- Authentication cookie **session cookie** (Max-Age yok, Expires yok)
- Tarayıcı kapanınca otomatik silinir
- Sunucuya haber gelmez (logout event yazılamaz — teknik gerçek)
- Redis TTL (15 dk) dolunca session otomatik silinir — arka uçta da temizlenir

Mobile native app (v2) için **refresh token** mekanizması eklenecek — persistent.

---

## 8. Permission Hesaplama ve Cache

### 8.1. ComputePermissions Algoritması

Bir kullanıcının o anki izinleri = memberships üzerinden toplama:

```
ComputePermissions(loginAccountId):
  memberships = SELECT * FROM memberships
                WHERE login_account_id = ? AND is_active
                  AND (valid_from IS NULL OR valid_from <= now)
                  AND (valid_to   IS NULL OR valid_to   >= now)

  permissions = new Dictionary<ContextKey, HashSet<string>>()

  for each m in memberships:
    role = SELECT r FROM roles WHERE id = m.role_id
    rolePermissions = SELECT p.key FROM role_permissions rp
                      JOIN permissions p ON p.id = rp.permission_id
                      WHERE rp.role_id = role.id

    contextKey = (m.context_type, m.context_id)
    if not in permissions: permissions[contextKey] = {}
    permissions[contextKey].AddAll(rolePermissions)

  // Malik/Hissedar/Kiracı implicit rolleri
  ownedUnits = SELECT * FROM unit_period_shareholders
               WHERE person_id = person(loginAccountId).id
                 AND now BETWEEN start AND end

  for each unit in ownedUnits:
    permissions[(Unit, unit.id)] = { "period.read" }  // malik kendi BB'sini okur

  tenantUnits = ... (kiracılar için)
  serviceContracts = ... (Bölüm 6.7)

  return permissions
```

### 8.2. Redis Cache

Key: `user:permissions:{loginAccountId}`
TTL: 15 dakika

Her request'te:
1. Middleware cache'ten oku
2. Cache miss → DB'den hesapla + cache'e yaz
3. Endpoint/Component bu cache'ten izin kontrolü yapar

### 8.3. Anlık Invalidation (Madde 6, 7)

Permission'lara etki edecek herhangi bir değişiklik:
- Rol oluşturma/güncelleme/silme
- Role-permission bağı eklenme/kaldırılma
- Membership yaratma/güncelleme/silme
- Person'un BB/Dönem ilişkisi değişmesi
- Servis sözleşmesi eklenme/bitme
- LoginAccount aktif/pasif değişimi
- IP whitelist / schedule değişimi

**İnvalidation akışı:**

```csharp
public class PermissionInvalidationService
{
    public async Task OnRoleChanged(Guid roleId)
    {
        var affectedUsers = await GetUsersUsingRole(roleId);
        foreach (var userId in affectedUsers)
        {
            await _redis.KeyDeleteAsync($"user:permissions:{userId}");
            await _hub.Clients.Group($"user:{userId}")
                .SendAsync("permissionUpdated", new { reason = "RoleChanged" });
        }
    }

    public async Task OnMembershipChanged(Guid membershipId)
    {
        var userId = await GetUserFromMembership(membershipId);
        await _redis.KeyDeleteAsync($"user:permissions:{userId}");
        await _hub.Clients.Group($"user:{userId}")
            .SendAsync("permissionUpdated", new { reason = "MembershipChanged" });
    }

    // ... diğer event'ler
}
```

### 8.4. Client Tepkisi

Browser'da `permissionUpdated` event'i geldiğinde:

```javascript
hub.on("permissionUpdated", async () => {
  // 1. Yeni permission cache'ini Redis'ten çek (bir /api/me endpoint'i ile)
  const newPerms = await fetch("/api/me/permissions");

  // 2. Local JS state'i güncelle
  authStore.setPermissions(newPerms);

  // 3. Blazor component'lerini yeniden render et
  // StateHasChanged() otomatik çalışır — AuthorizePermission component'leri yeniden değerlendirilir
});
```

**UI davranışı (Madde 6 onay):**
- Sayfa üzerindeki veri kalır
- `<AuthorizePermission Required="site.delete">` içinde gösterilmiş butonlar anında pasifleşir/kaybolur
- Kullanıcı artık yetkisi olmayan bir aksiyon yapamaya çalışırsa endpoint-seviyesinde 403 döner (belt-and-suspenders)
- Navigation (yeni sayfa açma) her zaman permission kontrolünden geçer

---

## 9. Membership (Üyelik)

Bir `LoginAccount`'un hangi bağlamda hangi rolde olduğunu tutar.

**memberships tablosu:**

| Alan | Tip |
|---|---|
| Id | Guid |
| LoginAccountId | FK |
| ContextType | enum (System, Organization, Branch, Site, ServiceOrganization) |
| ContextId | Guid? (System için null) |
| RoleId | FK |
| ValidFrom | datetimeoffset? |
| ValidTo | datetimeoffset? |
| IsActive | bool |
| Audit alanları | ... |

**Çoklu rol:** Aynı kullanıcının birden fazla Membership olabilir.

**Malik/Hissedar/Kiracı burada YOK** — BB Period (Dönem) tablosundan türer, 8.1'deki algoritma ile hesaplanır. Böylece:
- Dönem kapandığında malik yetkisini otomatik kaybeder (manuel silmeye gerek yok)
- Tarihsel sorgular temiz: "2 yıl önce bu BB'nin maliki kimdi?" — period tablosuna bak

---

## 10. Audit Security Events

Tüm kimlik/yetki olayları ayrı bir tabloda:

**audit.security_events tablosu:**

| Alan | Tip |
|---|---|
| Id | Guid |
| Timestamp | datetimeoffset |
| EventType | enum (aşağıda) |
| LoginAccountId | FK? (anon login için null) |
| PersonNationalId | string? (denormalize) |
| IpAddress | string |
| UserAgent | string |
| DeviceId | string? |
| SessionId | string? |
| CorrelationId | string |
| Details | JSONB |
| Success | bool |

**EventType enum değerleri:**

| Kategori | Değer | Ne zaman |
|---|---|---|
| Login | LoginSuccess | Başarılı giriş |
| Login | LoginFail_BadCredentials | Yanlış şifre |
| Login | LoginFail_UnknownUser | Var olmayan hesap |
| Login | LoginFail_InvalidFormat | Hatalı input formatı |
| Kural | AccountInactive | İsActive = false |
| Kural | AccountOutOfValidity | ValidFrom/To dışında |
| Kural | IpNotAllowed | Whitelist dışı |
| Kural | ScheduleBlocked | Saat kuralı |
| Kural | AccountLocked | Çok fazla başarısız deneme |
| Oturum | IpChanged | Session IP değişti |
| Oturum | DeviceMismatch | Cookie deviceId uyumsuz |
| Oturum | ForcedLogout | Yeni login geldi, eski kapatıldı |
| Oturum | PasswordResetForcedLogout | Şifre değişti, tümü kapatıldı |
| Şifre | PasswordChanged | Kullanıcı kendi şifresini değiştirdi |
| Şifre | PasswordReset | Reset token ile değiştirdi |
| 2FA | TwoFactorEnabled | Metot eklendi + doğrulandı |
| 2FA | TwoFactorDisabled | Kullanıcı kendi 2FA'sını kapattı |
| 2FA | TwoFactorAutoDisabled | Son metot pasifleştiği için otomatik kapandı |
| 2FA | TwoFactorVerified | Login sırasında doğruladı |
| 2FA | TwoFactorFailed | Yanlış kod |
| 2FA | TwoFactorEnableBlocked | Doğrulanmış metot olmadan aktifleştirme denendi |
| Yetki | RoleCreated | Yeni rol yaratıldı |
| Yetki | RolePermissionGranted | |
| Yetki | RolePermissionRevoked | |
| Yetki | MembershipCreated | |
| Yetki | MembershipRevoked | |
| Yetki | PermissionEscalationAttempt | Kendi izninin üstünde atama denedi |
| Destek | ImpersonationStart | Sistem destek başka kullanıcı oluyor |
| Destek | ImpersonationEnd | |

**Retention:** 10 yıl (ADR-0006).

**Indexleme:**
- `(timestamp)` — son olaylar
- `(login_account_id, timestamp)` — kullanıcı başına tarihçe
- `(event_type, timestamp)` — tip başına filtreleme
- `(ip_address, timestamp)` — IP başına tarihçe (şüpheli IP incelemesi)

---

## 11. Implementation Sırası

1. **Person + NationalId** value object + validation + test
2. **Address hierarchy seed** (Country/Region/Province/District/Neighborhood) — CSV'den
3. **LoginAccount** + ASP.NET Core Identity entegrasyonu
4. **Permission sabitleri** + senkronizasyon migration
5. **Role + RolePermission** tabloları + seed (sistem rolleri)
6. **Membership** tablosu + IActiveContextAccessor implementasyonu
7. **Login UI** (tek input + otomatik algılama)
8. **Login kural kontrolü** (IsActive, ValidFrom/To, IP, Schedule, Lockout)
9. **Session yönetimi** (Redis + DB + middleware)
10. **SignalR hub** + client event handler
11. **Permission cache service** + invalidation
12. **2FA metotları** (Email, SMS, TotpApp)
13. **Password reset** akışı
14. **audit.security_events** + interceptor
15. **UI:** role yönetim, permission atama, membership ekleme, 2FA profil

Her adımda: domain test + integration test + security test.

---

## 12. Alternatifler (Reddedilen)

### 12.1. Permission DB-Dinamik
**Reddedildi.** Permission'lar feature-bound (kod yazarken biz tanımlıyoruz). DB-dinamik olursa:
- Kullanıcı "hayali" bir izin yaratır (örn. `foo.bar`) — hiçbir kod bunu kontrol etmez
- Guvenlik delikleri (kontrolsüz yetki)
- Yönetilmesi zor

Statik + senkronize approach güvenli.

### 12.2. Tek "User" Tablosu
**Reddedildi.** KVKK + veri minimizasyon. Hissedarların login alanları (şifre, 2FA) tutulursa gereksiz PII.

### 12.3. Malik/Kiracı = Membership Rolü
**Reddedildi.** Membership statik atanır; BB dönem ilişkileri otomatik. İki yaklaşım senkron tutulamaz — birinde değişiklik diğerini unutur.

### 12.4. Session → JWT
**Reddedildi.** JWT stateless, anlık iptal zor. Tek oturum/tek IP kuralları enforcement için server-side session zorunlu. Redis hem hızlı hem iptal edilebilir.

### 12.5. Redis TTL Uzun (örn. 24 saat)
**Reddedildi.** 15 dakika + anında invalidate. Uzun TTL = izin değişiklikleri geç yansır.

---

## 13. Açık Konular (v2+)

- **Mobil native app + refresh token** — MVP'de yok
- **Biometric 2FA** — Push notification (FaceID/TouchID)
- **Anomaly detection** — Alışılmadık login pattern → ek doğrulama (saat, IP, cihaz)
- **SSO (SAML/OAuth2)** — Büyük organizasyonlar için
- **Custom Permission (DB-based)** — Çok esnek müşteri isteği olursa
- **Impersonation audit** — Destek ekibi "başka kullanıcı" oluyorsa sıkı loglama
- **KVKK pseudonymize** — Person silme hakkı talebi için (PII şifrelenir, tarihçe kalır)

---

## 14. Karar Kaydı

Bu ADR'ı onaylayan: _________________
Tarih: _________________
Notlar: _________________
