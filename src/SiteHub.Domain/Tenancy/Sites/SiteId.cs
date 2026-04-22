using SiteHub.Domain.Common;

namespace SiteHub.Domain.Tenancy.Sites;

/// <summary>
/// Site (Apartman/Kompleks) için strongly-typed ID.
///
/// Kullanım:
///   var id = SiteId.New();
///   var fromDb = SiteId.FromGuid(savedGuid);
/// </summary>
public readonly record struct SiteId(Guid Value) : ITypedId<SiteId>
{
    public static SiteId New() => new(Guid.CreateVersion7());
    public static SiteId FromGuid(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
