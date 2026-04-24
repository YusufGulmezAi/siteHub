namespace SiteHub.ManagementPortal.Components.Navigation;

using MudBlazor;

/// <summary>
/// Yönetici portalının sol menü ağacı.
///
/// <para><b>KURAL:</b> Bu dosyaya yalnızca <b>çalışan ve sayfası hazır olan</b> linkler
/// eklenir. Planlanan ama henüz yapılmamış ekranlar için
/// <c>docs/ROADMAP.md</c> kullanılır.</para>
///
/// <para>Yeni bir faz tamamlandığında ilgili menü öğeleri buraya eklenir;
/// ROADMAP.md'deki durum ✅ olarak güncellenir.</para>
///
/// <para>Hesap ve güvenlik ayarları sol menüde değil — sağ üst avatar menüsündedir
/// (<c>MainLayout.razor</c>).</para>
///
/// <para><b>F.6 Kategori A (Madde 4):</b> "Tenant Yönetimi" parent grubu kaldırıldı.
/// "Siteler" de kaldırıldı — siteler zaten Organization altında nested (listeye
/// /organizations/{id}/sites üzerinden erişiliyor). "Organizasyonlar" menü ismi
/// "Yönetim Firmaları" olarak güncellendi.</para>
/// </summary>
public static class MenuTree
{
    public static readonly IReadOnlyList<MenuItem> Items =
    [
        new() { Title = "Yönetim Firmaları", Href = "/organizations",
                Icon = Icons.Material.Filled.Business },
    ];
}
