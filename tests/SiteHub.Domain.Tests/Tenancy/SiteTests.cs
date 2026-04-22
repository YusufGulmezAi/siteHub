using SiteHub.Domain.Geography;
using SiteHub.Domain.Identity;
using SiteHub.Domain.Tenancy.Organizations;
using SiteHub.Domain.Tenancy.Sites;

namespace SiteHub.Domain.Tests.Tenancy;

public class SiteTests
{
    // ─── Test Sabitleri ──────────────────────────────────────────────────

    private static readonly DateTimeOffset Now = DateTimeOffset.Parse(
        "2026-04-22T10:00:00+03:00",
        System.Globalization.CultureInfo.InvariantCulture);

    private const long ValidCode = 123456;
    private const string ValidAddress = "Bağdat Cad. No:45, Kadıköy";
    private const string ValidIban = "TR330006100519786457841326";

    private static NationalId ValidVkn() => NationalId.CreateVkn("1234567890");
    private static OrganizationId SomeOrgId() => OrganizationId.New();
    private static ProvinceId SomeProvinceId() => ProvinceId.New();
    private static DistrictId SomeDistrictId() => DistrictId.New();

    // ─── Create ──────────────────────────────────────────────────────────

    [Fact]
    public void Create_MinimalGecerliGirdilerle_Olusturulur()
    {
        var orgId = SomeOrgId();
        var provinceId = SomeProvinceId();

        var site = Site.Create(
            code: ValidCode,
            organizationId: orgId,
            name: "Yıldız Sitesi",
            provinceId: provinceId,
            address: ValidAddress);

        site.Code.Should().Be(ValidCode);
        site.OrganizationId.Should().Be(orgId);
        site.Name.Should().Be("Yıldız Sitesi");
        site.ProvinceId.Should().Be(provinceId);
        site.Address.Should().Be(ValidAddress);
        site.CommercialTitle.Should().BeNull();
        site.DistrictId.Should().BeNull();
        site.Iban.Should().BeNull();
        site.TaxId.Should().BeNull();
        site.IsActive.Should().BeTrue();
        site.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public void Create_TumOpsiyonelAlanlarlaOlusturulur()
    {
        var districtId = SomeDistrictId();
        var site = Site.Create(
            code: ValidCode,
            organizationId: SomeOrgId(),
            name: "Yıldız Sitesi",
            provinceId: SomeProvinceId(),
            address: ValidAddress,
            commercialTitle: "Yıldız Yönetim Koop.",
            districtId: districtId,
            iban: ValidIban,
            taxId: ValidVkn());

        site.CommercialTitle.Should().Be("Yıldız Yönetim Koop.");
        site.DistrictId.Should().Be(districtId);
        site.Iban.Should().Be(ValidIban);
        site.TaxId.Should().NotBeNull();
        site.TaxId!.Value.Should().Be("1234567890");
    }

    [Fact]
    public void Create_IbanBosluklarla_NormalizeEdilir()
    {
        var site = Site.Create(
            code: ValidCode,
            organizationId: SomeOrgId(),
            name: "X",
            provinceId: SomeProvinceId(),
            address: ValidAddress,
            iban: "TR33 0006 1005 1978 6457 8413 26");

        site.Iban.Should().Be(ValidIban); // Boşluksuz
    }

    [Fact]
    public void Create_IbanKucukHarflerle_NormalizeEdilir()
    {
        var site = Site.Create(
            code: ValidCode,
            organizationId: SomeOrgId(),
            name: "X",
            provinceId: SomeProvinceId(),
            address: ValidAddress,
            iban: "tr330006100519786457841326");

        site.Iban.Should().StartWith("TR");
    }

    [Theory]
    [InlineData(100_000)]    // aralığın altı
    [InlineData(1_000_000)]  // aralığın üstü
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_GecersizCode_AtarArgumentOutOfRange(long badCode)
    {
        var act = () => Site.Create(
            code: badCode,
            organizationId: SomeOrgId(),
            name: "X",
            provinceId: SomeProvinceId(),
            address: ValidAddress);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Create_BosOrganizationId_Atar()
    {
        var act = () => Site.Create(
            code: ValidCode,
            organizationId: default,
            name: "X",
            provinceId: SomeProvinceId(),
            address: ValidAddress);

        act.Should().Throw<ArgumentException>().WithMessage("*Organization*");
    }

    [Fact]
    public void Create_BosProvinceId_Atar()
    {
        var act = () => Site.Create(
            code: ValidCode,
            organizationId: SomeOrgId(),
            name: "X",
            provinceId: default,
            address: ValidAddress);

        act.Should().Throw<ArgumentException>().WithMessage("*ProvinceId*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_BosName_KabulEtmez(string badName)
    {
        var act = () => Site.Create(
            code: ValidCode,
            organizationId: SomeOrgId(),
            name: badName,
            provinceId: SomeProvinceId(),
            address: ValidAddress);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_CokUzunName_KabulEtmez()
    {
        var longName = new string('A', 201);
        var act = () => Site.Create(
            code: ValidCode,
            organizationId: SomeOrgId(),
            name: longName,
            provinceId: SomeProvinceId(),
            address: ValidAddress);

        act.Should().Throw<ArgumentException>().WithMessage("*200*");
    }

    [Fact]
    public void Create_CokUzunCommercialTitle_KabulEtmez()
    {
        var longTitle = new string('A', 501);
        var act = () => Site.Create(
            code: ValidCode,
            organizationId: SomeOrgId(),
            name: "X",
            provinceId: SomeProvinceId(),
            address: ValidAddress,
            commercialTitle: longTitle);

        act.Should().Throw<ArgumentException>().WithMessage("*500*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_BosAddress_KabulEtmez(string badAddress)
    {
        var act = () => Site.Create(
            code: ValidCode,
            organizationId: SomeOrgId(),
            name: "X",
            provinceId: SomeProvinceId(),
            address: badAddress);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_CokUzunAddress_KabulEtmez()
    {
        var longAddress = new string('A', 1001);
        var act = () => Site.Create(
            code: ValidCode,
            organizationId: SomeOrgId(),
            name: "X",
            provinceId: SomeProvinceId(),
            address: longAddress);

        act.Should().Throw<ArgumentException>().WithMessage("*1000*");
    }

    [Theory]
    [InlineData("DE89370400440532013000")]     // Almanya IBAN
    [InlineData("TR3300061005197864578413")]   // Kısa (22 char değil 26)
    [InlineData("TR33000610051978645784132X")] // Harf var (TR dışında)
    [InlineData("GB82WEST12345698765432")]     // UK IBAN
    public void Create_GecersizIban_KabulEtmez(string badIban)
    {
        var act = () => Site.Create(
            code: ValidCode,
            organizationId: SomeOrgId(),
            name: "X",
            provinceId: SomeProvinceId(),
            address: ValidAddress,
            iban: badIban);

        act.Should().Throw<ArgumentException>().WithMessage("*IBAN*");
    }

    [Fact]
    public void Create_TcknKimlikKabulEtmez()
    {
        var tckn = NationalId.CreateTckn("10000000146");
        var act = () => Site.Create(
            code: ValidCode,
            organizationId: SomeOrgId(),
            name: "X",
            provinceId: SomeProvinceId(),
            address: ValidAddress,
            taxId: tckn);

        act.Should().Throw<ArgumentException>().WithMessage("*VKN*");
    }

    [Fact]
    public void Create_NameVeAddressTrimlenir()
    {
        var site = Site.Create(
            code: ValidCode,
            organizationId: SomeOrgId(),
            name: "  Yıldız Sitesi  ",
            provinceId: SomeProvinceId(),
            address: "  Bağdat Cad. No:45  ");

        site.Name.Should().Be("Yıldız Sitesi");
        site.Address.Should().Be("Bağdat Cad. No:45");
    }

    [Fact]
    public void Create_SearchTextNormalizeEdilmisOlusturur()
    {
        var site = Site.Create(
            code: ValidCode,
            organizationId: SomeOrgId(),
            name: "Şişli SİTESİ",
            provinceId: SomeProvinceId(),
            address: "Büyükdere Cad.");

        site.SearchText.Should().Contain("şişli sitesi");
        site.SearchText.Should().Contain("büyükdere");
        site.SearchText.Should().NotContain("ŞİŞLİ");
        site.SearchText.Should().NotContain("SİTESİ");
    }

    [Fact]
    public void Create_SearchTextCodeVeIbanIcerir()
    {
        var site = Site.Create(
            code: ValidCode,
            organizationId: SomeOrgId(),
            name: "X",
            provinceId: SomeProvinceId(),
            address: ValidAddress,
            iban: ValidIban,
            taxId: ValidVkn());

        site.SearchText.Should().Contain(ValidCode.ToString());
        site.SearchText.Should().Contain("1234567890"); // VKN
        site.SearchText.Should().Contain(ValidIban.ToLowerInvariant());
    }

    // ─── Rename ──────────────────────────────────────────────────────────

    [Fact]
    public void Rename_NameVeCommercialTitleGuncelleme()
    {
        var site = Site.Create(ValidCode, SomeOrgId(), "Eski Ad", SomeProvinceId(), ValidAddress);

        site.Rename("Yeni Ad", "Yeni Ticari Unvan");

        site.Name.Should().Be("Yeni Ad");
        site.CommercialTitle.Should().Be("Yeni Ticari Unvan");
    }

    [Fact]
    public void Rename_CommercialTitleNullYapabilir()
    {
        var site = Site.Create(ValidCode, SomeOrgId(), "X", SomeProvinceId(), ValidAddress,
            commercialTitle: "Başlangıç");

        site.Rename("Yeni Ad", null);

        site.CommercialTitle.Should().BeNull();
    }

    [Fact]
    public void Rename_SearchTextYenidenHesaplanir()
    {
        var site = Site.Create(ValidCode, SomeOrgId(), "Eski", SomeProvinceId(), ValidAddress);
        var oldSearchText = site.SearchText;

        site.Rename("Tamamen Yeni Ad", null);

        site.SearchText.Should().NotBe(oldSearchText);
        site.SearchText.Should().Contain("tamamen yeni ad");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rename_BosName_Atar(string badName)
    {
        var site = Site.Create(ValidCode, SomeOrgId(), "X", SomeProvinceId(), ValidAddress);
        var act = () => site.Rename(badName, null);
        act.Should().Throw<ArgumentException>();
    }

    // ─── ChangeAddress ───────────────────────────────────────────────────

    [Fact]
    public void ChangeAddress_TumAlanlariGuncellemeli()
    {
        var site = Site.Create(ValidCode, SomeOrgId(), "X", SomeProvinceId(), ValidAddress);
        var newProvince = SomeProvinceId();
        var newDistrict = SomeDistrictId();

        site.ChangeAddress("Yeni adres", newProvince, newDistrict);

        site.Address.Should().Be("Yeni adres");
        site.ProvinceId.Should().Be(newProvince);
        site.DistrictId.Should().Be(newDistrict);
    }

    [Fact]
    public void ChangeAddress_SearchTextYenidenHesaplanir()
    {
        var site = Site.Create(ValidCode, SomeOrgId(), "X", SomeProvinceId(), "Eski adres");
        var oldSearch = site.SearchText;

        site.ChangeAddress("Tamamen farklı adres", SomeProvinceId(), null);

        site.SearchText.Should().NotBe(oldSearch);
        site.SearchText.Should().Contain("tamamen farklı adres");
    }

    [Fact]
    public void ChangeAddress_BosProvinceId_Atar()
    {
        var site = Site.Create(ValidCode, SomeOrgId(), "X", SomeProvinceId(), ValidAddress);
        var act = () => site.ChangeAddress("Yeni adres", default, null);
        act.Should().Throw<ArgumentException>();
    }

    // ─── IBAN ────────────────────────────────────────────────────────────

    [Fact]
    public void SetIban_GecerliIbanGuncellemeli()
    {
        var site = Site.Create(ValidCode, SomeOrgId(), "X", SomeProvinceId(), ValidAddress);

        site.SetIban(ValidIban);

        site.Iban.Should().Be(ValidIban);
    }

    [Fact]
    public void SetIban_BoslukluIbanNormalizeEdilir()
    {
        var site = Site.Create(ValidCode, SomeOrgId(), "X", SomeProvinceId(), ValidAddress);

        site.SetIban("TR33 0006 1005 1978 6457 8413 26");

        site.Iban.Should().Be(ValidIban);
    }

    [Fact]
    public void SetIban_GecersizIban_Atar()
    {
        var site = Site.Create(ValidCode, SomeOrgId(), "X", SomeProvinceId(), ValidAddress);
        var act = () => site.SetIban("ABC123");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ClearIban_NedenZorunlu()
    {
        var site = Site.Create(ValidCode, SomeOrgId(), "X", SomeProvinceId(), ValidAddress,
            iban: ValidIban);

        var act = () => site.ClearIban("");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ClearIban_GecerliNedenleTemizler()
    {
        var site = Site.Create(ValidCode, SomeOrgId(), "X", SomeProvinceId(), ValidAddress,
            iban: ValidIban);

        site.ClearIban("Banka değişikliği");

        site.Iban.Should().BeNull();
    }

    // ─── Durum ───────────────────────────────────────────────────────────

    [Fact]
    public void Deactivate_IsActiveFalseYapar()
    {
        var site = Site.Create(ValidCode, SomeOrgId(), "X", SomeProvinceId(), ValidAddress);
        site.IsActive.Should().BeTrue();

        site.Deactivate();

        site.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Activate_IsActiveTrueYapar()
    {
        var site = Site.Create(ValidCode, SomeOrgId(), "X", SomeProvinceId(), ValidAddress);
        site.Deactivate();

        site.Activate();

        site.IsActive.Should().BeTrue();
    }

    [Fact]
    public void SoftDelete_DeletedAtSetEder()
    {
        var site = Site.Create(ValidCode, SomeOrgId(), "X", SomeProvinceId(), ValidAddress);

        site.SoftDelete("Site kapatıldı", Now);

        site.IsDeleted.Should().BeTrue();
        site.DeletedAt.Should().Be(Now);
        site.DeleteReason.Should().Be("Site kapatıldı");
    }

    [Fact]
    public void SoftDelete_NedenZorunlu()
    {
        var site = Site.Create(ValidCode, SomeOrgId(), "X", SomeProvinceId(), ValidAddress);

        var act = () => site.SoftDelete("", Now);

        act.Should().Throw<ArgumentException>();
    }

    // ─── TaxId ───────────────────────────────────────────────────────────

    [Fact]
    public void SetTaxId_VknGuncellemeli()
    {
        var site = Site.Create(ValidCode, SomeOrgId(), "X", SomeProvinceId(), ValidAddress);

        site.SetTaxId(ValidVkn());

        site.TaxId.Should().NotBeNull();
        site.TaxId!.Type.Should().Be(NationalIdType.VKN);
    }

    [Fact]
    public void SetTaxId_TcknAtar()
    {
        var site = Site.Create(ValidCode, SomeOrgId(), "X", SomeProvinceId(), ValidAddress);
        var tckn = NationalId.CreateTckn("10000000146");

        var act = () => site.SetTaxId(tckn);

        act.Should().Throw<ArgumentException>().WithMessage("*VKN*");
    }

    [Fact]
    public void ClearTaxId_NullYapar()
    {
        var site = Site.Create(ValidCode, SomeOrgId(), "X", SomeProvinceId(), ValidAddress,
            taxId: ValidVkn());

        site.ClearTaxId();

        site.TaxId.Should().BeNull();
    }
}
