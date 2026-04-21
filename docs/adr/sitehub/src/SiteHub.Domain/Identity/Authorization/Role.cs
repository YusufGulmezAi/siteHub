using SiteHub.Domain.Common;
using SiteHub.Domain.Tenancy.Organizations;

namespace SiteHub.Domain.Identity.Authorization;

public readonly record struct RoleId(Guid Value) : ITypedId<RoleId>
{
    public static RoleId New() => new(Guid.CreateVersion7());
    public static RoleId FromGuid(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

/// <summary>
/// Rol (ADR-0011 §6.3) — bir izin demeti.
///
/// Roller DİNAMİKTİR (DB'de), izinler STATİKTİR (kod'da). Admin yeni rol
/// yaratır, kod'daki izinlerden seçer. IsSystem=true olanlar seed (sistem
/// varsayılan rolleri).
///
/// SCOPE + OWNER MATRIX (ADR-0011 §6.3):
/// - System + IsSystem=true + owners null → SystemAdmin, SystemSupport vb.
/// - Organization + IsSystem=true + owners null → seed rol (OrganizationManager, ...)
/// - Organization + IsSystem=false + OrganizationId=ABC → ABC'nin özel rolü
/// - Site + IsSystem=true → seed Site rolleri (SiteManager, SiteStaff)
/// - Site + IsSystem=false + OrganizationId=ABC → ABC'nin kendi site rolü
/// - ServiceOrganization + IsSystem=false + ServiceOrganizationId=XYZ → XYZ'nin rolü
///
/// PRIVILEGE ESCALATION KORUMASI (ADR-0011 §6.5):
/// Yaratıcı kendi izinlerinin DIŞINA çıkamaz. Bu validasyon domain'de değil
/// application layer'da (RoleCreation use case) yapılır — çünkü creator'ın
/// "toplam izin setini" hesaplamak ComputePermissions gerektirir.
///
/// Role-Permission ilişkisi bu aggregate içinde tutulur (aggregate root).
/// </summary>
public sealed class Role : AuditableAggregateRoot<RoleId>
{
    private readonly List<RolePermission> _permissions = [];

    public string Name { get; private set; } = default!;
    public string? Description { get; private set; }
    public RoleScope Scope { get; private set; }
    public bool IsSystem { get; private set; }

    // Owner — hangi kuruma ait (null = sistem rolü)
    public OrganizationId? OrganizationId { get; private set; }
    public Guid? ServiceOrganizationId { get; private set; }   // ServiceOrganization entity v2'de gelecek

    /// <summary>Bu rolün tüm permission atamaları.</summary>
    public IReadOnlyList<RolePermission> Permissions => _permissions.AsReadOnly();

    private Role() : base() { }

    private Role(
        RoleId id,
        string name,
        string? description,
        RoleScope scope,
        bool isSystem,
        OrganizationId? organizationId,
        Guid? serviceOrganizationId)
        : base(id)
    {
        Name = name;
        Description = description;
        Scope = scope;
        IsSystem = isSystem;
        OrganizationId = organizationId;
        ServiceOrganizationId = serviceOrganizationId;
    }

    // ─── Factory'ler ─────────────────────────────────────────────────────

    /// <summary>Seed (sistem varsayılan) rolü yaratır.</summary>
    public static Role CreateSystemRole(string name, RoleScope scope, string? description = null)
    {
        ValidateName(name);
        return new Role(RoleId.New(), name, description, scope, isSystem: true, null, null);
    }

    /// <summary>Organizasyon-özel rol yaratır.</summary>
    public static Role CreateOrganizationRole(
        string name,
        OrganizationId organizationId,
        RoleScope scope,
        string? description = null)
    {
        ValidateName(name);

        if (scope != RoleScope.Organization && scope != RoleScope.Site)
            throw new BusinessRuleViolationException(
                "Organizasyon-özel rol yalnızca Organization veya Site scope'lu olabilir.");

        return new Role(RoleId.New(), name, description, scope, isSystem: false, organizationId, null);
    }

    /// <summary>Servis firması özel rolü yaratır.</summary>
    public static Role CreateServiceOrganizationRole(
        string name,
        Guid serviceOrganizationId,
        string? description = null)
    {
        ValidateName(name);
        return new Role(
            RoleId.New(), name, description,
            RoleScope.ServiceOrganization,
            isSystem: false,
            organizationId: null,
            serviceOrganizationId);
    }

    // ─── Mutasyonlar ─────────────────────────────────────────────────────

    public void Rename(string newName, string? description)
    {
        if (IsSystem)
            throw new InvalidStateException("Sistem rollerinin adı değiştirilemez.");
        ValidateName(newName);
        Name = newName;
        Description = description;
    }

    /// <summary>
    /// Role permission ekler. Privilege escalation kontrolü BURADA yapılmaz —
    /// application layer (use case handler) creator'ın izinlerini ComputePermissions
    /// ile hesaplayıp buraya sadece izin verilmiş permission'ları gönderir.
    /// </summary>
    public void AddPermission(PermissionId permissionId)
    {
        if (_permissions.Any(rp => rp.PermissionId == permissionId))
            return;  // Zaten var, idempotent

        _permissions.Add(new RolePermission(Id, permissionId, DateTimeOffset.UtcNow));
    }

    public void RemovePermission(PermissionId permissionId)
    {
        var existing = _permissions.FirstOrDefault(rp => rp.PermissionId == permissionId);
        if (existing is not null)
            _permissions.Remove(existing);
    }

    public void ClearPermissions() => _permissions.Clear();

    /// <summary>
    /// Rol silinebilir mi? Sistem rolleri silinemez + aktif membership varsa
    /// silinemez (bu kontrol application layer'da, repository sorgusuyla).
    /// </summary>
    public bool CanBeDeleted()
    {
        if (IsSystem) return false;
        return true;
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new BusinessRuleViolationException("Rol adı zorunlu.");
        if (name.Length > 100)
            throw new BusinessRuleViolationException("Rol adı en fazla 100 karakter olabilir.");
    }
}
