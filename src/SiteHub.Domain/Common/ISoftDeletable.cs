namespace SiteHub.Domain.Common;

/// <summary>
/// Geçici silme (soft-delete) destekleyen entity'ler için marker.
///
/// "Sil" denildiğinde kayıt fiziksel kaybolmaz; DeletedAt/DeletedBy doldurulur.
/// EF Core global query filter sayesinde aktif sorgularda görünmez.
///
/// Silinenleri görmek için: .IgnoreQueryFilters() çağrısı.
/// Geri almak için: AuditableAggregateRoot.Restore(reason).
/// </summary>
public interface ISoftDeletable
{
    DateTimeOffset? DeletedAt { get; }
    Guid? DeletedById { get; }
    string? DeletedByName { get; }

    /// <summary>Silme sebebi — iş gereği ZORUNLU (null = silinmemiş).</summary>
    string? DeleteReason { get; }

    bool IsDeleted => DeletedAt.HasValue;
}
