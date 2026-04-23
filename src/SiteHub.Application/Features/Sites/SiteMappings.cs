using SiteHub.Contracts.Sites;
using SiteHub.Domain.Tenancy.Sites;

namespace SiteHub.Application.Features.Sites;

/// <summary>
/// Site entity → Contracts DTO manuel mapping extension'ları.
///
/// <para><b>Neden bu dosya var?</b></para>
///
/// F.6 Cleanup'ta alınan karar (PROJECT_STATE §5.5 madde 10): DTO mapping için
/// AutoMapper / Mapster gibi reflection-tabanlı araçlar kullanılmaz. Manuel
/// extension method pattern'i tercih edilir — derleme-zamanı güvenliği,
/// type-safe refactor, AOT-uyumlu, sıfır NuGet bağımlılığı.
///
/// <para><b>Şu an neden boş?</b></para>
///
/// Query handler'lar EF Core LINQ projection kullanır (<c>.Select(s => new Dto(...))</c>).
/// Expression tree içinde extension method çalıştırılamaz (reflection-free runtime) —
/// projection'da inline <c>new Dto(...)</c> yazmak zorundayız. Bu yüzden şu anda
/// hiç mapping method'a ihtiyaç yok.
///
/// <para><b>Ne zaman doldurulur?</b></para>
///
/// In-memory entity → DTO dönüşümü gerektiğinde:
/// <list type="bullet">
///   <item>Bir Command handler Create sonrası tam DTO döndürmek istediğinde</item>
///   <item>Domain event'lerde DTO'ya çevirme</item>
///   <item>Background job'larda entity → response DTO</item>
/// </list>
///
/// <para><b>Pattern örneği (gelecek):</b></para>
///
/// <code>
/// internal static SiteListItemDto ToListItemDto(this Site site, string organizationName)
///     =&gt; new(
///         site.Id.Value,
///         site.OrganizationId.Value,
///         organizationName,
///         site.Code,
///         site.Name,
///         ...);
/// </code>
///
/// <para><b>ADR-0017:</b> F.6 sonunda "Manuel DTO Mapping Pattern" ADR'si bu kararı
/// geriye dönük belgeleyecek.</para>
/// </summary>
internal static class SiteMappings
{
    // Şimdilik boş — projection'lar yeterli.
    // In-memory mapping ihtiyacı doğduğunda buraya eklenir.
}
