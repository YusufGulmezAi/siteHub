using SiteHub.Domain.Common;

namespace SiteHub.Domain.Tenancy.Organizations;

/// <summary>
/// Organization (Kiracı / Yönetim Firması) için strongly-typed ID.
///
/// Kullanım:
///   var id = OrganizationId.New();
///   var fromDb = OrganizationId.FromGuid(savedGuid);
/// </summary>
public readonly record struct OrganizationId(Guid Value) : ITypedId<OrganizationId>
{
    public static OrganizationId New() => new(Guid.CreateVersion7());
    public static OrganizationId FromGuid(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
