using OtpNet;
using SiteHub.Application.Abstractions.Authentication;

namespace SiteHub.Infrastructure.Authentication;

/// <summary>
/// Otp.NET tabanl\u0131 TOTP implementation (RFC 6238).
///
/// <para>Otp.NET k\u00fct\u00fcphanesi authenticator app'ler ile standart uyumludur:
/// HMAC-SHA1, 6 digit, 30 sn period.</para>
/// </summary>
public sealed class OtpNetTotpService : ITotpService
{
    // \u00b11 time step tolerans \u2014 clock drift (kullan\u0131c\u0131 telefonu saati 10 sn sapm\u0131\u015f olabilir)
    private static readonly VerificationWindow Window = new(previous: 1, future: 1);

    public string GenerateSecret()
    {
        var key = KeyGeneration.GenerateRandomKey(20);  // 160 bit
        return Base32Encoding.ToString(key);
    }

    public string BuildOtpAuthUri(string secret, string accountName, string issuer)
    {
        if (string.IsNullOrWhiteSpace(secret))
            throw new ArgumentException("Secret bo\u015f olamaz.", nameof(secret));
        if (string.IsNullOrWhiteSpace(accountName))
            throw new ArgumentException("AccountName bo\u015f olamaz.", nameof(accountName));
        if (string.IsNullOrWhiteSpace(issuer))
            throw new ArgumentException("Issuer bo\u015f olamaz.", nameof(issuer));

        // otpauth://totp/Issuer:AccountName?secret=...&issuer=...&algorithm=SHA1&digits=6&period=30
        var encodedIssuer = Uri.EscapeDataString(issuer);
        var encodedAccount = Uri.EscapeDataString(accountName);
        var encodedSecret = Uri.EscapeDataString(secret);

        return $"otpauth://totp/{encodedIssuer}:{encodedAccount}" +
               $"?secret={encodedSecret}" +
               $"&issuer={encodedIssuer}" +
               $"&algorithm=SHA1" +
               $"&digits=6" +
               $"&period=30";
    }

    public bool VerifyCode(string secret, string code)
    {
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(code))
            return false;

        // Kullan\u0131c\u0131 "123 456" \u015feklinde bo\u015fluklu girse de kabul et
        var cleanCode = code.Replace(" ", "").Replace("-", "");
        if (cleanCode.Length != 6 || !cleanCode.All(char.IsDigit))
            return false;

        byte[] keyBytes;
        try
        {
            keyBytes = Base32Encoding.ToBytes(secret);
        }
        catch
        {
            return false;
        }

        var totp = new Totp(keyBytes, step: 30, mode: OtpHashMode.Sha1, totpSize: 6);
        return totp.VerifyTotp(cleanCode, out _, Window);
    }
}
