# Faz F.6 C.2 — Permission Altyapısı (Context-Aware MVP)

**Tarih:** 2026-04-23
**Scope:** 7 yeni dosya + 2 değişen dosya. UI Organizations/List.razor manuel snippet ile.

## Hedef

Kullanıcının hangi context'te hangi permission'lara sahip olduğunu runtime'da sorgulamak, UI'da permission-aware visibility sağlamak.

## Mimari — Hibrit B-Cascade

- **System scope** → `PermissionSet.ByContext["System"]` tek entry. Has() bu key'i önce kontrol eder (short-circuit), bulursa hemen true döner.
- **Organization scope** → Login zamanında o org'un **tüm site'larına** permission cascade edilir.
- **Site scope** → Sadece o site'ın entry'si.
- Runtime kontrol O(1) dictionary lookup. DB call yok.

## Dosya Listesi

### Yeni (7)

| Dosya | Açıklama |
|---|---|
| `Domain/Identity/Sessions/PermissionSet.cs` | Value object — ByContext dictionary + Has() |
| `Application/Abstractions/Authorization/IPermissionComputer.cs` | Login'de hesaplama abstraction |
| `Application/Features/Authentication/PermissionComputer.cs` | EF Core implementasyon, cascade |
| `Application/Abstractions/Authorization/ICurrentUserPermissionService.cs` | Runtime sorgu abstraction |
| `ManagementPortal/Services/Authorization/CurrentUserPermissionService.cs` | HttpContext/Redis okur, scoped cache |
| `ManagementPortal/Components/Shared/Authorization/HasPermission.razor` | UI wrapper bileşeni |
| `docs/plans/faz-f-6-c2-permission-notes.md` | Bu dosya |

### Değişen (2)

| Dosya | Değişiklik |
|---|---|
| `Domain/Identity/Sessions/Session.cs` | `PermissionSet Permissions` alanı + Create factory imzası |
| `Application/Features/Authentication/Login/LoginHandler.cs` | IPermissionComputer çağrısı, Session.Create'e verilir |

## DI Registrations (senin eklemen gereken)

**`SiteHub.Application` assembly'de (ne olursa onun DI setup'ı):**

```csharp
services.AddScoped<IPermissionComputer, PermissionComputer>();
```

**`SiteHub.ManagementPortal` Program.cs'de:**

```csharp
services.AddHttpContextAccessor();  // zaten varsa dokunma
services.AddScoped<ICurrentUserPermissionService, CurrentUserPermissionService>();
```

Eğer Application'da `AddApplicationServices()` gibi extension varsa `IPermissionComputer`'ı orada kaydetmen idealdir.

## Organizations/List.razor'a Örnek Entegrasyon (manuel)

Kendi List.razor'unun mevcut haline şu değişiklikleri uygula:

### 1. Yeni using'ler ekle (en üste)

```razor
@using SiteHub.Domain.Identity.Authorization
@using SiteHub.Shared.Authorization
@using SiteHub.ManagementPortal.Components.Shared.Authorization
```

### 2. "+ Yeni" butonu (varsa) — organization.create permission

Mevcut `+ Yeni Organizasyon` butonunu (varsa) sar:

```razor
<HasPermission Required="@Permissions.Organization.Create"
               ContextType="MembershipContextType.System">
    <MudButton Variant="Variant.Filled"
               Color="Color.Primary"
               StartIcon="@Icons.Material.Filled.Add"
               OnClick="NavigateToCreate">
        Yeni Organizasyon
    </MudButton>
</HasPermission>
```

**Not:** `organization.create` System scope'ta verilmiş — yeni organizasyon yaratmak SystemAdmin işi.

### 3. Düzenle ikonu — organization.update + bu organizasyon için

İşlemler kolonundaki "Düzenle" (kalem) ikonunu sar:

```razor
<HasPermission Required="@Permissions.Organization.Update"
               ContextType="MembershipContextType.Organization"
               ContextId="@context.Item.Id">
    <MudIconButton Icon="@Icons.Material.Filled.Edit"
                   Size="Size.Small"
                   OnClick="@(() => NavigateToEdit(context.Item.Id))"
                   Title="Düzenle" />
</HasPermission>
```

### 4. Siteler (Apartment) ikonu — site.read izni yeterli

```razor
<HasPermission Required="@Permissions.Site.Read"
               ContextType="MembershipContextType.Organization"
               ContextId="@context.Item.Id">
    <MudIconButton Icon="@Icons.Material.Filled.Apartment"
                   Size="Size.Small"
                   OnClick="@(() => NavigateToSites(context.Item.Id))"
                   Title="Siteler" />
</HasPermission>
```

### 5. Detay (göz) ikonu — organization.read

```razor
<HasPermission Required="@Permissions.Organization.Read"
               ContextType="MembershipContextType.Organization"
               ContextId="@context.Item.Id">
    <MudIconButton Icon="@Icons.Material.Filled.Visibility"
                   Size="Size.Small"
                   OnClick="@(() => NavigateToDetail(context.Item.Id))"
                   Title="Detay" />
</HasPermission>
```

