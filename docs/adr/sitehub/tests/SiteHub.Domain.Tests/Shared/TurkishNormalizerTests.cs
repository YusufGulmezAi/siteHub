using SiteHub.Domain.Text;

namespace SiteHub.Domain.Tests.Shared;

public class TurkishNormalizerTests
{
    [Theory]
    [InlineData("Şişli", "şişli")]
    [InlineData("ŞİŞLİ", "şişli")]
    [InlineData("şişli", "şişli")]
    [InlineData("ŞiŞLi", "şişli")]
    public void Normalize_Turkce_Buyuk_Kucuk_Harf_Doğru_Ceviriyor(string input, string expected)
    {
        TurkishNormalizer.Normalize(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("I", "ı")]       // Büyük I → küçük ı (Türkçe)
    [InlineData("İ", "i")]       // Büyük İ → küçük i (Türkçe)
    [InlineData("Istanbul", "ıstanbul")]
    [InlineData("İstanbul", "istanbul")]
    public void Normalize_Turkce_I_Kurallari_Uyguluyor(string input, string expected)
    {
        TurkishNormalizer.Normalize(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("  Şişli  ", "şişli")]                  // baş/son trim
    [InlineData("Şişli  Yönetim", "şişli yönetim")]     // iç çift boşluk
    [InlineData("  Ataşehir   Kiracı  A.Ş.  ", "ataşehir kiracı a.ş.")]
    public void Normalize_Bosluklari_Temizliyor(string input, string expected)
    {
        TurkishNormalizer.Normalize(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Normalize_Null_veya_Bos_Icin_Bos_Doner(string? input)
    {
        TurkishNormalizer.Normalize(input).Should().Be(string.Empty);
    }

    [Fact]
    public void Combine_Birden_Fazla_Alani_Normalize_ile_Birlestirir()
    {
        var result = TurkishNormalizer.Combine("Şişli Yönetim", "ŞİŞLİ A.Ş.", "0212 555 1234");

        result.Should().Be("şişli yönetim şişli a.ş. 0212 555 1234");
    }

    [Fact]
    public void Combine_Null_ve_Bos_Alanlari_Atlar()
    {
        var result = TurkishNormalizer.Combine("Şişli", null, "", "  ", "Yönetim");

        result.Should().Be("şişli yönetim");
    }
}
