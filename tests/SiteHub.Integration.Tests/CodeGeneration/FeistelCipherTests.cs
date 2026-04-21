using System.Security.Cryptography;
using FluentAssertions;
using SiteHub.Infrastructure.CodeGeneration;

namespace SiteHub.Integration.Tests.CodeGeneration;

public sealed class FeistelCipherTests
{
    // Test key — 32 byte, sabit (deterministic tests için)
    private static readonly byte[] TestKey = SHA256.HashData(
        System.Text.Encoding.UTF8.GetBytes("test-key-for-feistel"));

    [Fact]
    public void Encrypt_SameInputAndKey_ProducesSameOutput()
    {
        // Deterministic: aynı input + aynı key → aynı output
        var output1 = FeistelCipher.Encrypt(input: 42, bits: 20, key: TestKey);
        var output2 = FeistelCipher.Encrypt(input: 42, bits: 20, key: TestKey);

        output1.Should().Be(output2);
    }

    [Fact]
    public void Encrypt_DifferentInputs_ProduceDifferentOutputs()
    {
        // Bijection: farklı input → farklı output
        var output1 = FeistelCipher.Encrypt(input: 1, bits: 20, key: TestKey);
        var output2 = FeistelCipher.Encrypt(input: 2, bits: 20, key: TestKey);

        output1.Should().NotBe(output2);
    }

    [Fact]
    public void Encrypt_DifferentKeys_ProduceDifferentOutputs()
    {
        var key2 = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("different-key"));

        var output1 = FeistelCipher.Encrypt(input: 42, bits: 20, key: TestKey);
        var output2 = FeistelCipher.Encrypt(input: 42, bits: 20, key: key2);

        output1.Should().NotBe(output2);
    }

    [Fact]
    public void Encrypt_OutputStaysWithinBitRange()
    {
        // 20-bit Feistel → output < 2^20 = 1,048,576
        for (long i = 0; i < 1000; i++)
        {
            var output = FeistelCipher.Encrypt(i, bits: 20, key: TestKey);
            output.Should().BeLessThan(1L << 20);
            output.Should().BeGreaterThanOrEqualTo(0);
        }
    }

    [Fact]
    public void Encrypt_InvalidBits_Throws()
    {
        // Tek sayı — kabul edilmez
        Action act1 = () => FeistelCipher.Encrypt(input: 1, bits: 21, key: TestKey);
        act1.Should().Throw<ArgumentException>();

        // Çok küçük
        Action act2 = () => FeistelCipher.Encrypt(input: 1, bits: 2, key: TestKey);
        act2.Should().Throw<ArgumentException>();

        // Çok büyük
        Action act3 = () => FeistelCipher.Encrypt(input: 1, bits: 64, key: TestKey);
        act3.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Encrypt_ShortKey_Throws()
    {
        var shortKey = new byte[15]; // 15 byte < 16 minimum

        Action act = () => FeistelCipher.Encrypt(input: 1, bits: 20, key: shortKey);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Encrypt_InputOutOfRange_Throws()
    {
        Action act1 = () => FeistelCipher.Encrypt(input: -1, bits: 20, key: TestKey);
        act1.Should().Throw<ArgumentOutOfRangeException>();

        // 20-bit için maksimum değer 2^20 - 1
        Action act2 = () => FeistelCipher.Encrypt(input: 1L << 20, bits: 20, key: TestKey);
        act2.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Encrypt_Bijection_NoCollisions_Over10000Inputs()
    {
        // Bijection garantisi: 10000 farklı input → 10000 farklı output
        var outputs = new HashSet<long>();

        for (long i = 0; i < 10_000; i++)
        {
            var output = FeistelCipher.Encrypt(i, bits: 20, key: TestKey);
            outputs.Add(output).Should().BeTrue(
                $"Çakışma bulundu! Input {i}, output {output} daha önce üretilmişti.");
        }

        outputs.Count.Should().Be(10_000);
    }

    [Fact]
    public void EncryptToRange_Output_InCorrectRange()
    {
        // Site için: 100001-999999 (900K slot), 20-bit Feistel
        const long minValue = 100_001;
        const long maxValue = 999_999;
        const long slotCount = 900_000;
        const int bits = 20;

        for (long i = 0; i < 1000; i++)
        {
            var code = FeistelCipher.EncryptToRange(i, slotCount, bits, TestKey, minValue);

            code.Should().BeGreaterThanOrEqualTo(minValue);
            code.Should().BeLessThanOrEqualTo(maxValue);
        }
    }

    [Fact]
    public void EncryptToRange_NoCollisionsWithin900K()
    {
        // 900K slot üzerinden 100K sample — collision olmamalı
        const long slotCount = 900_000;
        const int bits = 20;
        const long minValue = 100_001;

        var codes = new HashSet<long>();

        for (long i = 0; i < 100_000; i++)
        {
            var code = FeistelCipher.EncryptToRange(i, slotCount, bits, TestKey, minValue);
            codes.Add(code).Should().BeTrue(
                $"Çakışma! Input {i}, code {code} daha önce üretilmişti.");
        }

        codes.Count.Should().Be(100_000);
    }

    [Fact]
    public void EncryptToRange_LooksRandom_NotSequential()
    {
        // Sıralı input → output sıralı olmamalı (obfuscation çalışıyor)
        var code1 = FeistelCipher.EncryptToRange(0, 900_000, 20, TestKey, 100_001);
        var code2 = FeistelCipher.EncryptToRange(1, 900_000, 20, TestKey, 100_001);
        var code3 = FeistelCipher.EncryptToRange(2, 900_000, 20, TestKey, 100_001);

        // Sıralı output gelseydi fark 1 olurdu — Feistel'de farklı olmalı
        (code2 - code1).Should().NotBe(1);
        (code3 - code2).Should().NotBe(1);
    }
}
