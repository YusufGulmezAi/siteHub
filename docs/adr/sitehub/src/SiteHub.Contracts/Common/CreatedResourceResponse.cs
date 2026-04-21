namespace SiteHub.Contracts.Common;

/// <summary>
/// Yeni kaynak oluşturma endpoint'lerinde standart response.
/// Örn: POST /api/sites → oluşturulan site'ın ID + Code'u döner.
/// Tam detayı GET /api/sites/{code} ile alınır.
/// </summary>
public sealed class CreatedResourceResponse
{
    /// <summary>Teknik ID (GUID).</summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Kullanıcı-okuyabilir kod (varsa). Örn: Site Code = 234567.
    /// URL'de kullanılır: /c/site/234567
    /// </summary>
    public long? Code { get; init; }

    /// <summary>
    /// Oluşturulan kaynağın slug'ı (varsa). Örn: "abc-yonetim-as"
    /// </summary>
    public string? Slug { get; init; }
}
