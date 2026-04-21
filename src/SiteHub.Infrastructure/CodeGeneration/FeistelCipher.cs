using System.Security.Cryptography;

namespace SiteHub.Infrastructure.CodeGeneration;

/// <summary>
/// Feistel network cipher — sıralı sayıyı görünüşte rastgele bir sayıya
/// çevirir (ADR-0012 §11.2).
///
/// Garantiler:
/// - Bijection: farklı input → farklı output (hiçbir çakışma yok)
/// - Reversible: encrypt(decrypt(x)) == x (ama biz sadece encrypt kullanıyoruz)
/// - Key-parametrize: aynı input + aynı key → aynı output (deterministic)
///
/// Amaç: crypto-grade güvenlik DEĞİL (AES için değil bu). Sadece "kullanıcı
/// tahmin edemesin" yeter. 4 round yeterince obfuscation sağlar (§11.4).
///
/// Bit genişliği: her zaman ÇİFT (2n bit) olmalı — sol/sağ yarı eşit.
/// Örn. 20-bit için 2×10, 24-bit için 2×12, 30-bit için 2×15.
/// </summary>
public static class FeistelCipher
{
    private const int Rounds = 4;

    /// <summary>
    /// N-bit girdiyi Feistel network'üyle obfuscate eder.
    /// </summary>
    /// <param name="input">Obfuscate edilecek sayı — [0, 2^bits) aralığında.</param>
    /// <param name="bits">Toplam bit genişliği. Çift olmalı (4, 6, 8, ..., 30, 32).</param>
    /// <param name="key">Feistel key. Minimum 16 byte (128-bit). Her entity için ayrı key.</param>
    /// <returns>Obfuscated değer — [0, 2^bits) aralığında.</returns>
    public static long Encrypt(long input, int bits, byte[] key)
    {
        if (bits < 4 || bits > 62 || bits % 2 != 0)
            throw new ArgumentException("Bits 4-62 arası ÇİFT sayı olmalı.", nameof(bits));

        if (key is null || key.Length < 16)
            throw new ArgumentException("Key minimum 16 byte olmalı.", nameof(key));

        var halfBits = bits / 2;
        var halfMask = (1L << halfBits) - 1L;     // alt yarı için mask
        var fullMask = (1L << bits) - 1L;

        if (input < 0 || input > fullMask)
            throw new ArgumentOutOfRangeException(nameof(input),
                $"Input [0, 2^{bits}) aralığında olmalı. Maks: {fullMask}");

        // Girdi [L | R] olarak ayrılır
        long left  = (input >> halfBits) & halfMask;
        long right = input & halfMask;

        // N round Feistel
        for (int round = 0; round < Rounds; round++)
        {
            var roundOutput = RoundFunction(right, round, key, halfBits);
            var newLeft  = right;
            var newRight = left ^ roundOutput;
            left  = newLeft;
            right = newRight;
        }

        return (left << halfBits) | right;
    }

    /// <summary>
    /// Feistel round function F(R, roundKey) → N-bit değer.
    /// HMAC-SHA256 kullanılır — hızlı, rastgele görünümlü çıktı.
    /// </summary>
    private static long RoundFunction(long rightHalf, int roundIndex, byte[] key, int halfBits)
    {
        // Round input = right half + round index (farklı round'larda farklı çıktı)
        Span<byte> input = stackalloc byte[sizeof(long) + sizeof(int)];
        BitConverter.TryWriteBytes(input[..sizeof(long)], rightHalf);
        BitConverter.TryWriteBytes(input[sizeof(long)..], roundIndex);

        Span<byte> hash = stackalloc byte[32]; // SHA-256 = 32 byte
        HMACSHA256.HashData(key, input, hash);

        // İlk 8 byte'ı long'a çevir, halfBits mask uygula
        long raw = BitConverter.ToInt64(hash[..sizeof(long)]);
        long mask = (1L << halfBits) - 1L;
        return raw & mask;
    }

    /// <summary>
    /// Cycle walking: Feistel çıktısı hedef aralıktan büyükse tekrar
    /// encrypt edilir. Aralık normalizasyonunda modulo bias'ı önler
    /// (§11.5). Ortalama iteration: 1-2, worst case: nadir.
    /// </summary>
    /// <param name="input">Sequence'den gelen değer [0, slotCount).</param>
    /// <param name="slotCount">Hedef slot sayısı (örn. 900,000 site için).</param>
    /// <param name="bits">Feistel bit genişliği — slotCount'u kapsamalı.</param>
    /// <param name="key">Feistel key.</param>
    /// <param name="minValue">Sonuca eklenecek taban değer (örn. 100001).</param>
    public static long EncryptToRange(long input, long slotCount, int bits, byte[] key, long minValue)
    {
        if (slotCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(slotCount), "Pozitif olmalı.");

        long fullSpace = 1L << bits;
        if (slotCount > fullSpace)
            throw new ArgumentException(
                $"slotCount ({slotCount}) feistel bit space'inden ({fullSpace}) büyük olamaz.",
                nameof(slotCount));

        // Input slotCount içinde olmalı
        long current = input % slotCount;

        // Cycle walking: Feistel output > slotCount ise tekrar encrypt
        // Güvenlik ağı: max iteration (sonsuz döngü koruması)
        const int maxIterations = 1000;
        for (int i = 0; i < maxIterations; i++)
        {
            current = Encrypt(current, bits, key);
            if (current < slotCount)
                return current + minValue;
        }

        throw new InvalidOperationException(
            $"Cycle walking {maxIterations} iteration'da dönmedi — " +
            "Feistel distribution'ında sorun olabilir.");
    }
}
