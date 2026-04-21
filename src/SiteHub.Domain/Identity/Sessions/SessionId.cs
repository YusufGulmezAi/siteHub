using SiteHub.Domain.Common;

namespace SiteHub.Domain.Identity.Sessions;

/// <summary>
/// Session kimliği. Guid v7 — sıralı ve benzersiz (Redis key'lerinde tutarlı sıralama).
/// </summary>
public readonly record struct SessionId(Guid Value) : ITypedId<SessionId>
{
    public static SessionId New() => new(Guid.CreateVersion7());
    public static SessionId FromGuid(Guid value) => new(value);

    /// <summary>
    /// String gösterim — Redis key'lerinde ve cookie'de kullanılır.
    /// Hex format (no dashes) → daha kompakt URL için.
    /// </summary>
    public override string ToString() => Value.ToString("N");

    public static bool TryParse(string? input, out SessionId id)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            id = default;
            return false;
        }
        if (Guid.TryParse(input, out var guid))
        {
            id = new SessionId(guid);
            return true;
        }
        id = default;
        return false;
    }
}
