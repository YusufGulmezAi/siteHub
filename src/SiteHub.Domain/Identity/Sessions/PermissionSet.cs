using SiteHub.Domain.Identity.Authorization;

namespace SiteHub.Domain.Identity.Sessions;

/// <summary>
/// Kullanıcının tüm context'lerdeki permission'larının özeti (ADR-0011 §8.1, F.6 C.2).
///
/// <para><b>Mimari (hibrit B-Cascade):</b></para>
/// <list type="bullet">
///   <item><b>System scope:</b> <c>ByContext["System"]</c> tek entry. <see cref="Has"/>
///     ilk önce buraya bakar — System'de varsa her context'te var (short-circuit).</item>
///   <item><b>Organization scope:</b> <c>ByContext["Organization:{orgId}"]</c> entry'si
///     ve cascade olarak o organizasyonun tüm site'larına da kopyalanır
///     (login zamanında <c>PermissionComputer</c> tarafından expand edilir).</item>
///   <item><b>Site scope:</b> <c>ByContext["Site:{siteId}"]</c> entry.</item>
/// </list>
///
/// <para>Runtime'da <see cref="Has"/> O(1) dictionary lookup — DB call yok.</para>
///
/// <para><b>Serialization:</b> JSON round-trip için property'ler public setter'lı.
/// <see cref="Session"/> Redis'e JSON olarak yazılıyor; PermissionSet session'ın
/// parçası.</para>
/// </summary>
public sealed class PermissionSet
{
    /// <summary>System scope için sabit context anahtarı.</summary>
    public const string SystemContextKey = "System";

    /// <summary>
    /// Context key → permission key'leri (izin sabit değerleri).
    /// Key formatı: "System", "Organization:{guid}", "Site:{guid}".
    /// </summary>
    public Dictionary<string, HashSet<string>> ByContext { get; set; } = new();

    /// <summary>Verilen context için context-key üretir.</summary>
    public static string ContextKeyOf(MembershipContextType type, Guid? id)
    {
        if (type == MembershipContextType.System)
            return SystemContextKey;

        return id.HasValue
            ? $"{type}:{id.Value}"
            : type.ToString();
    }

    /// <summary>
    /// Kullanıcı verilen context'te verilen permission'a sahip mi?
    ///
    /// <para>Kontrol sırası:</para>
    /// <list type="number">
    ///   <item>System context'te bu permission var mı → true (System her şeyi görür)</item>
    ///   <item>Spesifik context belirtilmişse: o context'te var mı → true</item>
    ///   <item>Context belirtilmemişse: herhangi bir context'te var mı → true</item>
    /// </list>
    /// </summary>
    /// <param name="permission">İzin sabiti (örn. "site.update").</param>
    /// <param name="contextType">Context tipi (null ise herhangi bir context).</param>
    /// <param name="contextId">Context ID (System için null).</param>
    public bool Has(string permission, MembershipContextType? contextType = null, Guid? contextId = null)
    {
        if (string.IsNullOrWhiteSpace(permission)) return false;

        // 1. System scope short-circuit — SystemAdmin / SystemSupport her yerde geçer
        if (ByContext.TryGetValue(SystemContextKey, out var systemPerms) &&
            systemPerms.Contains(permission))
        {
            return true;
        }

        // 2. Spesifik context belirtilmiş mi?
        if (contextType.HasValue)
        {
            var key = ContextKeyOf(contextType.Value, contextId);
            if (ByContext.TryGetValue(key, out var perms) && perms.Contains(permission))
                return true;
            return false;
        }

        // 3. Context belirtilmemiş — herhangi bir context'te varsa yeter
        foreach (var kvp in ByContext)
        {
            if (kvp.Value.Contains(permission))
                return true;
        }

        return false;
    }

    /// <summary>Permission set'te kayıt var mı (boş değil mi)?</summary>
    public bool HasAny() => ByContext.Values.Any(s => s.Count > 0);

    /// <summary>Tamamen boş bir PermissionSet (anonim kullanıcı için).</summary>
    public static PermissionSet Empty() => new();
}
