namespace SiteHub.ManagementPortal.Components.Navigation;

/// <summary>
/// Menü öğesi veri modeli. Markup yerine veri üzerinden tutulur ki
/// arama filtrelemesi yapılabilsin.
/// </summary>
public sealed class MenuItem
{
    public required string Title { get; init; }
    public string? Href { get; init; }
    public string? Icon { get; init; }
    public List<MenuItem> Children { get; init; } = [];

    /// <summary>Yetki kontrolü için gerekli permission (null ise herkes görür).</summary>
    public string? RequiredPermission { get; init; }

    public bool HasChildren => Children.Count > 0;

    /// <summary>
    /// <summary>
    /// Arama için: bu öğe veya herhangi bir alt öğesi aramayla eşleşiyor mu?
    ///
    /// Türkçe-bilinçli fuzzy search:
    /// - Case-insensitive (İ/i/I/ı tuzaklarını düşünür — Türkçe CompareInfo kullanır)
    /// - Diacritic-insensitive ("ataşehir" ~ "atasehir" eşleşir — mobil kullanıcılar
    ///   ş/ğ/ü yazmadan hızlı arama yapabiliyor)
    /// </summary>
    public bool Matches(string? query)
    {
        if (string.IsNullOrWhiteSpace(query)) return true;

        var needle = Normalize(query);
        var haystack = Normalize(Title);
        return haystack.Contains(needle, StringComparison.Ordinal)
               || Children.Any(c => c.Matches(query));
    }

    // Türkçe kültür — ToLower'da I→ı, İ→i doğru uygulansın diye gerekli
    private static readonly System.Globalization.CultureInfo TurkishCulture
        = System.Globalization.CultureInfo.GetCultureInfo("tr-TR");

    /// <summary>
    /// Arama için string normalize. İki aşama:
    ///  (1) Türkçe culture ile küçük harfe çevir (I/İ/ı/i doğru map olsun)
    ///  (2) Türkçe diacritic'leri ASCII muadilleriyle değiştir (fuzzy UX)
    ///
    /// Örn: "İstanbul Şubesi" → "istanbul subesi"
    ///      "ATAŞEHİR"        → "atasehir"
    ///      "göztepe"         → "goztepe"
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