## Uygulama

```powershell
cd D:\Projects\sitehub

Unblock-File C:\Users\<sen>\Downloads\sitehub-f6-c2.zip
Expand-Archive -Path "C:\Users\<sen>\Downloads\sitehub-f6-c2.zip" `
    -DestinationPath "D:\Projects\sitehub\" -Force

# DI registration'ları + Organizations/List.razor snippet'lerini elle ekle
dotnet build
dotnet test --no-build
```

Build temiz olmalı. 146 test yeşil (Session.Create imzası değişti ama testlerde kullanılmıyorsa etkilenmez; etkilenirse testleri güncelle).

## Smoke Test

### Test 1 — Admin login
1. Portal'ı başlat: `dotnet run --project src\SiteHub.ManagementPortal`
2. admin@sitehub.local / Admin123! ile giriş
3. Backend log'unda şu satırı göreceksin:
   ```
   PermissionComputer: account=..., context sayısı=X, toplam permission=Y
   ```
4. Log'da şu da görülmeli:
   ```
   Login başarılı: person=..., permContexts=X, ...
   ```

### Test 2 — Admin'in tüm butonları gör
1. `/organizations` sayfası
2. Her satırda 3 ikon (göz, kalem, apartment) görünmeli
3. "+ Yeni Organizasyon" butonu görünmeli
4. Hiçbiri gizlenmemeli (SystemAdmin = wildcard = her permission)

### Test 3 — Redis session'da PermissionSet
Bu test opsiyonel (Redis CLI gerekli):
```
redis-cli
> KEYS session:*
> GET session:<your-session-id>
```
JSON'da `permissions.byContext.System` altında tüm permission key'lerini görmelisin.

### Test 4 — Permission olmayan kullanıcı (manuel senaryo)
1. Admin ile yeni bir user oluştur (UI henüz yok — seed veya DB direkt)
2. Bu kullanıcıya hiç membership verme (veya sadece Site scope'ta read verb)
3. Bu kullanıcı ile giriş yap
4. Organizations listesindeki "Düzenle" ikonları gizli olmalı

Test 4 şu an manuel (user management UI yok). Smoke test 1-3 yeterli.

## Kapsam Dışı (sonraki)

- Redis cache invalidation (şimdilik 15 dk TTL ile yenileniyor)
- SignalR permission update broadcast
- Policy-based authorization `[Authorize(Policy = "site.update")]`
- `/api/me/permissions` endpoint
- Implicit permissions (malik/kiracı için `period.read`)
- Site Detail + diğer sayfalarda wrapping (C.5 seansında)

## Commit Önerisi

```
Faz F.6 C.2: Permission altyapisi (context-aware, hibrit B-Cascade)

Kullanicinin context bazinda hangi permission'lara sahip oldugunu
runtime'da sorgulama. Login'de hesaplanir, Redis session icinde
tutulur, O(1) dictionary lookup ile UI'da kullanilir.

Mimari:
- System scope: ByContext['System'] tek entry, Has() short-circuit
- Organization scope: Login zamaninda o org'un tum site'larina cascade
- Site scope: Sadece o site entry'si
- SystemAdmin icin Redis sismiyor (tek 'System' entry)

Yeni (7):
- Domain/Identity/Sessions/PermissionSet.cs
- Application/Abstractions/Authorization/IPermissionComputer.cs
- Application/Features/Authentication/PermissionComputer.cs
  - Memberships + Role + RolePermission join
  - Organization -> Site cascade (login'de expand)
- Application/Abstractions/Authorization/ICurrentUserPermissionService.cs
- ManagementPortal/Services/Authorization/CurrentUserPermissionService.cs
  - HttpContext.Items oncelikli, fallback ISessionStore
  - Scoped cache (circuit basina bir Redis call)
- ManagementPortal/Components/Shared/Authorization/HasPermission.razor
  - Required + ContextType + ContextId parametreleri

Degisen (2):
- Domain/Identity/Sessions/Session.cs
  - PermissionSet Permissions alani eklendi (required)
- Application/Features/Authentication/Login/LoginHandler.cs
  - IPermissionComputer DI
  - ComputeAsync cagrisi sonrasi Session.Create'e verilir

Kapsam disi:
- Redis invalidation (15 dk TTL yeterli)
- SignalR broadcast
- Policy-based authorization
- Implicit permissions (malik/kiraci)

Organizations/List.razor'a wrap'lar notes'taki snippet'lerle manuel
eklendi (hotfix dongusu sonrasi dosyanin son halini bozmamak icin).

Test: Build temiz, 146 test yesil.
Smoke: Admin login -> tum butonlar gorunur, backend log'unda
       PermissionComputer context sayisi X log'u.

PROJECT_STATE §5.5'e eklenecek Ogrenim: PermissionSet JSON roundtrip
icin Dictionary<string, HashSet<string>> + record yerine class kullanildi
(record init'in JSON deserialize'la sorunlu olmasi engellendi).
```
