namespace SiteHub.Domain.Common;

/// <summary>
/// Denetim kaydı + geçici silme destekleyen aggregate'ler için temel sınıf.
///
/// Firm, Site, User, Malik, Sakin, Unit gibi tüm aggregate'ler bu sınıftan türer.
/// Yeni bir aggregate eklerken:
///   public sealed class Site : AuditableAggregateRoot&lt;SiteId&gt;, IAggregateRoot
///   → denetim alanları ve soft-delete davranışları OTOMATİK gelir
///
/// ÖNEMLİ:
/// - Created/Updated alanları SaveChanges sırasında Interceptor tarafından doldurulur
/// - Delete/Restore manuel olarak (iş mantığı kararı sensin)
/// - Domain event'leri Entity'den gelir (Entity&lt;TId&gt; base class'ının)
/// </summary>
public abstract class AuditableAggregateRoot<TId> : Entity<TId>, IAuditable, ISoftDeletable, IAggregateRoot
    where TId : struct
{
    // ─── IAuditable ──────────────────────────────────────────────────────
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? CreatedById { get; private set; }
    public string? CreatedByName { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }
    public Guid? UpdatedById { get; private set; }
    public string? UpdatedByName { get; private set; }

    // ─── ISoftDeletable ──────────────────────────────────────────────────
    public DateTimeOffset? DeletedAt { get; private set; }
    public Guid? DeletedById { get; private set; }
    public string? DeletedByName { get; private set; }
    public string? DeleteReason { get; private set; }

    /// <summary>
    /// Bu entity silinmiş mi? (ISoftDeletable'daki default member'ı explicit
    /// olarak burada yayınlıyoruz ki derive eden sınıflarda doğrudan erişilsin.)
    /// </summary>
    public bool IsDeleted => DeletedAt.HasValue;

    protected AuditableAggregateRoot() : base() { }
    protected AuditableAggregateRoot(TId id) : base(id) { }

    // ─── Silme / geri alma ───────────────────────────────────────────────

    /// <summary>
    /// Entity'yi geçici olarak siler. Veritabanından fiziksel silinmez.
    ///
    /// Sebep ZORUNLU — iş gereği (ADR-0006: neden silindi bilinsin).
    /// Interceptor, delete öncesi tam snapshot'ı audit.entity_changes'e yazar.
    /// </summary>
    public virtual void SoftDelete(string reason, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Silme sebebi boş olamaz.", nameof(reason));
        if (reason.Length > 1000)
            throw new ArgumentException("Silme sebebi 1000 karakteri aşamaz.", nameof(reason));
        if (DeletedAt.HasValue)
            throw new InvalidOperationException("Kayıt zaten silinmiş.");

        DeletedAt = now;
        DeleteReason = reason.Trim();
        // DeletedById ve DeletedByName interceptor tarafından doldurulur
    }

    /// <summary>
    /// Silinmiş entity'yi geri alır. Sebep (neden geri alındığı) zorunlu.
    /// Interceptor bu işlemi de audit.entity_changes'e (operation=Restore) yazar.
    /// </summary>
    public virtual void Restore(string reason, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Geri alma sebebi boş olamaz.", nameof(reason));
        if (!DeletedAt.HasValue)
            throw new InvalidOperationException("Kayıt zaten aktif, geri alınacak durum yok.");

        DeletedAt = null;
        DeletedById = null;
        DeletedByName = null;
        DeleteReason = null;
        UpdatedAt = now;
        // UpdatedBy interceptor tarafından doldurulur
    }

    /// <summary>
    /// Interceptor'ın çağırdığı — aggregate'lerin değer kendisi atamak yerine
    /// buradan setler. Domain dışından (uygulama kodundan) çağrılmamalı.
    /// </summary>
    internal void SetCreatedAudit(DateTimeOffset at, Guid? userId, string? userName)
    {
        CreatedAt = at;
        CreatedById = userId;
        CreatedByName = userName;
    }

    internal void SetUpdatedAudit(DateTimeOffset at, Guid? userId, string? userName)
    {
        UpdatedAt = at;
        UpdatedById = userId;
        UpdatedByName = userName;
    }

    internal void SetDeletedAudit(Guid? userId, string? userName)
    {
        DeletedById = userId;
        DeletedByName = userName;
    }
}
