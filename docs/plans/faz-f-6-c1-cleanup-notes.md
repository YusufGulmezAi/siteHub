# Faz F.6 C.1 Cleanup — Flat /api/sites Endpoint Geri Alındı

**Tarih:** 2026-04-22
**Scope:** Kullanıcı mimari kararı — Site işlemleri daima Organization context altında.

## Neden Geri Alındı?

F.6 C.1'de `GET /api/sites` flat endpoint'i eklenmişti. Ama UI mimarisi netleşince:
- Kullanıcı önce Organization seçer, sonra o org'un site'larına iner
- Nav menüsünde "Siteler" girişi YOK, sadece "Organizasyonlar" var
- Flat endpoint'e UI'dan erişim yolu olmayacak
- Ölü kod bırakmak gereksiz karmaşa

**Karar:** Flat endpoint backend'i temizle. `OrganizationName` alanı nested
endpoint'lerde **korundu** (breadcrumb ve başlık için gerekli).

## Değişen 3 Dosya + Silinen 2 Dosya

**Değişenler:**
| Dosya | Değişiklik |
|---|---|
| `ManagementPortal/Endpoints/Sites/SiteEndpoints.cs` | `byId.MapGet("/")` + `FlatListQueryParams` + `GetAllAsync` method silindi |
| `ManagementPortal/Services/Api/ISitesApi.cs` | `GetAllAsync` signature silindi |
| `ManagementPortal/Services/Api/SitesApi.cs` | `GetAllAsync` implementation silindi |

**Silinenler (git rm):**
| Dosya | Sebep |
|---|---|
| `Application/Features/Sites/GetAllSitesQuery.cs` | Flat query + handler (yeniydi, UI kullanmayacak) |
| `Application/Features/Sites/SiteMappings.cs` | Boş placeholder (şimdi gerek yok, ADR-0017'de gerekirse tekrar) |

## KORUNAN (C.1'den kalan iyi şeyler)

**`Contracts/Sites/SiteDtos.cs`** — `OrganizationName` alanı DOKUNULMADI:
- `SiteListItemDto` 13 alanla kalır (OrganizationName sonuç olarak lazım)
- `SiteDetailDto` 16 alanla kalır
- Breadcrumb "Organizasyonlar > [Org Adı] > Siteler" için gerekli

**`Application/Features/Sites/GetSitesQuery.cs`** — DOKUNULMADI:
- Explicit Join (Sites.Join(Organizations)) kalır
- Nested endpoint'te `OrganizationName` dolu gelir

**`Application/Features/Sites/GetSiteByIdQuery.cs`** — DOKUNULMADI:
- Aynı Join pattern'i korundu

## Uygulama

```powershell
cd D:\Projects\sitehub

# 1. Zip'i uygula (modified 3 dosya)
Unblock-File C:\Users\<sen>\Downloads\sitehub-f6-c1-cleanup.zip
Expand-Archive -Path "C:\Users\<sen>\Downloads\sitehub-f6-c1-cleanup.zip" `
    -DestinationPath "D:\Projects\sitehub\" -Force

# 2. Silinecek 2 dosyayı MANUEL sil
Remove-Item src\SiteHub.Application\Features\Sites\GetAllSitesQuery.cs -Force
Remove-Item src\SiteHub.Application\Features\Sites\SiteMappings.cs -Force

# 3. Build + test
dotnet build
dotnet test --no-build
```

**Beklenen:**
- Build temiz (10 proje yeşil)
- 146 test hâlâ yeşil (sadece kullanılmayan kod siliniyor, logic etkilenmez)

## Commit Önerisi

```
Faz F.6 C.1 cleanup: Flat /api/sites endpoint geri alindi

Mimari karar: Site islemleri daima Organization context altinda yapilir.
Kullanici onceden Organization secer, o org'un site'larina iner.
Nav menude 'Siteler' girisi yok, sadece 'Organizasyonlar' var.

Silinen:
- Application/Features/Sites/GetAllSitesQuery.cs (flat query + handler)
- Application/Features/Sites/SiteMappings.cs (bos placeholder, gerek kalmadi)

Degisen (3 dosya):
- SiteEndpoints.cs: flat GET /api/sites + FlatListQueryParams kaldirildi
- ISitesApi.cs: GetAllAsync method signature kaldirildi
- SitesApi.cs: GetAllAsync implementation kaldirildi

Korunan (onemli):
- Contracts/Sites/SiteDtos.cs: OrganizationName alani STATUS KALDI
- GetSitesQuery.cs + GetSiteByIdQuery.cs: Explicit Join'ler KORUNDU
  (breadcrumb + baslik 'ABC Yonetim > Siteler' icin gerekli)

Gelecek-uyumluluk:
- Ileride rapor/export ihtiyaci dogarsa flat endpoint git revert ile
  geri eklenir
- ADR-0017 Manuel Mapping ADR'si C sonunda yazilacak, SiteMappings.cs
  gerekirse yeniden eklenir

Test: 146 test yesil, build temiz.
```

---

**Bir sonraki adım: F.6 C.4 Site List (nested)**

Bu commit push'landıktan sonra C.4 Site List delta'sı hazırlanır:
- `/organizations/{orgId}/sites` sayfası
- Organization List'e "Siteler" action ikonu
- MudBlazor breadcrumb workaround + Org adı başlıkta
