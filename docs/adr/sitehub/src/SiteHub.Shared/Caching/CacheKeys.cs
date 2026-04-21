namespace SiteHub.Shared.Caching;

/// <summary>
/// Cache key'leri için merkezi sabitler.
/// Her kategori için namespace ve factory method.
///
/// Neden? Elle "user:permissions:" yazmak hataya açık — yanlış yazılan key
/// cache miss'e sebep olur, hata loglarda görünmez. Merkezi yerde tutarsak
/// IDE auto-complete ve refactor desteği alırız.
/// </summary>
public static class CacheKeys
{
    /// <summary>User permissions cache (ADR-0011 §8.2).</summary>
    public static class UserPermissions
    {
        public const string Prefix = "user:permissions:";
        public static string For(Guid loginAccountId) => $"{Prefix}{loginAccountId:N}";
        public const string Pattern = "user:permissions:*";
    }

    /// <summary>Session cache (ADR-0011 §7.1).</summary>
    public static class Session
    {
        public const string Prefix = "session:";
        public static string For(string sessionId) => $"{Prefix}{sessionId}";
        public static string UserSessions(Guid loginAccountId) => $"user:{loginAccountId:N}:sessions";
    }

    /// <summary>Address reference data (Country/Region/Province/District/Neighborhood).</summary>
    public static class Address
    {
        public const string Regions = "ref:regions";
        public static string ProvincesByRegion(Guid regionId) => $"ref:provinces:region:{regionId:N}";
        public static string DistrictsByProvince(Guid provinceId) => $"ref:districts:province:{provinceId:N}";
        public static string NeighborhoodsByDistrict(Guid districtId) => $"ref:neighborhoods:district:{districtId:N}";
    }

    /// <summary>Organization metadata.</summary>
    public static class Organization
    {
        public static string Metadata(long code) => $"org:metadata:{code}";
        public static string BySlug(string slug) => $"org:slug:{slug}";
    }

    /// <summary>Site metadata.</summary>
    public static class Site
    {
        public static string Metadata(long code) => $"site:metadata:{code}";
        public static string Stats(long code) => $"site:stats:{code}";
    }
}

/// <summary>
/// Standart TTL süreleri.
/// </summary>
public static class CacheTtl
{
    /// <summary>15 dakika — kullanıcı izinleri için (ADR-0011).</summary>
    public static readonly TimeSpan UserPermissions = TimeSpan.FromMinutes(15);

    /// <summary>Oturum süresince — idle timeout için aynı değer (ADR-0011).</summary>
    public static readonly TimeSpan Session = TimeSpan.FromMinutes(15);

    /// <summary>1 saat — referans data için (adresler, bankalar nadir değişir).</summary>
    public static readonly TimeSpan ReferenceData = TimeSpan.FromHours(1);

    /// <summary>5 dakika — organizasyon/site metadata.</summary>
    public static readonly TimeSpan Metadata = TimeSpan.FromMinutes(5);

    /// <summary>30 saniye — short-lived cache.</summary>
    public static readonly TimeSpan Short = TimeSpan.FromSeconds(30);
}
