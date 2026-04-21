using System.Globalization;
using System.Text;

namespace SiteHub.Domain.Text;

/// <summary>
/// Türkçe metin normalize'ı — arama için ortak kural.
///
/// Ne yapar?
/// - Türkçe kültürle ToLower (İ→i, I→ı doğru uygulanır)
/// - Baş/son boşluklar kırpılır, iç çoklu boşluklar teke indirilir
/// - DİAKRİTİK kaldırılmaz (kullanıcı "şiş" yazıyorsa "Şişli" bulsun, "sisli" yazarsa BULMASIN)
///
/// Nerede kullanılır?
/// - SearchableAggregateRoot.UpdateSearchText — DB'ye yazılırken normalize
/// - Uygulama tarafında arama yaparken — kullanıcının yazdığı metni normalize
/// - Böylece her iki taraf aynı kuralda → deterministic eşleşme
///
/// ÖRNEK:
///   "Şişli YÖNETİM A.Ş."        → "şişli yönetim a.ş."
///   "  ATAŞEHİR  kiracı  "       → "ataşehir kiracı"
///   "ÜSKÜDAR YÖNETİM"            → "üsküdar yönetim"
/// </summary>
public static class TurkishNormalizer
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    public static string Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        // 1) Türkçe kültürde küçük harfe çevir (İ → i, I → ı)
        var lowered = input.ToLower(Turkish);

        // 2) Boşlukları kırp + iç çoklu boşlukları teke indir
        var sb = new StringBuilder(lowered.Length);
        var lastWasSpace = false;
        foreach (var ch in lowered.AsSpan().Trim())
        {
            if (char.IsWhiteSpace(ch))
            {
                if (!lastWasSpace) sb.Append(' ');
                lastWasSpace = true;
            }
            else
            {
                sb.Append(ch);
                lastWasSpace = false;
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Birden fazla alanın birleştirilmiş normalize halini üret.
    /// Örn: Name + CommercialTitle + TaxId → tek arama dizgisi.
    /// Null/empty alanlar atlanır.
    /// </summary>
    public static string Combine(params string?[] fields)
    {
        var nonEmpty = fields
            .Where(f => !string.IsNullOrWhiteSpace(f))
            .Select(f => Normalize(f));
        return string.Join(" ", nonEmpty);
    }
}
