namespace SiteHub.Infrastructure.Persistence.Seed.Geography;

/// <summary>
/// turkey-addresses.csv dosyasındaki bir satır.
/// CSV kolonları: IL_ID,İL ADI,ILCE_ID,İLÇE ADI,SEMT_ID,SEMT_ADI_BUYUK,POSTA_KODU
/// </summary>
internal sealed record TurkeyAddressRow(
    int IlId,
    string IlAdi,
    int IlceId,
    string IlceAdi,
    int SemtId,
    string SemtAdi,
    string? PostaKodu);
