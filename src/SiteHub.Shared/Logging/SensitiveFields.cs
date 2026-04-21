namespace SiteHub.Shared.Logging;

/// <summary>
/// Loglanmaması gereken veya maskelenmesi gereken alan adları.
/// Bu liste, Serilog destructuring policy'sinde ve MediatR logging
/// behavior'ında kullanılır.
///
/// Bir property adı (case-insensitive) bu listeye uyuyorsa, değeri "***"
/// olarak değiştirilir.
///
/// YENİ HASSAS ALAN EKLERKEN burayı güncelle. Bu merkezi yaklaşım, her
/// yeni endpoint'te "maskelemeyi hatırlamak" zorunluluğunu ortadan kaldırır.
/// </summary>
public static class SensitiveFields
{
    /// <summary>Tamamen "***" ile değiştirilecek alan adları.</summary>
    public static readonly IReadOnlySet<string> FullyMasked = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        // Parolalar
        "password", "currentpassword", "newpassword", "passwordconfirmation",
        "confirmpassword", "oldpassword",

        // Güvenlik kodları
        "cvv", "cvc", "securitycode",
        "otp", "otpcode", "totpcode", "smscode", "emailcode", "verificationcode",
        "pin", "pincode",

        // Token ve anahtarlar
        "token", "accesstoken", "refreshtoken", "idtoken", "bearertoken",
        "apikey", "apisecret", "clientsecret",
        "securitystamp", "concurrencystamp",

        // Özel cevaplar
        "securityanswer", "recoveryanswer"
    };

    /// <summary>Kısmen maskelenen alanlar: ilk/son birkaç karakter kalır, ortası "*" olur.</summary>
    public static readonly IReadOnlyDictionary<string, (int KeepStart, int KeepEnd)> PartiallyMasked
        = new Dictionary<string, (int, int)>(StringComparer.OrdinalIgnoreCase)
    {
        // Ulusal kimlik (ilk 3, son 4)
        ["nationalid"] = (3, 4),
        ["tckn"] = (3, 4),
        ["vkn"] = (2, 3),
        ["ykn"] = (3, 4),

        // Kart (ilk 6, son 4 — PCI DSS standartı)
        ["cardnumber"] = (6, 4),
        ["pan"] = (6, 4),
        ["cardno"] = (6, 4),

        // Banka
        ["iban"] = (4, 4),
        ["accountnumber"] = (0, 4),

        // İletişim
        ["phone"] = (0, 4),
        ["phonenumber"] = (0, 4),
        ["mobile"] = (0, 4),
        ["gsm"] = (0, 4),
        ["email"] = (2, 0)   // "al***@gmail.com" stili için özel handling
    };

    /// <summary>
    /// Verilen property adı tamamen maskelenecek mi?
    /// </summary>
    public static bool ShouldFullyMask(string propertyName)
        => FullyMasked.Contains(propertyName);

    /// <summary>
    /// Verilen property adı kısmen maskelenecek mi?
    /// </summary>
    public static bool ShouldPartiallyMask(string propertyName, out int keepStart, out int keepEnd)
    {
        if (PartiallyMasked.TryGetValue(propertyName, out var rules))
        {
            keepStart = rules.KeepStart;
            keepEnd = rules.KeepEnd;
            return true;
        }
        keepStart = keepEnd = 0;
        return false;
    }

    /// <summary>
    /// String değeri belirtilen kurala göre maskeler.
    /// Örn: "12345678901", keepStart=3, keepEnd=4 → "123****8901"
    /// </summary>
    public static string MaskString(string? value, int keepStart, int keepEnd)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        if (value.Length <= keepStart + keepEnd) return new string('*', value.Length);

        var maskedMiddle = new string('*', value.Length - keepStart - keepEnd);
        return string.Concat(
            value.AsSpan(0, keepStart),
            maskedMiddle.AsSpan(),
            value.AsSpan(value.Length - keepEnd, keepEnd)
        );
    }

    /// <summary>Tam maskeleme için sabit değer.</summary>
    public const string FullMask = "***";
}
