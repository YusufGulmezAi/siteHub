using SiteHub.Domain.Common;

namespace SiteHub.Domain.Identity;

/// <summary>
/// Ulusal Kimlik Numarası — TCKN / VKN / YKN
///
/// Bu bir VALUE OBJECT'tir:
///  - Değişmez (immutable): oluşturulduktan sonra değiştirilemez
///  - Kendi kendini doğrular: geçersiz bir numara ile oluşturulamaz
///  - Domain'in kalbidir: "bir kullanıcının TCKN'si" kavramı burada yaşar
///
/// Kullanım:
///   var tc = NationalId.CreateTckn("12345678901");
///   var vkn = NationalId.CreateVkn("1234567890");
///
/// Hatalı numara denemesi InvalidNationalIdException fırlatır.
/// Fırlatmadan doğrulamak için: NationalId.TryCreate(...).
/// </summary>
public sealed class NationalId : ValueObject
{
    public string Value { get; }
    public NationalIdType Type { get; }

    private NationalId(string value, NationalIdType type)
    {
        Value = value;
        Type = type;
    }

    // ─────────────────────────────────────────────────────────────
    // Factory Methods
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 11 haneli TCKN oluşturur. Checksum doğrulanır.
    /// </summary>
    public static NationalId CreateTckn(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var clean = value.Trim();
        if (!IsValidTckn(clean))
            throw new InvalidNationalIdException($"Geçersiz TCKN: {clean}");
        return new NationalId(clean, NationalIdType.TCKN);
    }

    /// <summary>
    /// 10 haneli VKN oluşturur. Checksum doğrulanır.
    /// </summary>
    public static NationalId CreateVkn(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var clean = value.Trim();
        if (!IsValidVkn(clean))
            throw new InvalidNationalIdException($"Geçersiz VKN: {clean}");
        return new NationalId(clean, NationalIdType.VKN);
    }

    /// <summary>
    /// 10 haneli VKN oluşturur — <b>checksum doğrulamadan</b>. Yalnızca:
    /// <list type="bullet">
    ///   <item>10 hane uzunluk</item>
    ///   <item>Yalnızca rakam</item>
    ///   <item>Tamamı sıfır olmamalı</item>
    /// </list>
    /// <para>
    /// Gelir İdaresi VKN checksum algoritması ilerde (banka entegrasyonu
    /// fazında) tekrar açılabilir. Şu an dev test kolaylığı için rastgele
    /// 10 hane kabul eder. Gerçek VKN doğrulaması banka/devlet tarafında
    /// zaten tekrar yapılacağı için UI fazında sıkı kontrol ertelendi.
    /// </para>
    /// <para>
    /// <c>CreateVkn</c> mevcut (Site.cs + Identity için checksum'lı). Bu
    /// yeni metot <b>sadece Organization oluşturma/güncelleme</b> akışında
    /// kullanılır.
    /// </para>
    /// </summary>
    public static NationalId CreateVknRelaxed(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var clean = value.Trim();

        if (clean.Length != 10)
            throw new InvalidNationalIdException(
                $"VKN 10 haneli olmalıdır: {clean}");

        if (!clean.All(char.IsDigit))
            throw new InvalidNationalIdException(
                $"VKN sadece rakam içermelidir: {clean}");

        if (clean.All(c => c == '0'))
            throw new InvalidNationalIdException(
                "VKN tamamen sıfır olamaz.");

        return new NationalId(clean, NationalIdType.VKN);
    }

    /// <summary>
    /// 11 haneli YKN oluşturur. 99 ile başlar ve TCKN checksum'ı uygular.
    /// </summary>
    public static NationalId CreateYkn(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var clean = value.Trim();
        if (!IsValidYkn(clean))
            throw new InvalidNationalIdException($"Geçersiz YKN: {clean}");
        return new NationalId(clean, NationalIdType.YKN);
    }

