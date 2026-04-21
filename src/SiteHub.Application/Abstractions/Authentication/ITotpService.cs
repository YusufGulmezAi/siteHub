namespace SiteHub.Application.Abstractions.Authentication;

/// <summary>
/// TOTP (Time-based One-Time Password) servisi — RFC 6238.
///
/// <para>Authenticator uygulamalar\u0131 (Google Authenticator, Microsoft Authenticator,
/// Authy, 1Password vb.) ile uyumlu.</para>
///
/// <para>Standart parametreler (de\u011fi\u015ftirilmez \u2014 authenticator uygulamalar\u0131
/// bu varsay\u0131lanlar\u0131 kullan\u0131r):</para>
/// <list type="bullet">
///   <item>Algorithm: HMAC-SHA1</item>
///   <item>Digits: 6</item>
///   <item>Period: 30 saniye</item>
/// </list>
/// </summary>
public interface ITotpService
{
    /// <summary>
    /// Yeni TOTP secret \u00fcretir (20 byte = 160 bit random) ve Base32 encode eder.
    /// Sonu\u00e7 ~32 karakter.
    /// </summary>
    string GenerateSecret();

    /// <summary>
    /// Authenticator uygulamalar\u0131n\u0131n QR ile taran\u0131p eklenebilmesi i\u00e7in
    /// <c>otpauth://</c> URI \u00fcretir.
    /// </summary>
    /// <param name="secret">Base32-encoded secret.</param>
    /// <param name="accountName">Kullan\u0131c\u0131 email'i veya username (authenticator listede g\u00f6r\u00fcn\u00fcr).</param>
    /// <param name="issuer">Uygulama ad\u0131 (SiteHub).</param>
    /// <returns>
    /// <c>otpauth://totp/SiteHub:admin@sitehub.local?secret=...&amp;issuer=SiteHub&amp;algorithm=SHA1&amp;digits=6&amp;period=30</c>
    /// </returns>
    string BuildOtpAuthUri(string secret, string accountName, string issuer);

    /// <summary>
    /// Kullan\u0131c\u0131n\u0131n girdi\u011fi 6 haneli kodu do\u011frular.
    /// \u00b11 zaman penceresine (30 sn) tolerans verir \u2014 clock drift'e kar\u015f\u0131 normal.
    /// </summary>
    /// <param name="secret">Kullan\u0131c\u0131n\u0131n kay\u0131tl\u0131 secret'\u0131 (Base32).</param>
    /// <param name="code">Kullan\u0131c\u0131n\u0131n girdi\u011fi 6 haneli kod.</param>
    /// <returns>Kod ge\u00e7erli ve zaman penceresi i\u00e7indeyse true.</returns>
    bool VerifyCode(string secret, string code);
}
