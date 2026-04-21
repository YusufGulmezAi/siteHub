namespace SiteHub.Infrastructure.Persistence.Seed.Geography;

/// <summary>
/// Türkiye için coğrafi bölge tanımları ve il → bölge eşlemesi.
///
/// Türkiye'de 7 coğrafi bölge (resmi) + 1 özel "Türkiye Dışı" placeholder.
/// İl → Bölge eşlemesi il plaka numarasına (IL_ID) göre.
///
/// Kaynak: resmi Türkiye coğrafi bölgeleri (MEB/İl Genel Meclisi standart).
/// </summary>
internal static class TurkishRegionMap
{
    /// <summary>Coğrafi bölge kodu + görünen ad + display order.</summary>
    public static readonly IReadOnlyList<(string Code, string Name, int DisplayOrder)> Regions =
    [
        ("MARMARA",           "Marmara Bölgesi",            1),
        ("EGE",               "Ege Bölgesi",                2),
        ("AKDENIZ",           "Akdeniz Bölgesi",            3),
        ("IC_ANADOLU",        "İç Anadolu Bölgesi",         4),
        ("KARADENIZ",         "Karadeniz Bölgesi",          5),
        ("DOGU_ANADOLU",      "Doğu Anadolu Bölgesi",       6),
        ("GUNEYDOGU_ANADOLU", "Güneydoğu Anadolu Bölgesi",  7),
        ("TURKIYE_DISI",      "Türkiye Dışı",               99)
    ];

    /// <summary>
    /// İl plaka kodu (IL_ID) → coğrafi bölge kodu eşlemesi.
    /// 81 il resmi dağılımı.
    /// </summary>
    public static readonly IReadOnlyDictionary<int, string> ProvinceToRegion = new Dictionary<int, string>
    {
        // Marmara (11 il)
        [10] = "MARMARA",   // Balıkesir
        [11] = "MARMARA",   // Bilecik
        [16] = "MARMARA",   // Bursa
        [17] = "MARMARA",   // Çanakkale
        [22] = "MARMARA",   // Edirne
        [34] = "MARMARA",   // İstanbul
        [39] = "MARMARA",   // Kırklareli
        [41] = "MARMARA",   // Kocaeli
        [54] = "MARMARA",   // Sakarya
        [59] = "MARMARA",   // Tekirdağ
        [77] = "MARMARA",   // Yalova

        // Ege (8 il)
        [3]  = "EGE",       // Afyonkarahisar
        [9]  = "EGE",       // Aydın
        [20] = "EGE",       // Denizli
        [35] = "EGE",       // İzmir
        [43] = "EGE",       // Kütahya
        [45] = "EGE",       // Manisa
        [48] = "EGE",       // Muğla
        [64] = "EGE",       // Uşak

        // Akdeniz (8 il)
        [1]  = "AKDENIZ",   // Adana
        [7]  = "AKDENIZ",   // Antalya
        [15] = "AKDENIZ",   // Burdur
        [31] = "AKDENIZ",   // Hatay
        [32] = "AKDENIZ",   // Isparta
        [33] = "AKDENIZ",   // Mersin
        [46] = "AKDENIZ",   // Kahramanmaraş
        [80] = "AKDENIZ",   // Osmaniye

        // İç Anadolu (13 il)
        [6]  = "IC_ANADOLU", // Ankara
        [14] = "IC_ANADOLU", // Bolu — not: Bolu coğrafi olarak Karadeniz ama bazı kaynaklarda İç Anadolu
        [18] = "IC_ANADOLU", // Çankırı
        [19] = "IC_ANADOLU", // Çorum
        [26] = "IC_ANADOLU", // Eskişehir
        [38] = "IC_ANADOLU", // Kayseri
        [40] = "IC_ANADOLU", // Kırşehir
        [42] = "IC_ANADOLU", // Konya
        [50] = "IC_ANADOLU", // Nevşehir
        [51] = "IC_ANADOLU", // Niğde
        [58] = "IC_ANADOLU", // Sivas
        [66] = "IC_ANADOLU", // Yozgat
        [68] = "IC_ANADOLU", // Aksaray
        [70] = "IC_ANADOLU", // Karaman
        [71] = "IC_ANADOLU", // Kırıkkale

        // Karadeniz (18 il)
        [5]  = "KARADENIZ",  // Amasya
        [8]  = "KARADENIZ",  // Artvin
        [28] = "KARADENIZ",  // Giresun
        [29] = "KARADENIZ",  // Gümüşhane
        [37] = "KARADENIZ",  // Kastamonu
        [52] = "KARADENIZ",  // Ordu
        [53] = "KARADENIZ",  // Rize
        [55] = "KARADENIZ",  // Samsun
        [57] = "KARADENIZ",  // Sinop
        [60] = "KARADENIZ",  // Tokat
        [61] = "KARADENIZ",  // Trabzon
        [67] = "KARADENIZ",  // Zonguldak
        [69] = "KARADENIZ",  // Bayburt
        [74] = "KARADENIZ",  // Bartın
        [78] = "KARADENIZ",  // Karabük
        [81] = "KARADENIZ",  // Düzce

        // Doğu Anadolu (14 il)
        [4]  = "DOGU_ANADOLU", // Ağrı
        [12] = "DOGU_ANADOLU", // Bingöl
        [13] = "DOGU_ANADOLU", // Bitlis
        [23] = "DOGU_ANADOLU", // Elazığ
        [24] = "DOGU_ANADOLU", // Erzincan
        [25] = "DOGU_ANADOLU", // Erzurum
        [30] = "DOGU_ANADOLU", // Hakkari
        [36] = "DOGU_ANADOLU", // Kars
        [44] = "DOGU_ANADOLU", // Malatya
        [49] = "DOGU_ANADOLU", // Muş
        [62] = "DOGU_ANADOLU", // Tunceli
        [65] = "DOGU_ANADOLU", // Van
        [75] = "DOGU_ANADOLU", // Ardahan
        [76] = "DOGU_ANADOLU", // Iğdır

        // Güneydoğu Anadolu (9 il)
        [2]  = "GUNEYDOGU_ANADOLU", // Adıyaman
        [21] = "GUNEYDOGU_ANADOLU", // Diyarbakır
        [27] = "GUNEYDOGU_ANADOLU", // Gaziantep
        [47] = "GUNEYDOGU_ANADOLU", // Mardin
        [56] = "GUNEYDOGU_ANADOLU", // Siirt
        [63] = "GUNEYDOGU_ANADOLU", // Şanlıurfa
        [72] = "GUNEYDOGU_ANADOLU", // Batman
        [73] = "GUNEYDOGU_ANADOLU", // Şırnak
        [79] = "GUNEYDOGU_ANADOLU", // Kilis
    };

    /// <summary>Plaka kodunu sıfırdolgulu string'e çevirir (1 → "01", 34 → "34").</summary>
    public static string PlateCodeOf(int provinceId) =>
        provinceId.ToString("D2");
}
