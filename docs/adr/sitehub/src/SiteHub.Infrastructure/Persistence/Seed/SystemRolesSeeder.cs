using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SiteHub.Domain.Identity.Authorization;
using SiteHub.Shared.Authorization;

namespace SiteHub.Infrastructure.Persistence.Seed;

/// <summary>
/// Varsayılan sistem rollerini seed eder (ADR-0011 §6.6).
///
/// PermissionSynchronizer'dan SONRA çalışmalı (permission'lar DB'de olmalı).
///
/// Seed rolleri:
/// - Sistem Yöneticisi (System): tüm izinler
/// - Sistem Destek (System): okuma izinleri
/// - Organizasyon Sahibi (Organization): org.* + site.* + person.* + period.*
/// - Organizasyon Yöneticisi (Organization): kısıtlı (delete yok)
/// - Organizasyon Muhasebeci (Organization): read-only
/// - Site Yöneticisi (Site): site.* + period.*
/// - Site Teknisyen (Site): read-only
/// - Servis Firması Yöneticisi (ServiceOrganization): kısıtlı
///
/// Idempotent: Role adıyla (+ Scope + IsSystem) eşleşen kayıt varsa atlar.
/// Permission'ları her seferinde tazeler (kod güncellenmişse DB de güncellenir).
/// </summary>
public sealed class SystemRolesSeeder
{
    private readonly SiteHubDbContext _db;
    private readonly ILogger<SystemRolesSeeder> _logger;

