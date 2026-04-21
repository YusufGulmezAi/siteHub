using FluentAssertions;
using SiteHub.Application.Abstractions.Authentication;
using Xunit;

namespace SiteHub.Application.Tests.Authentication;

public class LoginInputParserTests
{
    [Theory]
    [InlineData("12345678901", LoginInputType.Tckn)]           // 11 hane, 0 ile başlamıyor
    [InlineData("99999999999", LoginInputType.Ykn)]            // 99 ile başlar
    [InlineData("1234567890", LoginInputType.Vkn)]             // 10 hane
    [InlineData("ahmet@ornek.com", LoginInputType.Email)]
    [InlineData("AHMET@ORNEK.COM", LoginInputType.Email)]
    [InlineData("+90 532 123 45 67", LoginInputType.Mobile)]
    [InlineData("+905321234567", LoginInputType.Mobile)]
    [InlineData("0532 123 45 67", LoginInputType.Mobile)]
    [InlineData("0-532-123-45-67", LoginInputType.Mobile)]
    [InlineData("", LoginInputType.Unknown)]
    [InlineData("   ", LoginInputType.Unknown)]
    [InlineData("abc", LoginInputType.Unknown)]
    [InlineData("@ornek.com", LoginInputType.Unknown)]         // Local part eksik
    [InlineData("a@b", LoginInputType.Unknown)]                // Domain'de nokta yok
    [InlineData("01234567890", LoginInputType.Unknown)]        // TCKN 0 ile başlayamaz
    [InlineData("12345", LoginInputType.Unknown)]              // Kısa sayı
    public void Detect_ReturnsExpectedType(string input, LoginInputType expected)
    {
        LoginInputParser.Detect(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("Ahmet@ORNEK.Com", LoginInputType.Email, "ahmet@ornek.com")]
    [InlineData("+90 (532) 123 45 67", LoginInputType.Mobile, "+905321234567")]
    [InlineData("0532 123 4567", LoginInputType.Mobile, "+905321234567")]
    [InlineData("905321234567", LoginInputType.Mobile, "+905321234567")]
    [InlineData("12345678901", LoginInputType.Tckn, "12345678901")]
    public void Normalize_ProducesExpectedForm(string input, LoginInputType type, string expected)
    {
        LoginInputParser.Normalize(input, type).Should().Be(expected);
    }

    [Fact]
    public void Detect_WhitespaceEdges_Trimmed()
    {
        LoginInputParser.Detect("  12345678901  ").Should().Be(LoginInputType.Tckn);
        LoginInputParser.Detect("  ahmet@ornek.com  ").Should().Be(LoginInputType.Email);
    }
}
