namespace SiteHub.Application.Abstractions.Authentication;

/// <summary>
/// Login + 2FA güvenlik politikası ayarları (<c>appsettings.json</c> "LoginSecurity" bölümü).
///
/// <para>Hem parola hatası hem de 2FA kod hatası için ortak eşik + süre. İki sistemin
/// tutarlı çalışması için tek kaynaktan konfigüre edilir.</para>
///
/// <para>Development: kısa süreler (1 dk) — test hızı önemli.<br/>
/// Production: güvenli default'lar (15 dk) — brute-force koruması.</para>
/// </summary>
public sealed class LoginSecurityOptions
{
    public const string SectionName = "LoginSecurity";

    /// <summary>
    /// Parola için maksimum yanlış deneme sayısı. Bu sayıya ulaşılınca hesap
    /// <see cref="LockoutDurationMinutes"/> kadar kilitlenir. Varsayılan: 5.
    /// </summary>
    public int MaxFailedAttempts { get; set; } = 5;

    /// <summary>
    /// Parola kilit süresi (dakika). Bu süre dolana kadar doğru parola bile
    /// kabul edilmez. Varsayılan: 15.
    /// </summary>
    public int LockoutDurationMinutes { get; set; } = 15;

    /// <summary>
    /// 2FA kodu için maksimum yanlış deneme sayısı. Varsayılan: 5.
    /// </summary>
    public int TwoFactorMaxAttempts { get; set; } = 5;

    /// <summary>
    /// 2FA block süresi (dakika). Rate limit tetiklenince bu kadar bekleme
    /// gerekir. Varsayılan: 15.
    /// </summary>
    public int TwoFactorBlockMinutes { get; set; } = 15;

    // Convenience properties (TimeSpan)
    public TimeSpan LockoutDuration => TimeSpan.FromMinutes(LockoutDurationMinutes);
    public TimeSpan TwoFactorBlockDuration => TimeSpan.FromMinutes(TwoFactorBlockMinutes);
}
