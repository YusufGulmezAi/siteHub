namespace SiteHub.Shared.Authentication;

/// <summary>
/// Cookie authentication scheme sabitleri (ADR-0011 §7).
///
/// <para>İki ayrı scheme:</para>
/// <list type="bullet">
///   <item><c>MgmtAuth</c> — ManagementPortal (organizasyon/site yönetimi)</item>
///   <item><c>ResidentAuth</c> — ResidentPortal (malik/sakin uygulaması)</item>
/// </list>
///
/// <para>İki ayrı cookie adı → aynı tarayıcıda iki portal'a aynı anda
/// bağımsız oturum açılabilir. Karışmaz.</para>
/// </summary>
public static class CookieSchemes
{
    /// <summary>Management Portal auth scheme.</summary>
    public const string Management = "MgmtAuth";

    /// <summary>Management Portal cookie adı.</summary>
    public const string ManagementCookieName = ".SiteHub.Mgmt";

    /// <summary>Resident Portal auth scheme.</summary>
    public const string Resident = "ResidentAuth";

    /// <summary>Resident Portal cookie adı.</summary>
    public const string ResidentCookieName = ".SiteHub.Resident";

    /// <summary>Device ID cookie — her iki portal için ortak.</summary>
    public const string DeviceIdCookieName = "sitehub_device";
}

/// <summary>ClaimsPrincipal claim type sabitleri.</summary>
public static class SiteHubClaims
{
    /// <summary>Session ID (Redis key'inin bir parçası).</summary>
    public const string SessionId = "sitehub:session_id";

    /// <summary>LoginAccount ID.</summary>
    public const string LoginAccountId = "sitehub:login_account_id";

    /// <summary>Person ID.</summary>
    public const string PersonId = "sitehub:person_id";

    /// <summary>Device ID (cookie'deki değerle eşleşmeli).</summary>
    public const string DeviceId = "sitehub:device_id";
}
