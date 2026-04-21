namespace SiteHub.ManagementPortal.Components.Navigation;

using MudBlazor;

/// <summary>
/// Yönetici portalının tüm menü ağacı.
///
/// Bu ağaç şu an için statik tanımlıdır; ileride aktif context'e ve kullanıcının
/// permission'larına göre filtrelenecek. `RequiredPermission` alanı o amaçla
/// doldurulmuştur.
///
/// Yeni menü eklerken yetki kontrolü için permission kodu mutlaka belirtilmeli.
/// </summary>
public static class MenuTree
{
    public static readonly IReadOnlyList<MenuItem> Items =
    [
        new() { Title = "Ana Sayfa", Href = "/", Icon = Icons.Material.Filled.Dashboard },

        new()
        {
            Title = "Organizasyon",
            Icon = Icons.Material.Filled.AccountTree,
            Children =
            [
                new() { Title = "Kiracılar (Firmalar)", Href = "/firms",
                        Icon = Icons.Material.Filled.Business,
                        RequiredPermission = "firm.view" },
                new() { Title = "Siteler", Href = "/sites",
                        Icon = Icons.Material.Filled.Apartment,
                        RequiredPermission = "site.view" },
                new() { Title = "Bloklar ve Bağımsız Bölümler", Href = "/units",
                        Icon = Icons.Material.Filled.MeetingRoom,
                        RequiredPermission = "unit.view" }
            ]
        },

        new()
        {
            Title = "Malik / Sakin",
            Icon = Icons.Material.Filled.People,
            Children =
            [
                new() { Title = "Malikler", Href = "/owners",
                        Icon = Icons.Material.Filled.Person,
                        RequiredPermission = "resident.view" },
                new() { Title = "Kiracılar (Ev)", Href = "/tenants",
                        Icon = Icons.Material.Filled.PersonOutline,
                        RequiredPermission = "resident.view" },
                new() { Title = "Yönetim Kurulu", Href = "/board",
                        Icon = Icons.Material.Filled.Groups,
                        RequiredPermission = "board.view" }
            ]
        },

        new()
        {
            Title = "Finansal",
            Icon = Icons.Material.Filled.AttachMoney,
            Children =
            [
                new() { Title = "Bütçeler", Href = "/budgets",
                        Icon = Icons.Material.Filled.PieChart,
                        RequiredPermission = "budget.view" },
                new() { Title = "Tahakkuk", Href = "/accruals",
                        Icon = Icons.Material.Filled.Receipt,
                        RequiredPermission = "accrual.view" },
                new() { Title = "Tahsilat", Href = "/collections",
                        Icon = Icons.Material.Filled.Payments,
                        RequiredPermission = "collection.view" },
                new() { Title = "Hesap Ekstresi", Href = "/statements",
                        Icon = Icons.Material.Filled.AccountBalance,
                        RequiredPermission = "statement.view" },
                new() { Title = "İcra Takibi", Href = "/legal-collections",
                        Icon = Icons.Material.Filled.Gavel,
                        RequiredPermission = "legal.view" }
            ]
        },

        new()
        {
            Title = "Muhasebe (v2)",
            Icon = Icons.Material.Filled.Calculate,
            Children =
            [
                new() { Title = "Hesap Planı", Href = "/accounting/chart",
                        Icon = Icons.Material.Filled.List,
                        RequiredPermission = "accounting.view" },
                new() { Title = "Yevmiye", Href = "/accounting/journals",
                        Icon = Icons.Material.Filled.MenuBook,
                        RequiredPermission = "accounting.view" },
                new() { Title = "Mizan", Href = "/accounting/trial-balance",
                        Icon = Icons.Material.Filled.Balance,
                        RequiredPermission = "accounting.view" }
            ]
        },

        new()
        {
            Title = "İnsan Kaynakları (v2)",
            Icon = Icons.Material.Filled.Badge,
            Children =
            [
                new() { Title = "Personel", Href = "/hr/employees",
                        Icon = Icons.Material.Filled.Engineering,
                        RequiredPermission = "hr.view" },
                new() { Title = "Puantaj", Href = "/hr/timesheet",
                        Icon = Icons.Material.Filled.AccessTime,
                        RequiredPermission = "hr.timesheet.view" },
                new() { Title = "Bordro", Href = "/hr/payroll",
                        Icon = Icons.Material.Filled.RequestQuote,
                        RequiredPermission = "hr.payroll.view" },
                new() { Title = "SGK & Beyannameler", Href = "/hr/declarations",
                        Icon = Icons.Material.Filled.FactCheck,
                        RequiredPermission = "hr.declaration.view" }
            ]
        },

        new()
        {
            Title = "İletişim & Talepler",
            Icon = Icons.Material.Filled.Campaign,
            Children =
            [
                new() { Title = "Talep/Şikayet/Öneri", Href = "/requests",
                        Icon = Icons.Material.Filled.Feedback,
                        RequiredPermission = "request.view" },
                new() { Title = "Duyurular", Href = "/announcements",
                        Icon = Icons.Material.Filled.Notifications,
                        RequiredPermission = "announcement.view" },
                new() { Title = "Karar Defteri", Href = "/decisions",
                        Icon = Icons.Material.Filled.Book,
                        RequiredPermission = "decision.view" }
            ]
        },

        new()
        {
            Title = "Satın Alma & Stok (v2)",
            Icon = Icons.Material.Filled.ShoppingCart,
            Children =
            [
                new() { Title = "Satın Alma Talepleri", Href = "/purchasing/requests",
                        Icon = Icons.Material.Filled.RequestPage,
                        RequiredPermission = "purchasing.view" },
                new() { Title = "Siparişler", Href = "/purchasing/orders",
                        Icon = Icons.Material.Filled.Receipt,
                        RequiredPermission = "purchasing.view" },
                new() { Title = "Stok", Href = "/inventory",
                        Icon = Icons.Material.Filled.Inventory,
                        RequiredPermission = "inventory.view" }
            ]
        },

        new()
        {
            Title = "Raporlar",
            Icon = Icons.Material.Filled.Analytics,
            Children =
            [
                new() { Title = "Finansal Özet", Href = "/reports/financial",
                        Icon = Icons.Material.Filled.TrendingUp },
                new() { Title = "Tahsilat Raporu", Href = "/reports/collections",
                        Icon = Icons.Material.Filled.BarChart },
                new() { Title = "Borçlu Listesi", Href = "/reports/debtors",
                        Icon = Icons.Material.Filled.Warning }
            ]
        },

        new()
        {
            Title = "Sistem",
            Icon = Icons.Material.Filled.Settings,
            Children =
            [
                new() { Title = "Kullanıcılar & Roller", Href = "/system/users",
                        Icon = Icons.Material.Filled.ManageAccounts,
                        RequiredPermission = "system.users" },
                new() { Title = "İzinler", Href = "/system/permissions",
                        Icon = Icons.Material.Filled.Lock,
                        RequiredPermission = "system.permissions" },
                new() { Title = "Denetim Logları", Href = "/system/audit",
                        Icon = Icons.Material.Filled.History,
                        RequiredPermission = "system.audit" },
                new() { Title = "Ayarlar", Href = "/system/settings",
                        Icon = Icons.Material.Filled.Tune,
                        RequiredPermission = "system.settings" }
            ]
        }
    ];
}
