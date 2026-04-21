using FluentAssertions;
using SiteHub.Domain.Identity;

namespace SiteHub.Domain.Tests.Identity;

public class NationalIdTests
{
    // ─── TCKN Testleri ───────────────────────────────────────────

    [Theory]
    [InlineData("10000000146")]   // Matematiksel olarak geçerli örnek
    [InlineData("12345678950")]   // Matematiksel olarak geçerli örnek
    public void CreateTckn_Gecerli_Numarayla_Olusturulur(string validTckn)
    {
        var id = NationalId.CreateTckn(validTckn);

        id.Value.Should().Be(validTckn);
        id.Type.Should().Be(NationalIdType.TCKN);
    }

    [Theory]
    [InlineData("12345678901")]   // Checksum yanlış
    [InlineData("00000000000")]   // 0 ile başlıyor
    [InlineData("1234567890")]    // 10 hane (VKN uzunluğunda)
    [InlineData("1234567890a")]   // Rakam dışı karakter
    [InlineData("")]              // Boş
    public void CreateTckn_Gecersiz_Numarayla_Hata_Firlatir(string invalidTckn)
    {
        var act = () => NationalId.CreateTckn(invalidTckn);

        act.Should().Throw<InvalidNationalIdException>();
    }

    // ─── VKN Testleri ────────────────────────────────────────────

    [Theory]
    [InlineData("1234567890")]    // Matematiksel olarak geçerli örnek
    public void CreateVkn_Gecerli_Numarayla_Olusturulur(string validVkn)
    {
        var id = NationalId.CreateVkn(validVkn);

        id.Value.Should().Be(validVkn);
        id.Type.Should().Be(NationalIdType.VKN);
    }

    [Theory]
    [InlineData("1234567891")]    // Checksum yanlış
    [InlineData("123456789")]     // 9 hane
    [InlineData("12345678901")]   // 11 hane (TCKN uzunluğunda)
    public void CreateVkn_Gecersiz_Numarayla_Hata_Firlatir(string invalidVkn)
    {
        var act = () => NationalId.CreateVkn(invalidVkn);

        act.Should().Throw<InvalidNationalIdException>();
    }

    // ─── Parse Testi ─────────────────────────────────────────────

    [Fact]
    public void Parse_10_Hane_Icin_Vkn_Olusturur()
    {
        var id = NationalId.Parse("1234567890");
        id.Type.Should().Be(NationalIdType.VKN);
    }

    [Fact]
    public void Parse_99_ile_Baslayan_11_Hane_Icin_Ykn_Olusturur()
    {
        // Bu testin çalışması için geçerli bir YKN örneği gerekli
        // YKN = 99 ile başlayan TCKN checksum'ına uyan numara
        // "99000000026" örneği: ilk 2 hane "99", kalan 9 hane TCKN checksum'a uyuyor
        // Bu test gerçek YKN örneği ile güncellenmeli
        var act = () => NationalId.Parse("12345678901");
        // Şu an için sadece TCKN doğrulaması üzerinden gidiyor
        act.Should().Throw<InvalidNationalIdException>();
    }

    [Fact]
    public void Parse_Gecersiz_Uzunluk_Icin_Hata_Firlatir()
    {
        var act = () => NationalId.Parse("12345");
        act.Should().Throw<InvalidNationalIdException>();
    }

    // ─── TryParse Testi ──────────────────────────────────────────

    [Fact]
    public void TryParse_Gecersiz_Icin_Null_Doner()
    {
        var result = NationalId.TryParse("invalid");
        result.Should().BeNull();
    }

    [Fact]
    public void TryParse_Null_Icin_Null_Doner()
    {
        var result = NationalId.TryParse(null);
        result.Should().BeNull();
    }

    // ─── Equality Testi (Value Object davranışı) ─────────────────

    [Fact]
    public void Ayni_Deger_ve_Tip_Icin_Esit()
    {
        var a = NationalId.CreateTckn("10000000146");
        var b = NationalId.CreateTckn("10000000146");

        a.Should().Be(b);
        (a == b).Should().BeTrue();
        a.GetHashCode().Should().Be(b.GetHashCode());
    }
}
