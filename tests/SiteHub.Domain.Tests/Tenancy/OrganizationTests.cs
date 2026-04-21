using SiteHub.Domain.Identity;
using SiteHub.Domain.Tenancy.Organizations;

namespace SiteHub.Domain.Tests.Tenancy;

public class OrganizationTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse(
        "2026-04-19T10:00:00+03:00",
        System.Globalization.CultureInfo.InvariantCulture);

    // Test sabit — geçerli 10 haneli VKN (checksum doğru).
    private static NationalId ValidVkn() => NationalId.CreateVkn("1234567890");
    private static NationalId AnotherVkn() => NationalId.CreateVkn("9876543217");

    // Test sabit — geçerli 6 haneli Feistel kod (aralık 100001-999999)
    private const long ValidCode = 123456;

    // ─── Create ──────────────────────────────────────────────────────────

    [Fact]
    public void Create_Gecerli_Parametrelerle_Olusturulur()
    {
        var org = Organization.Create(ValidCode, "ABC Yönetim", "ABC Yönetim Hizmetleri A.Ş.", ValidVkn());

        org.Code.Should().Be(ValidCode);
        org.Name.Should().Be("ABC Yönetim");
        org.CommercialTitle.Should().Be("ABC Yönetim Hizmetleri A.Ş.");
        org.TaxId.Value.Should().Be("1234567890");
        org.TaxId.Type.Should().Be(NationalIdType.VKN);
        org.IsActive.Should().BeTrue();
        org.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public void Create_SearchText_Normalize_Edilmis_Olusturur()
    {
        var org = Organization.Create(ValidCode, "Şişli YÖNETİM", "ŞİŞLİ Yönetim A.Ş.", ValidVkn());

        org.SearchText.Should().Contain("şişli yönetim");
        org.SearchText.Should().Contain("şişli yönetim a.ş.");
        // Türkçe kültürde İ → i olmalı (ToLower(tr-TR))
        org.SearchText.Should().NotContain("ŞİŞLİ");
        org.SearchText.Should().NotContain("YÖNETİM");
    }

    [Fact]
    public void Create_SearchText_Code_ve_VKN_Icerir()
    {
        var org = Organization.Create(ValidCode, "ABC", "ABC A.Ş.", ValidVkn());

        org.SearchText.Should().Contain(ValidCode.ToString()); // Kod aranabilir
        org.SearchText.Should().Contain("1234567890"); // VKN aranabilir
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_Bos_Ad_Kabul_Etmez(string badName)
    {
        var act = () => Organization.Create(ValidCode, badName, "Uzun Unvan A.Ş.", ValidVkn());
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_Bos_CommercialTitle_Kabul_Etmez(string badTitle)
    {
        var act = () => Organization.Create(ValidCode, "ABC", badTitle, ValidVkn());
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_TCKN_Kimlik_Kabul_Etmez()
    {
        var tckn = NationalId.CreateTckn("10000000146");
        var act = () => Organization.Create(ValidCode, "ABC", "ABC A.Ş.", tckn);
        act.Should().Throw<ArgumentException>().WithMessage("*VKN*");
    }

    [Fact]
    public void Create_Null_TaxId_Kabul_Etmez()
    {
        var act = () => Organization.Create(ValidCode, "ABC", "ABC A.Ş.", null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100_000)]      // sınırın altı
    [InlineData(1_000_000)]    // sınırın üstü
    [InlineData(-1)]
    public void Create_Gecersiz_Kod_Kabul_Etmez(long badCode)
    {
        var act = () => Organization.Create(badCode, "ABC", "ABC A.Ş.", ValidVkn());
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(100_001)]  // alt sınır
    [InlineData(999_999)]  // üst sınır
    [InlineData(500_000)]  // orta
    public void Create_Gecerli_Kod_Aralik_Kabul_Eder(long goodCode)
    {
        var org = Organization.Create(goodCode, "ABC", "ABC A.Ş.", ValidVkn());
        org.Code.Should().Be(goodCode);
    }

    [Fact]
    public void Create_Ad_200_Karakterden_Uzun_Olamaz()
    {
        var longName = new string('a', 201);
        var act = () => Organization.Create(ValidCode, longName, "Unvan", ValidVkn());
        act.Should().Throw<ArgumentException>().WithMessage("*200*");
    }

    [Fact]
    public void Create_Unvan_500_Karakterden_Uzun_Olamaz()
    {
        var longTitle = new string('a', 501);
        var act = () => Organization.Create(ValidCode, "ABC", longTitle, ValidVkn());
        act.Should().Throw<ArgumentException>().WithMessage("*500*");
    }

    // ─── Rename → SearchText güncellenir ─────────────────────────────────

    [Fact]
    public void Rename_SearchText_Yeni_Adla_Guncellenir()
    {
        var org = Organization.Create(ValidCode, "Eski Ad", "Eski Unvan A.Ş.", ValidVkn());
        org.SearchText.Should().Contain("eski ad");

        org.Rename("Ataşehir Yönetim", "Ataşehir Yönetim A.Ş.");

        org.SearchText.Should().Contain("ataşehir yönetim");
        org.SearchText.Should().NotContain("eski ad");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rename_Bos_Ad_Kabul_Etmez(string badName)
    {
        var org = Organization.Create(ValidCode, "ABC", "ABC A.Ş.", ValidVkn());
        var act = () => org.Rename(badName, "Yeni Unvan");
        act.Should().Throw<ArgumentException>();
    }

    // ─── UpdateContact ───────────────────────────────────────────────────

    [Fact]
    public void UpdateContact_SearchText_Email_ve_Telefonu_Icerir()
    {
        var org = Organization.Create(ValidCode, "ABC", "ABC A.Ş.", ValidVkn());

        org.UpdateContact("İstanbul, Şişli", "0212 555 1234", "info@abc.com.tr");

        org.SearchText.Should().Contain("istanbul");
        org.SearchText.Should().Contain("info@abc.com.tr");
        org.SearchText.Should().Contain("0212 555 1234");
    }

    [Fact]
    public void UpdateContact_Bos_Deger_Null_Olarak_Kaydeder()
    {
        var org = Organization.Create(ValidCode, "ABC", "ABC A.Ş.", ValidVkn());

        org.UpdateContact("   ", "", null);

        org.Address.Should().BeNull();
        org.Phone.Should().BeNull();
        org.Email.Should().BeNull();
    }

    // ─── ChangeTaxId ─────────────────────────────────────────────────────

    [Fact]
    public void ChangeTaxId_Yeni_VKN_Atar()
    {
        var org = Organization.Create(ValidCode, "ABC", "ABC A.Ş.", ValidVkn());
        var newVkn = AnotherVkn();

        org.ChangeTaxId(newVkn);

        org.TaxId.Should().Be(newVkn);
        org.SearchText.Should().Contain("9876543217");
        org.SearchText.Should().NotContain("1234567890");
    }

    [Fact]
    public void ChangeTaxId_TCKN_Kabul_Etmez()
    {
        var org = Organization.Create(ValidCode, "ABC", "ABC A.Ş.", ValidVkn());
        var tckn = NationalId.CreateTckn("10000000146");

        var act = () => org.ChangeTaxId(tckn);
        act.Should().Throw<ArgumentException>().WithMessage("*VKN*");
    }

    [Fact]
    public void ChangeTaxId_Null_Kabul_Etmez()
    {
        var org = Organization.Create(ValidCode, "ABC", "ABC A.Ş.", ValidVkn());
        var act = () => org.ChangeTaxId(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    // ─── Activate / Deactivate ──────────────────────────────────────────

    [Fact]
    public void Deactivate_IsActive_False_Yapar()
    {
        var org = Organization.Create(ValidCode, "ABC", "ABC A.Ş.", ValidVkn());
        org.IsActive.Should().BeTrue();

        org.Deactivate();

        org.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Activate_Zaten_Aktif_Olani_Etkilemez_Idempotent()
    {
        var org = Organization.Create(ValidCode, "ABC", "ABC A.Ş.", ValidVkn());

        org.Activate(); // zaten aktif — no-op
        org.Activate(); // tekrar — no-op

        org.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Deactivate_Zaten_Pasif_Olani_Etkilemez_Idempotent()
    {
        var org = Organization.Create(ValidCode, "ABC", "ABC A.Ş.", ValidVkn());
        org.Deactivate();

        org.Deactivate(); // tekrar — no-op

        org.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Deactivate_Sonra_Activate_Geri_Alir()
    {
        var org = Organization.Create(ValidCode, "ABC", "ABC A.Ş.", ValidVkn());
        org.Deactivate();
        org.IsActive.Should().BeFalse();

        org.Activate();

        org.IsActive.Should().BeTrue();
    }

    // ─── Soft Delete ─────────────────────────────────────────────────────

    [Fact]
    public void SoftDelete_Silinmis_Isaretler()
    {
        var org = Organization.Create(ValidCode, "ABC", "ABC A.Ş.", ValidVkn());

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
        var org = Organization.Create(ValidCode, "ABC", "ABC A.Ş.", ValidVkn());
        var act = () => org.SoftDelete(reason, Now);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SoftDelete_Zaten_Silinmis_Hata_Firlatir()
    {
        var org = Organization.Create(ValidCode, "ABC", "ABC A.Ş.", ValidVkn());
        org.SoftDelete("ilk", Now);

        var act = () => org.SoftDelete("ikinci", Now);
        act.Should().Throw<InvalidOperationException>();
    }

    // ─── Restore ─────────────────────────────────────────────────────────

    [Fact]
    public void Restore_Silinmis_Organizasyonu_Geri_Alir()
    {
        var org = Organization.Create(ValidCode, "ABC", "ABC A.Ş.", ValidVkn());
        org.SoftDelete("yanlışlıkla", Now);

        org.Restore("hata düzeltiliyor", Now.AddMinutes(5));

        org.IsDeleted.Should().BeFalse();
        org.DeletedAt.Should().BeNull();
        org.DeleteReason.Should().BeNull();
    }
}