    /// <summary>
    /// Numaranın uzunluğundan tip tahmin eder ve doğrular.
    /// Kullanıcı arayüzünde "TCKN/VKN fark etmeksizin" input alırken kullanılır.
    /// </summary>
    public static NationalId Parse(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var clean = value.Trim();

        return clean.Length switch
        {
            11 when clean.StartsWith("99", StringComparison.Ordinal) => CreateYkn(clean),
            11                              => CreateTckn(clean),
            10                              => CreateVkn(clean),
            _ => throw new InvalidNationalIdException(
                $"Ulusal kimlik numarası 10 hane (VKN) veya 11 hane (TCKN/YKN) olmalıdır: {clean}")
        };
    }

    /// <summary>
    /// İstisna fırlatmadan dener. Başarısızsa null döner.
    /// Form doğrulamada fırlat-yakala maliyetinden kaçınmak için.
    /// </summary>
    public static NationalId? TryParse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        try { return Parse(value); }
        catch (InvalidNationalIdException) { return null; }
    }

    // ─────────────────────────────────────────────────────────────
    // Validation Logic (checksum algoritmaları)
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// TCKN algoritması (NVİ resmi kuralı):
    /// 1. 11 hane, hepsi rakam
    /// 2. İlk hane 0 olamaz
    /// 3. (1.+3.+5.+7.+9.) * 7 - (2.+4.+6.+8.) → mod 10 = 10. hane
    /// 4. İlk 10 hanenin toplamı mod 10 = 11. hane
    /// </summary>
    public static bool IsValidTckn(string value)
    {
        if (value.Length != 11) return false;
        if (!value.All(char.IsDigit)) return false;
        if (value[0] == '0') return false;

        var d = new int[11];
        for (int i = 0; i < 11; i++) d[i] = value[i] - '0';

        int oddSum = d[0] + d[2] + d[4] + d[6] + d[8];
        int evenSum = d[1] + d[3] + d[5] + d[7];

        int checksum10 = ((oddSum * 7) - evenSum) % 10;
        if (checksum10 < 0) checksum10 += 10;
        if (checksum10 != d[9]) return false;

        int sumFirstTen = 0;
        for (int i = 0; i < 10; i++) sumFirstTen += d[i];
        int checksum11 = sumFirstTen % 10;
        if (checksum11 != d[10]) return false;

        return true;
    }

    /// <summary>
    /// VKN algoritması (Gelir İdaresi Başkanlığı kuralı):
    /// 10 haneli, her hane için özel ağırlıklandırma
    /// </summary>
    public static bool IsValidVkn(string value)
    {
        if (value.Length != 10) return false;
        if (!value.All(char.IsDigit)) return false;

        var d = new int[10];
        for (int i = 0; i < 10; i++) d[i] = value[i] - '0';

        long sum = 0;
        for (int i = 0; i < 9; i++)
        {
            int tmp = (d[i] + (9 - i)) % 10;
            sum += tmp == 0
                ? tmp
                : (tmp * (int)Math.Pow(2, 9 - i)) % 9 == 0
                    ? 9
                    : (tmp * (int)Math.Pow(2, 9 - i)) % 9;
        }

        int checkDigit = (int)((10 - (sum % 10)) % 10);
        return checkDigit == d[9];
    }

    /// <summary>
    /// YKN: 11 haneli, 99 ile başlar, TCKN gibi checksum doğrulanır
    /// </summary>
    public static bool IsValidYkn(string value)
    {
        if (value.Length != 11) return false;
        if (!value.StartsWith("99", StringComparison.Ordinal)) return false;
        return IsValidTckn(value);
    }

    // ─────────────────────────────────────────────────────────────
    // Overrides
    // ─────────────────────────────────────────────────────────────

    public override string ToString() => Value;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
        yield return Type;
    }

    // EF Core / serialization için implicit string conversion
    public static implicit operator string(NationalId id) => id.Value;
}

/// <summary>
/// Geçersiz ulusal kimlik numarası hatası.
/// </summary>
public sealed class InvalidNationalIdException(string message) : Exception(message);
