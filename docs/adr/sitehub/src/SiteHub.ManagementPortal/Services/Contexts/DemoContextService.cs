namespace SiteHub.ManagementPortal.Services.Contexts;

/// <summary>
/// Context combobox'unda gösterilecek öğe.
/// Gerçek implementasyonda backend'den gelecek (AvailableContextsQuery).
/// </summary>
public sealed record ContextItem(
    string Id,
    ContextLevel Level,
    string Name,
    string? ParentId,
    string? ParentName);

public enum ContextLevel { System, Organization, Site }

/// <summary>
/// Şu an demo amaçlı. İleride IContextRepository ile backend'den gelecek.
/// Kullanıcının erişebildiği tüm context'leri sağlar.
/// </summary>
public sealed class DemoContextService
{
    private readonly List<ContextItem> _all;
    private readonly List<string> _recentIds = [];

    public DemoContextService()
    {
        // Demo veri — 1200 site simülasyonu için kasıtlı büyük liste
        _all =
        [
            new("sys", ContextLevel.System, "Sistem Yönetimi", null, null)
        ];

        // 5 örnek kiracı
        var firmNames = new[]
        {
            "Marmara Yönetim A.Ş.",
            "Ege Profesyonel Hizmetler",
            "Başkent Site İşletmeciliği",
            "Boğaz Yönetim Ltd.",
            "Anadolu Yapı Yönetim"
        };

        var siteSuffixes = new[]
        {
            "Park Evleri", "Rezidans", "Konakları", "Sitesi", "Evleri",
            "Yaşam", "Plaza", "Towers", "Life", "Garden",
            "Panorama", "Hills", "Vadisi", "Bahçeleri", "Köşkleri"
        };

        var citySuffixes = new[]
        {
            "Ataşehir", "Beykoz", "Beşiktaş", "Kadıköy", "Üsküdar",
            "Maltepe", "Bakırköy", "Şişli", "Sarıyer", "Etiler",
            "Yeşilköy", "Florya", "Çekmeköy", "Çengelköy", "Bostancı"
        };

        var rnd = new Random(42); // deterministik

        foreach (var firmName in firmNames)
        {
            var firmId = $"firm-{Guid.CreateVersion7():N}"[..16];
            _all.Add(new(firmId, ContextLevel.Organization, firmName, null, null));

            // Her kiracıya 200-300 arası site — toplam ~1200+ site
            var siteCount = rnd.Next(200, 301);
            for (int i = 0; i < siteCount; i++)
            {
                var siteName = $"{citySuffixes[rnd.Next(citySuffixes.Length)]} {siteSuffixes[rnd.Next(siteSuffixes.Length)]} {i + 1}";
                var siteId = $"site-{Guid.CreateVersion7():N}"[..16];
                _all.Add(new(siteId, ContextLevel.Site, siteName, firmId, firmName));
            }
        }
    }

    /// <summary>Kullanıcının erişebildiği tüm context'ler (bir kez yüklenir).</summary>
    public IReadOnlyList<ContextItem> All => _all;

    /// <summary>Son kullanılan context'ler — en yeni başta, max 5.</summary>
    public IReadOnlyList<ContextItem> Recents =>
        _recentIds.Select(id => _all.FirstOrDefault(c => c.Id == id))
                  .OfType<ContextItem>()
                  .ToList();

    /// <summary>Aranan metne göre context'leri filtreler (max 50 sonuç — çok sonuç UI'yı bozar).</summary>
    public IReadOnlyList<ContextItem> Search(string? query, int max = 50)
    {
        if (string.IsNullOrWhiteSpace(query))
            return _all.Take(max).ToList();

        var normalized = Normalize(query);
        return _all
            .Where(c => Normalize(c.Name).Contains(normalized)
                        || (c.ParentName != null && Normalize(c.ParentName).Contains(normalized)))
            .Take(max)
            .ToList();
    }

    /// <summary>Bir kiracının altındaki siteler.</summary>
    public IReadOnlyList<ContextItem> SitesOfOrganization(string firmId) =>
        _all.Where(c => c.Level == ContextLevel.Site && c.ParentId == firmId).ToList();

    /// <summary>Seçilen context'i "recent" listesinin başına ekler.</summary>
    public void MarkRecent(string contextId)
    {
        _recentIds.Remove(contextId);
        _recentIds.Insert(0, contextId);
        while (_recentIds.Count > 5) _recentIds.RemoveAt(_recentIds.Count - 1);
    }

    // Türkçe kültür — ToLower'da I→ı, İ→i doğru uygulansın diye gerekli
    private static readonly System.Globalization.CultureInfo TurkishCulture
        = System.Globalization.CultureInfo.GetCultureInfo("tr-TR");

    /// <summary>
    /// Arama için Türkçe-bilinçli normalize. Detay: MenuItem.Normalize (aynı strateji).
    /// </summary>
    private static string Normalize(string s)
    {
        var lowered = s.ToLower(TurkishCulture);
        return lowered
            .Replace('ı', 'i')
            .Replace('ş', 's')
            .Replace('ğ', 'g')
            .Replace('ü', 'u')
            .Replace('ö', 'o')
            .Replace('ç', 'c');
    }
}
