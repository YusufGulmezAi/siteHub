using System.Globalization;
using System.Text;

namespace SiteHub.Infrastructure.Persistence.Seed.Geography;

/// <summary>
/// turkey-addresses.csv dosyasını okur. Bağımsız bir parser — CsvHelper gibi
/// kütüphane eklememek için manuel yazıldı (sadece 7 kolon, sabit format).
///
/// Kodlama: UTF-8 (BOM olabilir, olmayabilir).
/// Delimiter: virgül. Quote escape: yok (CSV'de tırnak geçen hücre yok).
/// </summary>
internal static class TurkeyAddressCsvReader
{
    /// <summary>
    /// CSV dosyasını satır satır okur ve structured row'lar olarak döner.
    /// Header satırı atlanır. Boş satırlar atlanır.
    ///
    /// Hatalı satırlar (kolon sayısı yanlış, sayı parse edilemiyor vs.) SESSİZCE atlanmaz —
    /// skippedRows listesinde döner, caller log'layabilir.
    /// </summary>
    public static (IReadOnlyList<TurkeyAddressRow> Rows, IReadOnlyList<string> SkippedRows) ReadAll(string csvFilePath)
    {
        if (!File.Exists(csvFilePath))
            throw new FileNotFoundException(
                $"CSV dosyası bulunamadı: {csvFilePath}", csvFilePath);

        var rows = new List<TurkeyAddressRow>();
        var skipped = new List<string>();
        var lineNumber = 0;

        using var reader = new StreamReader(csvFilePath, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

        while (reader.ReadLine() is { } line)
        {
            lineNumber++;
            if (lineNumber == 1) continue;   // header
            if (string.IsNullOrWhiteSpace(line)) continue;

            var parts = line.Split(',');
            if (parts.Length < 7)
            {
                skipped.Add($"Satır {lineNumber}: kolon sayısı yetersiz ({parts.Length}/7) — atlandı: {line}");
                continue;
            }

            try
            {
                var row = new TurkeyAddressRow(
                    IlId: int.Parse(parts[0].Trim(), CultureInfo.InvariantCulture),
                    IlAdi: CleanName(parts[1]),
                    IlceId: int.Parse(parts[2].Trim(), CultureInfo.InvariantCulture),
                    IlceAdi: CleanName(parts[3]),
                    SemtId: int.Parse(parts[4].Trim(), CultureInfo.InvariantCulture),
                    SemtAdi: CleanName(parts[5]),
                    PostaKodu: ParsePostalCode(parts[6]));
                rows.Add(row);
            }
            catch (Exception ex)
            {
                skipped.Add($"Satır {lineNumber}: parse hatası ({ex.Message}) — atlandı: {line}");
            }
        }

        return (rows, skipped);
    }

    /// <summary>
    /// CSV'deki "BÜYÜK HARF" adları "Title Case" Türkçe formatına çevirir.
    /// "İSTANBUL" → "İstanbul", "KÜÇÜKÇEKMECE" → "Küçükçekmece"
    /// NOT: Diakritikler korunur (Normalize çağrılmıyor) — mahalle/ilçe adları olduğu gibi kalmalı.
    /// </summary>
    private static string CleanName(string raw)
    {
        var trimmed = raw.Trim();
        if (string.IsNullOrEmpty(trimmed)) return trimmed;

        // Türkçe TextInfo ile önce lower, sonra title case (İ↔i, I↔ı doğru)
        var turkishCulture = CultureInfo.GetCultureInfo("tr-TR");
        var lower = trimmed.ToLower(turkishCulture);
        return turkishCulture.TextInfo.ToTitleCase(lower);
    }

    /// <summary>
    /// Posta kodu parse — geçersiz veya boş değer null döner.
    /// </summary>
    private static string? ParsePostalCode(string raw)
    {
        var trimmed = raw.Trim();
        if (string.IsNullOrEmpty(trimmed)) return null;
        if (trimmed.Length != 5 || !trimmed.All(char.IsDigit)) return null;
        return trimmed;
    }
}
