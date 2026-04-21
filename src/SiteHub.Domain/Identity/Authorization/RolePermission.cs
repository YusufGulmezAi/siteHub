using SiteHub.Domain.Common;

namespace SiteHub.Domain.Identity.Authorization;

/// <summary>
/// Role-Permission bağı (many-to-many join, aggregate root: Role).
///
/// Role aggregate'inin parçası — bağımsız yaratılmaz/değiştirilmez.
/// Role.AddPermission / RemovePermission metodları üzerinden yönetilir.
///
/// Composite key: (RoleId, PermissionId) — aynı izin bir role iki kez eklenemez.
/// </summary>
public sealed class RolePermission
{
    public RoleId RoleId { get; private set; }
    public PermissionId PermissionId { get; private set; }
    public DateTimeOffset GrantedAt { get; private set; }

    // EF Core için private
    private RolePermission() { }

    internal RolePermission(RoleId roleId, PermissionId permissionId, DateTimeOffset grantedAt)
    {
        RoleId = roleId;
        PermissionId = permissionId;
        GrantedAt = grantedAt;
    }
}
