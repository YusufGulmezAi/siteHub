namespace SiteHub.Contracts.Geography;

/// <summary>
/// İl bilgisi. UI dropdown'u veya adres seçimi için minimum alanlar.
///
/// <para>Not: Bu DTO, <c>Application.Features.Geography.ProvinceDto</c>'nun birebir kopyasıdır
/// (Faz F.6 A.2 "Seçenek B" — duplicate kabul, cleanup sonra). Wire format aynıdır,
/// JSON serialize/deserialize her iki taraf için sorunsuz çalışır.</para>
/// </summary>
public sealed record ProvinceDto(
    Guid Id,
    string Name,
    string PlateCode,
    int ExternalId);
