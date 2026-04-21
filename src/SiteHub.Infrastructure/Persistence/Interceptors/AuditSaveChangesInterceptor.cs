using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SiteHub.Application.Abstractions.Audit;
using SiteHub.Domain.Audit;
using SiteHub.Domain.Common;
using SiteHub.Shared.Logging;

namespace SiteHub.Infrastructure.Persistence.Interceptors;

/// <summary>
/// EF Core interceptor — SaveChanges sırasında iki iş yapar:
///
/// 1) Audit alanlarını OTOMATİK doldurur:
///    - Added entity → CreatedAt, CreatedById, CreatedByName
///    - Modified entity → UpdatedAt, UpdatedById, UpdatedByName
///    - Soft-deleted → DeletedById, DeletedByName (DeletedAt entity'de set edildi)
///
/// 2) audit.entity_changes tablosuna her değişiklik için kayıt ekler:
///    - Operation (Insert/Update/Delete/Restore)
///    - Eski/yeni değerler (hassas alanlar maskelenmiş)
///    - Kim, nereden, ne zaman, hangi bağlamda
///
/// DI lifetime: Scoped — her SaveChanges için current user/connection info okur.
///
/// GERÇEK "DELETE" İŞLEMİ:
/// - EF Core'da DbContext.Remove(entity) çağrıldığında EntityState.Deleted olur
/// - Interceptor bunu yakalar:
///   - Entity ISoftDeletable mi? → state'i Modified'a çevir + DeletedAt/By set et
///   - Değil mi? → olduğu gibi fiziksel sil (HardDelete audit kaydı yaz)
/// </summary>
public sealed class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUserService _currentUser;
    private readonly ICurrentConnectionInfo _connection;
    private readonly TimeProvider _time;

    // JSON serializer — kolon adları snake_case, tarih ISO 8601
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public AuditSaveChangesInterceptor(
        ICurrentUserService currentUser,
        ICurrentConnectionInfo connection,
        TimeProvider time)
    {
        _currentUser = currentUser;
        _connection = connection;
        _time = time;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            ProcessChanges(eventData.Context);
        }
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        if (eventData.Context is not null)
        {
            ProcessChanges(eventData.Context);
        }
        return base.SavingChanges(eventData, result);
    }

    private void ProcessChanges(DbContext context)
    {
        var now = _time.GetUtcNow();
        var userId = _currentUser.UserId;
        var userName = _currentUser.UserName;
        var auditEntriesToAdd = new List<AuditEntry>();

        // Audit kayıtları için entity değişikliklerini YAKALA
        // (SaveChanges'ten sonra tracker boşalır, şimdi topla)
        var entries = context.ChangeTracker.Entries()
            .Where(e => e.Entity is not AuditEntry) // audit'in kendisini audit'leme (sonsuz döngü)
            .ToList();

        foreach (var entry in entries)
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    HandleInsert(entry, now, userId, userName, auditEntriesToAdd);
                    break;

                case EntityState.Modified:
                    HandleUpdate(entry, now, userId, userName, auditEntriesToAdd);
                    break;

                case EntityState.Deleted:
                    HandleDelete(entry, now, userId, userName, auditEntriesToAdd);
                    break;
            }
        }

        // Audit entry'leri DbContext'e ekle — SaveChanges onları da persist eder
        if (auditEntriesToAdd.Count > 0)
        {
            context.Set<AuditEntry>().AddRange(auditEntriesToAdd);
        }
    }

    // ─── Insert ──────────────────────────────────────────────────────────

    private void HandleInsert(
        EntityEntry entry, DateTimeOffset now, Guid? userId, string? userName,
        List<AuditEntry> audits)
    {
        // Audit alanlarını doldur
        if (entry.Entity is IAuditable auditable)
        {
            SetCreatedAuditReflected(entry, now, userId, userName);
        }

        // Audit entry'si oluştur — tüm alanları "__snapshot_after" olarak yaz
        var snapshot = BuildSnapshot(entry);
        var changes = new JsonObject { ["__snapshot_after"] = snapshot };

        audits.Add(CreateAuditEntry(entry, AuditOperation.Insert, now, userId, userName, changes));
    }

    // ─── Update ──────────────────────────────────────────────────────────

    private void HandleUpdate(
        EntityEntry entry, DateTimeOffset now, Guid? userId, string? userName,
        List<AuditEntry> audits)
    {
        // Audit alanlarını doldur
        if (entry.Entity is IAuditable)
        {
            SetUpdatedAuditReflected(entry, now, userId, userName);
        }

        // Soft-delete durumu mu? Entity kodu DeletedAt/DeleteReason set etti,
        // biz DeletedById/Name dolduralım
        var isSoftDeleting = entry.Entity is ISoftDeletable sd && sd.DeletedAt.HasValue
            && entry.Property(nameof(ISoftDeletable.DeletedAt)).IsModified
            && entry.OriginalValues[nameof(ISoftDeletable.DeletedAt)] == null;

        var isRestoring = entry.Entity is ISoftDeletable
            && entry.Property(nameof(ISoftDeletable.DeletedAt)).IsModified
            && entry.OriginalValues[nameof(ISoftDeletable.DeletedAt)] != null
            && entry.CurrentValues[nameof(ISoftDeletable.DeletedAt)] == null;

        if (isSoftDeleting && entry.Entity is ISoftDeletable)
        {
            SetDeletedAuditReflected(entry, userId, userName);
        }

        // Değişiklikleri topla
        var changes = new JsonObject();

        if (isSoftDeleting)
        {
            // Delete operasyonu — tam önceki hali + sebep
            var snapshotBefore = BuildOriginalSnapshot(entry);
            changes["__snapshot_before"] = snapshotBefore;
            changes["reason"] = entry.Property(nameof(ISoftDeletable.DeleteReason)).CurrentValue?.ToString();

            audits.Add(CreateAuditEntry(entry, AuditOperation.Delete, now, userId, userName, changes));
            return;
        }

        if (isRestoring)
        {
            changes["deletedAt_was"] = entry.OriginalValues[nameof(ISoftDeletable.DeletedAt)]?.ToString();
            audits.Add(CreateAuditEntry(entry, AuditOperation.Restore, now, userId, userName, changes));
            return;
        }

        // Normal update — değişen alanlar
        foreach (var prop in entry.Properties.Where(p => p.IsModified))
        {
            var propName = prop.Metadata.Name;

            // Otomatik audit alanlarını changes'e YAZMA (meta-noise)
            if (IsAuditMetaField(propName)) continue;

            var oldValue = prop.OriginalValue;
            var newValue = prop.CurrentValue;

            changes[ToCamelCase(propName)] = new JsonObject
            {
                ["old"] = MaskIfSensitive(propName, oldValue),
                ["new"] = MaskIfSensitive(propName, newValue)
            };
        }

        // Değişen hiçbir alan yoksa (sadece audit alanları değişti) kayıt oluşturma
        if (changes.Count == 0) return;

        audits.Add(CreateAuditEntry(entry, AuditOperation.Update, now, userId, userName, changes));
    }

    // ─── Delete ──────────────────────────────────────────────────────────

    private void HandleDelete(
        EntityEntry entry, DateTimeOffset now, Guid? userId, string? userName,
        List<AuditEntry> audits)
    {
        // Soft-deletable mı? → delete'i modified'a çevir + audit alanlarını set et
        if (entry.Entity is ISoftDeletable softDeletable)
        {
            throw new InvalidOperationException(
                $"{entry.Entity.GetType().Name} ISoftDeletable'dır — DbContext.Remove() kullanma, " +
                $"aggregate.SoftDelete(reason) çağır. Bu koruma iş kuralını zorunlu kılar.");
        }

        // Soft-deletable değil — fiziksel silme (HardDelete)
        var snapshotBefore = BuildOriginalSnapshot(entry);
        var changes = new JsonObject { ["__snapshot_before"] = snapshotBefore };

        audits.Add(CreateAuditEntry(entry, AuditOperation.HardDelete, now, userId, userName, changes));
    }

    // ─── Helper'lar ──────────────────────────────────────────────────────

    private static void SetCreatedAuditReflected(
        EntityEntry entry, DateTimeOffset now, Guid? userId, string? userName)
    {
        entry.Property(nameof(IAuditable.CreatedAt)).CurrentValue = now;
        entry.Property(nameof(IAuditable.CreatedById)).CurrentValue = userId;
        entry.Property(nameof(IAuditable.CreatedByName)).CurrentValue = userName;
    }

    private static void SetUpdatedAuditReflected(
        EntityEntry entry, DateTimeOffset now, Guid? userId, string? userName)
    {
        entry.Property(nameof(IAuditable.UpdatedAt)).CurrentValue = now;
        entry.Property(nameof(IAuditable.UpdatedById)).CurrentValue = userId;
        entry.Property(nameof(IAuditable.UpdatedByName)).CurrentValue = userName;
    }

    private static void SetDeletedAuditReflected(EntityEntry entry, Guid? userId, string? userName)
    {
        entry.Property(nameof(ISoftDeletable.DeletedById)).CurrentValue = userId;
        entry.Property(nameof(ISoftDeletable.DeletedByName)).CurrentValue = userName;
    }

    private AuditEntry CreateAuditEntry(
        EntityEntry entry, AuditOperation operation, DateTimeOffset now,
        Guid? userId, string? userName, JsonObject changes)
    {
        var entityType = entry.Entity.GetType().Name;
        var entityId = ExtractEntityId(entry);

        return new AuditEntry(
            timestamp: now,
            entityType: entityType,
            entityId: entityId,
            operation: operation,
            userId: userId,
            userName: userName,
            ipAddress: _connection.IpAddress,
            userAgent: _connection.UserAgent,
            correlationId: _connection.CorrelationId,
            contextType: null,   // TODO: IActiveContextAccessor hazır olunca doldur
            contextId: null,
            changesJson: changes.ToJsonString(JsonOpts));
    }

    /// <summary>Entity'nin primary key Id'sini Guid olarak çıkar.</summary>
    private static Guid ExtractEntityId(EntityEntry entry)
    {
        var keyProp = entry.Metadata.FindPrimaryKey()?.Properties.FirstOrDefault();
        if (keyProp is null) return Guid.Empty;

        var keyValue = entry.Property(keyProp.Name).CurrentValue;
        return keyValue switch
        {
            Guid g => g,
            _ => keyValue is not null
                ? Guid.TryParse(keyValue.ToString(), out var parsed) ? parsed : Guid.Empty
                : Guid.Empty
        };
    }

    /// <summary>Tüm kolonların mevcut değerlerini JSON olarak dön.</summary>
    private static JsonObject BuildSnapshot(EntityEntry entry)
    {
        var snapshot = new JsonObject();
        foreach (var prop in entry.Properties.Where(p => !IsAuditMetaField(p.Metadata.Name)))
        {
            var propName = ToCamelCase(prop.Metadata.Name);
            snapshot[propName] = MaskIfSensitive(prop.Metadata.Name, prop.CurrentValue);
        }
        return snapshot;
    }

    /// <summary>Silinme öncesi orijinal değerler.</summary>
    private static JsonObject BuildOriginalSnapshot(EntityEntry entry)
    {
        var snapshot = new JsonObject();
        foreach (var prop in entry.Properties.Where(p => !IsAuditMetaField(p.Metadata.Name)))
        {
            var propName = ToCamelCase(prop.Metadata.Name);
            snapshot[propName] = MaskIfSensitive(prop.Metadata.Name, prop.OriginalValue);
        }
        return snapshot;
    }

    private static JsonNode? MaskIfSensitive(string propName, object? value)
    {
        if (value is null) return null;

        if (SensitiveFields.ShouldFullyMask(propName))
            return SensitiveFields.FullMask;

        var strValue = value.ToString();
        if (strValue is null) return null;

        if (SensitiveFields.ShouldPartiallyMask(propName, out var keepStart, out var keepEnd))
            return SensitiveFields.MaskString(strValue, keepStart, keepEnd);

        // Primitive ise tipine göre koru (int, bool, DateTimeOffset)
        return value switch
        {
            string s => s,
            bool b => b,
            int i => i,
            long l => l,
            decimal d => d,
            double dbl => dbl,
            Guid g => g.ToString(),
            DateTime dt => dt.ToString("O"),
            DateTimeOffset dto => dto.ToString("O"),
            _ => strValue
        };
    }

    private static bool IsAuditMetaField(string name) => name is
        nameof(IAuditable.CreatedAt) or nameof(IAuditable.CreatedById) or nameof(IAuditable.CreatedByName) or
        nameof(IAuditable.UpdatedAt) or nameof(IAuditable.UpdatedById) or nameof(IAuditable.UpdatedByName);

    private static string ToCamelCase(string name)
        => string.IsNullOrEmpty(name) ? name : char.ToLowerInvariant(name[0]) + name[1..];
}
