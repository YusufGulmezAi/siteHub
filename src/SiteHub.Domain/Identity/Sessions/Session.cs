namespace SiteHub.Domain.Identity.Sessions;

/// <summary>
/// Aktif kullanıcı oturumu (ADR-0011 §7).
///
/// <para>Session sadece Redis'te yaşar — DB'de tablo yok (MVP). Bunun sebebi:</para>
/// <list type="bullet">
///   <item>15 dakika sliding TTL — Redis bunu otomatik yönetir</item>
///   <item>Her request'te okunur (permission check, context resolve) — ms seviyesinde olmalı</item>
///   <item>Server restart'ta session'lar biter — "fresh start" (acceptable for MVP)</item>
/// </list>
///
/// <para>İleride `sessions` tablosu (DB'de özet kayıt — forensic/audit için) ADR-0011 §7.1'de
/// belirtildiği üzere eklenebilir ama MVP kapsamında değil.</para>
///
/// <para>SECURITY (ADR-0011 §7.2 "Tek IP, Tek Cihaz, Tek Oturum"):</para>
/// <list type="bullet">
///   <item>Her request'te IpAddress + DeviceId karşılaştırması yapılır</item>
///   <item>Aynı LoginAccount'un başka session'ı varsa eski kapatılır (tek session)</item>
///   <item>IP veya DeviceId farklıysa session kapatılır + SecurityEvent</item>
/// </list>
///
/// <para>Bu class JSON serialize edilir → property'ler public set olmak zorunda (record init yeter).</para>
/// </summary>
public sealed record Session
{
    public required SessionId SessionId { get; init; }
    public required LoginAccountId LoginAccountId { get; init; }
    public required PersonId PersonId { get; init; }

    /// <summary>Random 32-byte string. Cookie'de sitehub_device olarak tutulur.</summary>
    public required string DeviceId { get; init; }

    /// <summary>Login anındaki IP. Her request'te bu değerle karşılaştırılır.</summary>
    public required string IpAddress { get; init; }

    public required string UserAgent { get; init; }
    public required bool IsMobile { get; init; }

    /// <summary>Kullanıcının tam adı (Person.FullName) — UI'da header/menüde gösterim için.</summary>
    public required string FullName { get; init; }

    /// <summary>Login email — UI'da menüde, activity logda gösterim.</summary>
    public required string Email { get; init; }

    /// <summary>2FA etkin mi? Session a\u00e7\u0131l\u0131rken snapshot al\u0131n\u0131r.</summary>
    public required bool TwoFactorEnabled { get; init; }

    public required DateTimeOffset LoginAt { get; init; }
    public required DateTimeOffset LastActivityAt { get; init; }

    /// <summary>
    /// O an'ın aktif context'i (URL'den belirlenir). Null olabilir (kullanıcı home'da /).
    /// ADR-0011 §7.7.
    /// </summary>
    public ActiveContext? ActiveContext { get; init; }

    /// <summary>
    /// Tüm membership context'lerinin listesi — session açılışında snapshot alınır.
    /// Kullanıcı context switch yaparken bu listeden seçer (örn. dropdown menu).
    /// Permissions BURADA değil, ActiveContext.PermissionSnapshot içinde.
    /// </summary>
    public required IReadOnlyList<MembershipSummary> AvailableContexts { get; init; }

    /// <summary>
    /// 2FA bekliyor mu? true ise session "yar\u0131-aktif" durumda \u2014 sadece
    /// <c>/auth/verify-2fa</c> ve <c>/auth/logout</c> endpoint'lerine eri\u015fim var.
    /// Ba\u015far\u0131l\u0131 TOTP verify'dan sonra false'a d\u00fc\u015fer.
    /// ADR-0011 §4.
    /// </summary>
    public bool Pending2FA { get; init; }

    // ─── Factory ─────────────────────────────────────────────────────────

    public static Session Create(
        LoginAccountId loginAccountId,
        PersonId personId,
        string fullName,
        string email,
        string deviceId,
        string ipAddress,
        string userAgent,
        bool isMobile,
        IReadOnlyList<MembershipSummary> availableContexts,
        DateTimeOffset now,
        bool pending2FA = false,
        bool twoFactorEnabled = false)
    {
        return new Session
        {
            SessionId = SessionId.New(),
            LoginAccountId = loginAccountId,
            PersonId = personId,
            FullName = fullName,
            Email = email,
            TwoFactorEnabled = twoFactorEnabled,
            DeviceId = deviceId,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            IsMobile = isMobile,
            LoginAt = now,
            LastActivityAt = now,
            ActiveContext = null,
            AvailableContexts = availableContexts,
            Pending2FA = pending2FA,
        };
    }

    public Session WithActiveContext(ActiveContext? context, DateTimeOffset now)
        => this with { ActiveContext = context, LastActivityAt = now };

    public Session Touch(DateTimeOffset now) => this with { LastActivityAt = now };

    /// <summary>2FA do\u011fruland\u0131: Pending2FA = false yap.</summary>
    public Session CompletePending2FA(DateTimeOffset now)
        => this with { Pending2FA = false, LastActivityAt = now };
}

/// <summary>
/// Bir kullanıcının sahip olduğu bir membership'in özet bilgisi (UI'da gösterim için).
/// Session içinde taşınır — her request'te DB sorgusu yapılmaz.
/// </summary>
public sealed record MembershipSummary(
    Guid MembershipId,
    int ContextType,        // MembershipContextType'ın int değeri (System=1, Organization=2, ...)
    Guid? ContextId,
    string? ContextCode,    // Organization/Site Feistel kodu (orgCode, siteCode)
    string ContextDisplayName,
    Guid RoleId,
    string RoleName);
