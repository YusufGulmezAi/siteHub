namespace SiteHub.Domain.Common;

/// <summary>
/// Tüm entity'ler için temel sınıf.
///
/// Entity: benzersiz bir kimliği (Id) olan nesne.
/// Aynı değerlere sahip olsalar bile iki entity farklı Id'lere sahipse
/// farklı nesnelerdir (ör: iki kullanıcının adı aynı olabilir ama farklı Id'leri vardır).
///
/// Domain event desteği: Entity'ler iş süreçleri sırasında olaylar üretebilir
/// (ör: "OdemeAlindi", "AidatTahakkukEdildi"). Bu olaylar Infrastructure katmanında
/// MediatR üzerinden publish edilir.
/// </summary>
public abstract class Entity<TId> : IEquatable<Entity<TId>>
    where TId : notnull
{
    private readonly List<IDomainEvent> _domainEvents = [];

    protected Entity(TId id)
    {
        Id = id;
    }

    // EF Core için parametresiz protected constructor
    protected Entity() { Id = default!; }

    public TId Id { get; protected set; }

    /// <summary>
    /// Yayınlanmayı bekleyen domain event'leri.
    /// DbContext.SaveChanges sırasında publish edilip temizlenir.
    /// </summary>
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void RaiseDomainEvent(IDomainEvent domainEvent)
        => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();

    // ─── Eşitlik: Entity'ler Id'ye göre karşılaştırılır ───

    public bool Equals(Entity<TId>? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (GetType() != other.GetType()) return false;
        return EqualityComparer<TId>.Default.Equals(Id, other.Id);
    }

    public override bool Equals(object? obj) => Equals(obj as Entity<TId>);

    public override int GetHashCode() => EqualityComparer<TId>.Default.GetHashCode(Id);

    public static bool operator ==(Entity<TId>? left, Entity<TId>? right)
        => Equals(left, right);

    public static bool operator !=(Entity<TId>? left, Entity<TId>? right)
        => !Equals(left, right);
}
