using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SiteHub.Application.Abstractions.Authorization;
using SiteHub.Application.Abstractions.Persistence;
using SiteHub.Domain.Identity;
using SiteHub.Domain.Identity.Authorization;
using SiteHub.Domain.Identity.Sessions;
using SiteHub.Domain.Tenancy.Organizations;

namespace SiteHub.Application.Features.Authentication;

/// <summary>
/// <see cref="IPermissionComputer"/> EF Core implementasyonu.
///
/// <para><b>Algoritma (F.6 C.2, hibrit B-Cascade):</b></para>
/// <list type="number">
///   <item>Kullanıcının effective membership'lerini çek (active + validity aralığında)</item>
///   <item>Her membership için Role → RolePermission → Permission.Key join'i</item>
///   <item>Her membership'in context'i için PermissionSet.ByContext'e ekle</item>
///   <item>Cascade: Organization scope'un permission'larını o organizasyonun
///     tüm aktif site'larına kopyala (login zamanında bir kez, runtime lookup yok)</item>
/// </list>
///
/// <para>System scope kopyalanmaz — <see cref="PermissionSet.Has"/> zaten
/// short-circuit yapıyor ("System'de varsa her yerde geçer"). Bu sayede
/// SystemAdmin için Redis session şişmiyor.</para>
/// </summary>
public sealed class PermissionComputer : IPermissionComputer
{
    private readonly ISiteHubDbContext _db;
    private readonly ILogger<PermissionComputer> _logger;

    public PermissionComputer(ISiteHubDbContext db, ILogger<PermissionComputer> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<PermissionSet> ComputeAsync(
        LoginAccountId loginAccountId,
        DateTimeOffset now,
        CancellationToken ct = default)
    {
        var set = new PermissionSet();

        // 1. Active + effective memberships
        var memberships = await _db.Memberships
            .AsNoTracking()
            .Where(m => m.LoginAccountId == loginAccountId && m.IsActive)
            .ToListAsync(ct);

        if (memberships.Count == 0)
        {
            _logger.LogInformation(
                "PermissionComputer: kullanıcının hiç membership'i yok (account={AccountId}).",
                loginAccountId);
            return set;
        }

        // 2. Her membership için role'ün permission key'lerini yükle
        //    Küçük n (kullanıcı başına tipik 1-5 membership), N+1 sorunu dert değil.
        foreach (var m in memberships)
        {
            if (!m.IsEffectiveAt(now)) continue;

            var permissionKeys = await LoadPermissionKeysForRoleAsync(m.RoleId, ct);
            if (permissionKeys.Count == 0) continue;

            // 3. Bu membership'in kendi context'ine ekle
            var contextKey = PermissionSet.ContextKeyOf(m.ContextType, m.ContextId);
            AddPermissions(set, contextKey, permissionKeys);

            // 4. Cascade: Organization scope → o org'un tüm aktif site'larına kopyala
            if (m.ContextType == MembershipContextType.Organization && m.ContextId.HasValue)
            {
                var orgId = OrganizationId.FromGuid(m.ContextId.Value);

                var siteIds = await _db.Sites
                    .AsNoTracking()
                    .Where(s => s.OrganizationId == orgId)
                    .Select(s => s.Id.Value)
                    .ToListAsync(ct);

                foreach (var siteGuid in siteIds)
                {
                    var siteKey = PermissionSet.ContextKeyOf(
                        MembershipContextType.Site, siteGuid);
                    AddPermissions(set, siteKey, permissionKeys);
                }

                _logger.LogDebug(
                    "Cascade: Organization {OrgId} → {SiteCount} site'a permission kopyalandı.",
                    m.ContextId.Value, siteIds.Count);
            }
        }

        _logger.LogInformation(
            "PermissionComputer: account={AccountId}, context sayısı={ContextCount}, toplam permission={TotalPerms}.",
            loginAccountId,
            set.ByContext.Count,
            set.ByContext.Values.Sum(s => s.Count));

        return set;
    }

    /// <summary>
    /// Verilen rolün active (deprecated olmayan) permission key'lerini döner.
    /// Role.Permissions (RolePermission koleksiyonu) üzerinden Permission'a join.
    /// </summary>
    private async Task<List<string>> LoadPermissionKeysForRoleAsync(
        RoleId roleId, CancellationToken ct)
    {
        // Role-RolePermission aggregate ilişkisi: Role.Permissions her RolePermission'ı.
        // LINQ EF Core: rolePermission → permission → key
        var keys = await (
            from r in _db.Roles
            where r.Id == roleId
            from rp in r.Permissions
            join p in _db.Permissions on rp.PermissionId equals p.Id
            where p.DeprecatedAt == null
            select p.Key
        )
        .AsNoTracking()
        .ToListAsync(ct);

        return keys;
    }

    private static void AddPermissions(
        PermissionSet set, string contextKey, IEnumerable<string> keys)
    {
        if (!set.ByContext.TryGetValue(contextKey, out var existing))
        {
            existing = new HashSet<string>(StringComparer.Ordinal);
            set.ByContext[contextKey] = existing;
        }

        foreach (var key in keys)
            existing.Add(key);
    }
}
