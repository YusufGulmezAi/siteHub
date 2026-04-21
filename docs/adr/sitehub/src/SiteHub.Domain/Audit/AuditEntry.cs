namespace SiteHub.Domain.Audit;

/// <summary>
/// Bir entity üzerindeki tek bir değişiklik kaydı.
///
/// audit.entity_changes tablosuna yazılır. APPEND-ONLY — hiç update/delete
/// yapılmaz. 10 yıl saklanır (ADR-0006).
///
/// Tipik satır örnekleri:
/// - Bir Firm güncellendi → Update, changes JSON'da alan alan eski/yeni değerler
/// - Bir Site silindi → Delete, changes JSON'da __snapshot_before (tam kayıt) + reason
/// - Bir Unit oluşturuldu → Insert, changes JSON'da __snapshot_after (tüm alanlar)
///
/// Bu entity domain katmanında durur ama "aggregate root" değil — her sistem olayı
/// için audit kaydı oluşturan altyapı (AuditSaveChangesInterceptor) tarafından üretilir.
/// İş mantığı bu kayıtları DOĞRUDAN manipüle etmez; sadece sorgular.
/// </summary>
public sealed class AuditEntry
{
    public Guid Id { get; private set; }
    public DateTimeOffset Timestamp { get; private set; }

    /// <summary>"Firm", "Site", "Unit", "User", vb.</summary>
    public string EntityType { get; private set; } = default!;

    /// <summary>Entity'nin ID'si (Guid olarak serialize edilir — tüm TypedId'ler Guid tabanlı).</summary>
    public Guid EntityId { get; private set; }

    public AuditOperation Operation { get; private set; }

    // ─── Kim ─────────────────────────────────────────────────────────────
    public Guid? UserId { get; private set; }
    public string? UserName { get; private set; }        // denormalize: o anki ad

    // ─── Nereden ────────────────────────────────────────────────────────
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }
    public string? CorrelationId { get; private set; }

    // ─── Hangi bağlamda (System/Firm/Site context) ──────────────────────
    public string? ContextType { get; private set; }      // "System", "Firm", "Site"
    public Guid? ContextId { get; private set; }

    /// <summary>
    /// Değişen alanlar JSON formatında.
    ///
    /// Insert: { "__snapshot_after": { name: "...", ... } }
    /// Update: { "fieldName": { "old": "...", "new": "..." }, ... }
    /// Delete: { "__snapshot_before": {...}, "reason": "..." }
    /// Restore: { "reason": "...", "deletedAt_was": "..." }
    ///
    /// Hassas alanlar (password, token, TCKN'nin büyük kısmı) MASKELENİR.
    /// </summary>
    public string ChangesJson { get; private set; } = "{}";

    // EF Core için
    private AuditEntry() { }

    public AuditEntry(
        DateTimeOffset timestamp,
        string entityType,
        Guid entityId,
        AuditOperation operation,
        Guid? userId,
        string? userName,
        string? ipAddress,
        string? userAgent,
        string? correlationId,
        string? contextType,
        Guid? contextId,
        string changesJson)
    {
        Id = Guid.CreateVersion7();
        Timestamp = timestamp;
        EntityType = entityType;
        EntityId = entityId;
        Operation = operation;
        UserId = userId;
        UserName = userName;
        IpAddress = ipAddress;
        UserAgent = userAgent;
        CorrelationId = correlationId;
        ContextType = contextType;
        ContextId = contextId;
        ChangesJson = changesJson;
    }
}

public enum AuditOperation
{
    Insert = 1,
    Update = 2,
    Delete = 3,        // geçici silme (soft-delete)
    Restore = 4,       // silinmiş kaydı geri alma
    HardDelete = 5     // fiziksel silme (sistem operasyonu, çok nadir)
}
