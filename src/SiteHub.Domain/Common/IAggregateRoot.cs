namespace SiteHub.Domain.Common;

/// <summary>
/// Aggregate Root: DDD'de bir işlem sınırını (transactional consistency boundary)
/// temsil eden ana entity.
///
/// Örnekler:
/// - User aggregate: User (root) + Memberships
/// - Invoice aggregate: Invoice (root) + InvoiceLines
/// - Site aggregate: Site (root) + Units + Blocks
///
/// Kural: Bir aggregate'ın DIŞINDAN yalnızca root entity'ye referans verilebilir.
/// Child entity'lere dışarıdan erişim yok. Değişiklikler root üzerinden yapılır.
///
/// Bu interface bir "marker"dır: davranış eklemez, sadece niyeti belirtir.
/// </summary>
public interface IAggregateRoot
{
}