    public SystemRolesSeeder(SiteHubDbContext db, ILogger<SystemRolesSeeder> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Sistem rolleri seed başlatılıyor...");

        // Permission key → PermissionId map'i
        var permissions = await _db.Permissions
            .Where(p => p.DeprecatedAt == null)
            .ToDictionaryAsync(p => p.Key, p => p.Id, ct);

        var definitions = BuildRoleDefinitions();

        foreach (var def in definitions)
        {
            var existing = await _db.Roles
                .Include(r => r.Permissions)
                .FirstOrDefaultAsync(r =>
                    r.Name == def.Name &&
                    r.Scope == def.Scope &&
                    r.IsSystem, ct);

            if (existing is null)
            {
                var role = Role.CreateSystemRole(def.Name, def.Scope, def.Description);
                AssignPermissions(role, def.PermissionKeys, permissions);
                _db.Roles.Add(role);
                _logger.LogInformation(
                    "Sistem rolü eklendi: {Name} ({Scope}), {Count} izin.",
                    def.Name, def.Scope, role.Permissions.Count);
            }
            else
            {
                // Sistem rolünün izinlerini tazele (kod güncellenmişse)
                RefreshPermissions(existing, def.PermissionKeys, permissions);
                _logger.LogDebug("Sistem rolü tazelendi: {Name}.", def.Name);
            }
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Sistem rolleri seed tamamlandı.");
    }

    private static void AssignPermissions(
        Role role,
        IEnumerable<string> permissionKeys,
        IReadOnlyDictionary<string, PermissionId> permMap)
    {
        foreach (var key in permissionKeys)
        {
            if (permMap.TryGetValue(key, out var permId))
                role.AddPermission(permId);
        }
    }

    private static void RefreshPermissions(
        Role role,
        IReadOnlyList<string> desiredKeys,
        IReadOnlyDictionary<string, PermissionId> permMap)
    {
        var desiredIds = desiredKeys
            .Where(permMap.ContainsKey)
            .Select(k => permMap[k])
            .ToHashSet();

        // Kaldırılacaklar
        var toRemove = role.Permissions
            .Where(rp => !desiredIds.Contains(rp.PermissionId))
            .Select(rp => rp.PermissionId)
            .ToList();

        foreach (var id in toRemove)
            role.RemovePermission(id);

        // Eklenecekler
        foreach (var id in desiredIds)
            role.AddPermission(id);
    }

    private static IReadOnlyList<RoleDefinition> BuildRoleDefinitions()
    {
        return new List<RoleDefinition>
        {
            // System
            new(
                "Sistem Yöneticisi",
                RoleScope.System,
                "Sistem genelinde tam yetki",
                AllPermissions()),

            new(
                "Sistem Destek",
                RoleScope.System,
                "Müşteri destek — salt okuma yetkileri",
                new[]
                {
                    Permissions.System.Read,
                    Permissions.Organization.Read,
                    Permissions.Site.Read,
                    Permissions.Person.Read,
                    Permissions.Period.Read,
                    Permissions.ServiceContract.Read
                }),

            // Organization
            new(
                "Organizasyon Sahibi",
                RoleScope.Organization,
                "Organizasyonun tüm yetkilerine sahip sahibi/genel müdür",
                new[]
                {
                    Permissions.Organization.Read, Permissions.Organization.Update,
                    Permissions.Organization.Analytics, Permissions.Organization.BankManage,
                    Permissions.Organization.BranchManage, Permissions.Organization.ContractSign,
                    Permissions.Site.Read, Permissions.Site.Create, Permissions.Site.Update,
                    Permissions.Site.Delete, Permissions.Site.Analytics,
                    Permissions.Site.StructureEdit, Permissions.Site.DocumentUpload,
                    Permissions.Site.BankManage,
                    Permissions.Period.Read, Permissions.Period.Create,
                    Permissions.Period.Update, Permissions.Period.Close,
                    Permissions.Person.Read, Permissions.Person.Create, Permissions.Person.Update,
                    Permissions.ServiceContract.Read, Permissions.ServiceContract.Create,
                    Permissions.ServiceContract.Update, Permissions.ServiceContract.Terminate,
                    Permissions.Approval.Approve, Permissions.Approval.PolicyManage
                }),

            new(
                "Organizasyon Yöneticisi",
                RoleScope.Organization,
                "Günlük yönetim — silme yetkisi yok",
                new[]
                {
                    Permissions.Organization.Read, Permissions.Organization.Update,
                    Permissions.Organization.BankManage, Permissions.Organization.BranchManage,
                    Permissions.Site.Read, Permissions.Site.Create, Permissions.Site.Update,
                    Permissions.Site.Analytics, Permissions.Site.StructureEdit,
                    Permissions.Site.DocumentUpload, Permissions.Site.BankManage,
                    Permissions.Period.Read, Permissions.Period.Create, Permissions.Period.Update,
                    Permissions.Person.Read, Permissions.Person.Create, Permissions.Person.Update,
                    Permissions.ServiceContract.Read, Permissions.ServiceContract.Create,
                    Permissions.ServiceContract.Update,
                    Permissions.Approval.Approve
                }),

            new(
                "Organizasyon Muhasebeci",
                RoleScope.Organization,
                "Muhasebe görüntüleme ve raporlama",
                new[]
                {
                    Permissions.Organization.Read,
                    Permissions.Site.Read, Permissions.Site.Analytics,
                    Permissions.Period.Read,
                    Permissions.Person.Read
                }),

            // Site
            new(
                "Site Yöneticisi",
                RoleScope.Site,
                "Site seviyesinde yönetim yetkisi",
                new[]
                {
                    Permissions.Site.Read, Permissions.Site.Update,
                    Permissions.Site.StructureEdit, Permissions.Site.DocumentUpload,
                    Permissions.Site.Analytics,
                    Permissions.Period.Read, Permissions.Period.Create,
                    Permissions.Period.Update, Permissions.Period.Close,
                    Permissions.Person.Read, Permissions.Person.Create, Permissions.Person.Update,
                    Permissions.Approval.Approve
                }),

            new(
                "Site Teknisyen",
                RoleScope.Site,
                "Sadece okuma yetkisi (güvenlik, bekçi, teknik personel)",
                new[]
                {
                    Permissions.Site.Read,
                    Permissions.Period.Read
                }),

            // ServiceOrganization
            new(
                "Servis Firması Yöneticisi",
                RoleScope.ServiceOrganization,
                "Servis firması personelini yönetir, sözleşmelerini görür",
                new[]
                {
                    Permissions.ServiceContract.Read,
                    Permissions.Person.Read, Permissions.Person.Create, Permissions.Person.Update
                })
        };
    }

    /// <summary>
    /// Reflection ile Permissions sınıfındaki tüm sabitleri listeler (SystemAdmin için).
    /// </summary>
    private static IReadOnlyList<string> AllPermissions()
    {
        var result = new List<string>();
        foreach (var nestedType in typeof(Permissions).GetNestedTypes())
        {
            var constants = nestedType.GetFields(
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.Static)
                .Where(f => f.IsLiteral && f.FieldType == typeof(string));

            foreach (var field in constants)
            {
                var value = (string?)field.GetRawConstantValue();
                if (value is not null) result.Add(value);
            }
        }
        return result;
    }

    private sealed record RoleDefinition(
        string Name,
        RoleScope Scope,
        string Description,
        IReadOnlyList<string> PermissionKeys);
}
