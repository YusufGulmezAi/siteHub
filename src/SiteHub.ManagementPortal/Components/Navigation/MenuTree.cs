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
/// </summary>
public static class MenuTree
{
    public static readonly IReadOnlyList<MenuItem> Items =
    [
        new() { Title = "Ana Sayfa", Href = "/", Icon = Icons.Material.Filled.Dashboard },

        // Faz E tamamlanınca buraya "Organizasyon" menüsü eklenecek:
        // new()
        // {
        //     Title = "Organizasyon",
        //     Icon = Icons.Material.Filled.AccountTree,
        //     Children =
        //     [
        //         new() { Title = "Firmalar", Href = "/firms",
        //                 Icon = Icons.Material.Filled.Business,
        //                 RequiredPermission = "firm.view" },
        //     ]
        // },
    ];
}
