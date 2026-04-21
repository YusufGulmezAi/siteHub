namespace SiteHub.Domain.Common;

/// <summary>
/// Oluşturulma ve güncellenme denetim bilgisini tutan entity'ler için marker.
///
/// Bu alanları <see cref="AuditableAggregateRoot{TId}"/> otomatik doldurur
/// (SaveChanges sırasında AuditSaveChangesInterceptor tarafından).
///
/// UserName alanı DENORMALIZE — ileride user silinse bile geçmişte kim
/// değiştirdiğini görebilmek için. ID + Ad beraber tutulur.
/// </summary>
public interface IAuditable
{
    DateTimeOffset CreatedAt { get; }
    Guid? CreatedById { get; }
    string? CreatedByName { get; }

    DateTimeOffset? UpdatedAt { get; }
    Guid? UpdatedById { get; }
    string? UpdatedByName { get; }
}
