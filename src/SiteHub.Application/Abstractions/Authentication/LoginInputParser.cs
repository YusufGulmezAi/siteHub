using System.Text.RegularExpressions;

namespace SiteHub.Application.Abstractions.Authentication;

/// <summary>
/// Login ekranında tek bir input alanı var (ADR-0011 §1).
/// Kullanıcı TCKN, VKN, YKN, Email veya Mobil girebilir — bu sınıf otomatik tespit eder.
///
/// <para>Tespit kuralları (sırayla denenir):</para>
/// <list type="number">
///   <item>@ içeriyorsa → Email</item>
///   <item>+ ile başlıyorsa veya boşluk/tire sonrası 10-11 rakam → Mobile</item>
///   <item>Sadece rakam ve uzunluk 11 → TCKN (başında 0 olamaz, checksum var)</item>
///   <item>Sadece rakam ve uzunluk 10 → VKN</item>
///   <item>99 ile başlayıp 11 hane → YKN (Yabancı Kimlik No)</item>
///   <item>Diğer → Unknown</item>
/// </list>
///
/// <para>NOT: Checksum doğrulaması YAPILMAZ — sadece tip tespiti. Login handler
/// tespit edilen tipe göre uygun tabloda arar (TCKN/VKN/YKN Person.NationalId,
/// Email Person.Email, Mobile Person.MobilePhone).</para>
/// </summary>
public static class LoginInputParser
{
    private static readonly Regex DigitsOnly = new(@"^\d+$", RegexOptions.Compiled);

    /// <summary>
    /// Input'tan rakam dışı karakterleri temizler (mobil input için).
    /// "+90 (532) 123 45 67" → "905321234567"
    /// </summary>
    private static string NormalizePhone(string input)
    {
        return new string(input.Where(char.IsDigit).ToArray());
    }

    public static LoginInputType Detect(string rawInput)
    {
        if (string.IsNullOrWhiteSpace(rawInput))
            return LoginInputType.Unknown;

        var trimmed = rawInput.Trim();

        // 1. Email
        if (trimmed.Contains('@'))
        {
            // Basit email şekil kontrolü — domain'de nokta var mı
            var atIdx = trimmed.IndexOf('@');
            if (atIdx > 0 && atIdx < trimmed.Length - 3 &&
                trimmed[(atIdx + 1)..].Contains('.'))
                return LoginInputType.Email;
            return LoginInputType.Unknown;
        }

        // 2. Mobile (+ ile başlarsa veya özel karakterler varsa)
        if (trimmed.StartsWith('+') || trimmed.Contains(' ') || trimmed.Contains('-') ||
            trimmed.Contains('(') || trimmed.Contains(')'))
        {
            var digits = NormalizePhone(trimmed);
            // TR mobile: 10 hane (5xx xxxxxxx) veya 11 hane (0 + 10 hane) veya 12 hane (90 + 10)
            if (digits.Length >= 10 && digits.Length <= 13)
                return LoginInputType.Mobile;
            return LoginInputType.Unknown;
        }

        // 3. Sadece rakam?
        if (!DigitsOnly.IsMatch(trimmed))
            return LoginInputType.Unknown;

        // 11 hane
        if (trimmed.Length == 11)
        {
            // YKN: 99 ile başlar
            if (trimmed.StartsWith("99"))
                return LoginInputType.Ykn;

            // TCKN: İlk hane 0 olamaz
            if (trimmed[0] != '0')
                return LoginInputType.Tckn;

            return LoginInputType.Unknown;
        }

        // 10 hane → VKN
        if (trimmed.Length == 10)
            return LoginInputType.Vkn;

        // Aksi halde: belki boşluk/tire'siz mobil? (5xxxxxxxxx = 10 hane)
        // Ama bu VKN ile çakışır → user'a yanlış tip dönebilir. Biz 10 hane için VKN öne aldık.
        // Mobil olmak için özel karakter ZORUNLU → yukarıda yakalandı.

        return LoginInputType.Unknown;
    }

    /// <summary>
    /// Input'u normalize eder (karşılaştırma için). Email lowercase, telefon sadece rakam, vs.
    /// </summary>
    public static string Normalize(string rawInput, LoginInputType type)
    {
        var trimmed = rawInput.Trim();
        return type switch
        {
            LoginInputType.Email => trimmed.ToLowerInvariant(),
            LoginInputType.Mobile => NormalizeMobileToE164Tr(NormalizePhone(trimmed)),
            LoginInputType.Tckn or LoginInputType.Vkn or LoginInputType.Ykn => trimmed,
            _ => trimmed
        };
    }

    /// <summary>
    /// TR telefon normalizasyonu: "+90 532 123 45 67" veya "0532..." veya "532..." → "+905321234567"
    /// </summary>
    private static string NormalizeMobileToE164Tr(string digits)
    {
        if (digits.Length == 10 && digits[0] == '5')        // 5321234567
            return "+90" + digits;
        if (digits.Length == 11 && digits[0] == '0')        // 05321234567
            return "+90" + digits[1..];
        if (digits.Length == 12 && digits.StartsWith("90"))  // 905321234567
            return "+" + digits;
        return "+" + digits;  // Diğer ülke kodları desteği ileride
    }
}

public enum LoginInputType
{
    Unknown = 0,
    Tckn = 1,       // Türk Kimlik No — 11 hane
    Vkn = 2,        // Vergi Kimlik No — 10 hane
    Ykn = 3,        // Yabancı Kimlik No — 99 ile başlayan 11 hane
    Email = 4,
    Mobile = 5
}
