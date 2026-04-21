using System.Security.Cryptography;
using System.Text;

namespace SiteHub.Application.Abstractions.Authentication;

/// <summary>
/// Token hash'leme yardımcıları.
///
/// <para>Şifre sıfırlama token'ı DB'de plaintext saklanmaz. Kullanıcı elindeki
/// plain token'ı SHA-256 ile hash'ler, DB'deki hash ile karşılaştırırız.</para>
///
/// <para>Güvenlik notu: Token rastgele 32 byte → PBKDF2/bcrypt gibi slow hash gereksiz
/// (brute-force için 256-bit entropy yeter). SHA-256 yeterli ve hızlı.</para>
/// </summary>
public static class TokenHasher
{
    /// <summary>
    /// 32 byte crypto-random token üretir, URL-safe base64 döner (~43 karakter).
    /// Kullanıcıya bu gönderilir (email linkinde).
    /// </summary>
    public static string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    /// <summary>
    /// 6 haneli numeric kod üretir. SMS için (kolay yazılabilir).
    /// </summary>
    public static string GenerateNumericCode()
    {
        // RandomNumberGenerator.GetInt32 inclusive-exclusive
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000);
        return code.ToString("D6");
    }

    /// <summary>
    /// Token'ın SHA-256 hash'ini hex string olarak döner (64 karakter).
    /// </summary>
    public static string Hash(string token)
    {
        if (string.IsNullOrEmpty(token))
            throw new ArgumentException("Token boş olamaz.", nameof(token));

        var bytes = Encoding.UTF8.GetBytes(token);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
