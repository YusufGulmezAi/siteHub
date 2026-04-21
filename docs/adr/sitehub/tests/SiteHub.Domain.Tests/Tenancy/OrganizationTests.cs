using SiteHub.Domain.Identity;
using SiteHub.Domain.Tenancy.Organizations;

namespace SiteHub.Domain.Tests.Tenancy;

public class OrganizationTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse(
        "2026-04-19T10:00:00+03:00",
        System.Globalization.CultureInfo.InvariantCulture);

    // ─── Create ──────────────────────────────────────────────────────────

    [Fact]
    public void Create_Gecerli_Parametrelerle_Olusturulur()
    {
        var org = Organization.Create("ABC Yönetim", "ABC Yönetim Hizmetleri A.Ş.", null);

        org.Name.Should().Be("ABC Yönetim");
        org.CommercialTitle.Should().Be("ABC Yönetim Hizmetleri A.Ş.");
        org.TaxId.Should().BeNull();
        org.IsActive.Should().BeTrue();
        org.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public void Create_SearchText_Normalize_Edilmis_Olusturur()
    {
        var org = Organization.Create("Şişli YÖNETİM", "ŞİŞLİ Yönetim A.Ş.", null);

        org.SearchText.Should().Contain("şişli yönetim");
        org.SearchText.Should().Contain("şişli yönetim a.ş.");
        // Türkçe kültürde İ → i olmalı (ToLower(tr-TR))
        org.SearchText.Should().NotContain("ŞİŞLİ");
        org.SearchText.Should().NotContain("YÖNETİM");
    }

    [Fact]
    public void Create_VKN_ile_Olusturulur_SearchText_VKN_Icerir()
    {
        var vkn = NationalId.CreateVkn("1234567890");
        var org = Organization.Create("ABC", "ABC A.Ş.", vkn);

        org.TaxId.Should().Be(vkn);
        org.SearchText.Should().Contain("1234567890"); // VKN aranabilir
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_Bos_Ad_Kabul_Etmez(string badName)
    {
        var act = () => Organization.Create(badName, "Uzun Unvan A.Ş.", null);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_TCKN_Kimlik_Kabul_Etmez()
    {
        var tckn = NationalId.CreateTckn("10000000146");
        var act = () => Organization.Create("ABC", "ABC A.Ş.", tckn);
        act.Should().Throw<ArgumentException>().WithMessage("*VKN*");
    }

    // ─── Rename → SearchText güncellenir ─────────────────────────────────

    [Fact]
    public void Rename_SearchText_Yeni_Adla_Guncellenir()
    {
        var org = Organization.Create("Eski Ad", "Eski Unvan A.Ş.", null);
        org.SearchText.Should().Contain("eski ad");

        org.Rename("Ataşehir Yönetim", "Ataşehir Yönetim A.Ş.");

        org.SearchText.Should().Contain("ataşehir yönetim");
        org.SearchText.Should().NotContain("eski ad");
    }

    // ─── UpdateContact → SearchText e-posta/telefon içerir ─────────────

    [Fact]
    public void UpdateContact_SearchText_Email_ve_Telefonu_Icerir()
    {
        var org = Organization.Create("ABC", "ABC A.Ş.", null);

        org.UpdateContact("İstanbul, Şişli", "0212 555 1234", "info@abc.com.tr");

        org.SearchText.Should().Contain("istanbul");
        org.SearchText.Should().Contain("info@abc.com.tr");
        org.SearchText.Should().Contain("0212 555 1234");
    }

    // ─── Soft Delete ─────────────────────────────────────────────────────

    [Fact]
    public void SoftDelete_Silinmis_Isaretler()
    {
        var org = Organization.Create("ABC", "ABC A.Ş.", null);

        org.SoftDelete("Kiracı aboneliğini iptal etti", Now);

        org.IsDeleted.Should().BeTrue();
        org.DeletedAt.Should().Be(Now);
        org.DeleteReason.Should().Be("Kiracı aboneliğini iptal etti");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void SoftDelete_Bos_Sebep_Kabul_Etmez(string reason)
    {
        var org = Organization.Create("ABC", "ABC A.Ş.", null);
        var act = () => org.SoftDelete(reason, Now);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SoftDelete_Zaten_Silinmis_Hata_Firlatir()
    {
        var org = Organization.Create("ABC", "ABC A.Ş.", null);
        org.SoftDelete("ilk", Now);

        var act = () => org.SoftDelete("ikinci", Now);
        act.Should().Throw<InvalidOperationException>();
    }

    // ─── Restore ─────────────────────────────────────────────────────────

    [Fact]
    public void Restore_Silinmis_Organizasyonu_Geri_Alir()
    {
        var org = Organization.Create("ABC", "ABC A.Ş.", null);
        org.SoftDelete("yanlışlıkla", Now);

        org.Restore("hata düzeltiliyor", Now.AddMinutes(5));

        org.IsDeleted.Should().BeFalse();
        org.DeletedAt.Should().BeNull();
        org.DeleteReason.Should().BeNull();
    }
}
