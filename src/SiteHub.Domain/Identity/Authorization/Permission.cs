using SiteHub.Domain.Common;

namespace SiteHub.Domain.Identity.Authorization;

public readonly record struct PermissionId(Guid Value) : ITypedId<PermissionId>
{
    public static PermissionId New() => new(Guid.CreateVersion7());
    public static PermissionId FromGuid(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

/// <summary>
/// Permission veritabanı kaydı (ADR-0011 §6.2).
///
/// Kod'daki <c>Permissions</c> sabitlerinin DB karşılığı. Deploy sırasında
/// <c>PermissionSynchronizer</c> çalışır ve kod ile DB'yi senkronize eder:
///
/// - Yeni sabit eklenmişse DB'ye eklenir
/// - Kod'da artık olmayan sabit DB'de varsa DeprecatedAt set edilir (SİLİNMEZ —
///   tarihçeyi bozmaması için, role_permissions tablosunda referanslar var)
///
/// Neden silinmez? role_permissions tablosunda bir permission_id'ye referans
/// olabilir. Silmek FK ihlali olur + tarihçe kaybolur. Deprecate edilince:
/// - UI'da seçim için görünmez
/// - Mevcut role atamaları çalışmaya devam eder (geri dönük uyum)
/// - Yeni seed rollerde kullanılmaz
/// </summary>
public sealed class Permission : Entity<PermissionId>
{
    public string Key { get; private set; } = default!;         // "site.read" — kod'daki sabit değeri
    public string Resource { get; private set; } = default!;    // "Site"
    public string Action { get; private set; } = default!;      // "Read"
    public string Description { get; private set; } = default!; // Türkçe: "Site detayını görüntüleme"
    public DateTimeOffset? DeprecatedAt { get; private set; }

    private Permission() : base() { }

    private Permission(PermissionId id, string key, string resource, string action, string description)
        : base(id)
    {
        Key = key;
        Resource = resource;
        Action = action;
        Description = description;
    }

    public static Permission Create(string key, string resource, string action, string description)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new BusinessRuleViolationException("Permission key zorunlu.");
        if (!key.Contains('.'))
            throw new BusinessRuleViolationException(
                "Permission key '{resource}.{action}' formatında olmalı.");

        return new Permission(PermissionId.New(), key, resource, action, description);
    }

    public void UpdateDescription(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
            throw new BusinessRuleViolationException("Açıklama boş olamaz.");
        Description = description;
    }

    /// <summary>
    /// Permission'ı deprecate eder (kod'dan kaldırıldı). Role'lere atama yapılmaz
    /// ama mevcut role_permissions kayıtları bozulmaz.
    /// </summary>
    public void Deprecate()
    {
        if (DeprecatedAt.HasValue) return;
        DeprecatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Deprecation'ı geri al (kod'a geri eklendiyse).</summary>
    public void Undeprecate() => DeprecatedAt = null;

    public bool IsDeprecated => DeprecatedAt.HasValue;
}
