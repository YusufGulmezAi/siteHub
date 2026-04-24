# F.6 Madde 5 — Breadcrumb Standardı

**Tarih:** 2026-04-24
**Scope:** 1 yeni component, 3 sayfada refactor.

## Amaç

Portal'daki breadcrumb pattern'i (MudStack + MudLink + MudIcon + MudText) 3 sayfada birebir tekrar ediyordu. Shared component'a çevrildi. Ayrıca:

- Font büyütüldü: body2 (14px) → subtitle1 (16px, 500 weight) — daha belirgin
- Altına 1px divider çizgi eklendi — minimal ayırıcı, sayfanın üst alanını body'den ayırır
- "Organizasyonlar" → "Yönetim Firmaları" (Site Detail ve List'te — Kategori A ile tutarlılık)

## Mimari

**API:** Array parameter. Tek component, 1-2 satırla kullanım.

```razor
<SiteHubBreadcrumb Items="@_breadcrumbItems" />

@code {
    private SiteHubBreadcrumb.BreadcrumbItem[] _breadcrumbItems =>
        new[]
        {
            new SiteHubBreadcrumb.BreadcrumbItem("Yönetim Firmaları", "/organizations"),
            new SiteHubBreadcrumb.BreadcrumbItem("ABC İnşaat", null) // son item, tıklanamaz
        };
}
```

Son item otomatik "current" sayılır (`Href == null` → gri + tıklanamaz). Boş veya null array → component hiç render etmez.

## Dosya Listesi

### Yeni (1)
- `Components/Shared/SiteHubBreadcrumb.razor` — shared component (98 satır)

### Değişen (3)
- `Components/Pages/Organizations/Detail.razor` — 2-seviye breadcrumb
- `Components/Pages/Sites/Detail.razor` — 4-seviye breadcrumb + "Organizasyonlar" → "Yönetim Firmaları"
- `Components/Pages/Sites/List.razor` — 3-seviye breadcrumb + "Organizasyonlar" → "Yönetim Firmaları"

## Uygulama

```powershell
cd D:\Projects\sitehub
Unblock-File C:\Users\Yusuf\Downloads\sitehub-f6-m5.zip
Expand-Archive -Path "C:\Users\Yusuf\Downloads\sitehub-f6-m5.zip" -DestinationPath "D:\Projects\sitehub\" -Force
dotnet build
```

## Smoke Test

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --project src\SiteHub.ManagementPortal
```

### Test 1 — Organizations/Detail
1. `/organizations` → bir satırı tıkla → Detail
2. Breadcrumb: **Yönetim Firmaları › [Firma Adı]**
3. Font subtitle1, altında 1px gri çizgi
4. "Yönetim Firmaları" tıklanabilir (mavi, underline on hover)
5. Firma adı gri, tıklanamaz

### Test 2 — Sites/List
1. `/organizations` → bir org'un Apartment ikonu
2. Breadcrumb: **Yönetim Firmaları › [Firma] › Siteler**
3. "Siteler" gri, tıklanamaz

### Test 3 — Sites/Detail
1. Site List'te satır tıkla
2. Breadcrumb: **Yönetim Firmaları › [Firma] › Siteler › [Site]**
3. Font büyük, alt çizgi var

### Test 4 — Create mode'lar
- `/organizations/new` → Breadcrumb: **Yönetim Firmaları › Yeni**
- `/organizations/{id}/sites/new` → Breadcrumb: **Yönetim Firmaları › [Firma] › Siteler › Yeni**

## Gelecek Sayfalar

Yeni bir sayfa eklerken breadcrumb yapmak için:

```razor
@using SiteHub.ManagementPortal.Components.Shared

<SiteHubBreadcrumb Items="@_breadcrumbItems" />

@code {
    private SiteHubBreadcrumb.BreadcrumbItem[] _breadcrumbItems =>
        new[]
        {
            new SiteHubBreadcrumb.BreadcrumbItem("Başlık1", "/url1"),
            new SiteHubBreadcrumb.BreadcrumbItem("Başlık2", null)
        };
}
```

## Commit Önerisi

```
Faz F.6 Madde 5: Breadcrumb standardi (shared component)

Uc farkli sayfada birebir tekrar eden breadcrumb pattern'i tek shared
component'a toplandi. Tutarli gorunum + kod azaltma.

Yeni:
- Components/Shared/SiteHubBreadcrumb.razor
  Array parameter API: <SiteHubBreadcrumb Items="@items" />
  BreadcrumbItem(Text, Href?) record. Href null -> son item, tiklanamaz.
  Font: Typo.subtitle1 (16px, 500 weight) - body2'den biraz buyuk.
  Alt cizgi: 1px var(--mud-palette-divider) - minimal ayirici.
  Empty/null items -> hic render etmez.

Degisen:
- Components/Pages/Organizations/Detail.razor
  2-seviye breadcrumb (Yonetim Firmalari > [Firma])
- Components/Pages/Sites/Detail.razor
  4-seviye (Yonetim Firmalari > [Firma] > Siteler > [Site])
  + "Organizasyonlar" -> "Yonetim Firmalari" (Kategori A tutarlilik)
- Components/Pages/Sites/List.razor
  3-seviye (Yonetim Firmalari > [Firma] > Siteler)
  + ayni metin duzeltmesi

Test: Build temiz.
Smoke: 3 sayfada breadcrumb yeni gorunumde, son item gri, diger item'lar
       tiklanabilir mavi underline.
```
