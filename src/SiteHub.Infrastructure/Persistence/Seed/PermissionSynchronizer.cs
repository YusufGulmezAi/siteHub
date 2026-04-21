using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SiteHub.Domain.Identity.Authorization;
using SiteHub.Shared.Authorization;

namespace SiteHub.Infrastructure.Persistence.Seed;

/// <summary>
/// Kod'daki <c>SiteHub.Shared.Authorization.Permissions</c> sabitleri ile
/// <c>identity.permissions</c> tablosunu senkronize eder (ADR-0011 §6.2).
///
/// Akış:
/// 1. Reflection ile tüm <c>Permissions</c> nested class'larındaki const string'ler okunur
/// 2. Her biri için DB kaydı kontrol edilir:
///    - Yoksa → eklenir
///    - Varsa + deprecated ise → undeprecate edilir
/// 3. DB'de olan ama kod'da olmayan → DeprecatedAt = now (SİLİNMEZ)
///
/// İdempotent: Her startup'ta güvenle çalışır.
///
/// Description: Her izin için Türkçe açıklama — dictionary içinde tutulur.
/// Yeni izin eklenince buraya da yazılmazsa Key'den türetilir (genel metin).
/// </summary>
public sealed class PermissionSynchronizer
{
    private readonly SiteHubDbContext _db;
    private readonly ILogger<PermissionSynchronizer> _logger;

    public PermissionSynchronizer(SiteHubDbContext db, ILogger<PermissionSynchronizer> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task SynchronizeAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Permission senkronizasyonu başlatılıyor...");

        var codePermissions = DiscoverCodePermissions();
        _logger.LogInformation("Kod'da {Count} permission bulundu.", codePermissions.Count);

        var dbPermissions = await _db.Permissions.ToListAsync(ct);
        var dbByKey = dbPermissions.ToDictionary(p => p.Key, p => p);

        var added = 0;
        var reactivated = 0;
        var deprecated = 0;

        // 1. Kod'dakiler → DB'ye ekle veya undeprecate
        foreach (var (key, (resource, action)) in codePermissions)
        {
            var description = PermissionDescriptions.GetOrDefault(key);

            if (!dbByKey.TryGetValue(key, out var existing))
            {
                var permission = Permission.Create(key, resource, action, description);
                _db.Permissions.Add(permission);
                added++;
            }
            else if (existing.IsDeprecated)
            {
                existing.Undeprecate();
                existing.UpdateDescription(description);
                reactivated++;
            }
            else
            {
                // Description güncellenmesi (kod'daki açıklama değişmişse)
                if (existing.Description != description)
                    existing.UpdateDescription(description);
            }
        }

        // 2. DB'de var + kod'da yok → deprecate
        foreach (var dbPerm in dbPermissions)
        {
            if (!codePermissions.ContainsKey(dbPerm.Key) && !dbPerm.IsDeprecated)
            {
                dbPerm.Deprecate();
                deprecated++;
                _logger.LogWarning(
                    "Permission '{Key}' kod'dan kaldırılmış, deprecate edildi.",
                    dbPerm.Key);
            }
        }

        if (added > 0 || reactivated > 0 || deprecated > 0)
            await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Permission senkronizasyonu tamamlandı. Eklendi: {Added}, Reaktive: {Reactivated}, Deprecate: {Deprecated}",
            added, reactivated, deprecated);
    }

    /// <summary>
    /// Reflection ile <c>Permissions</c> sınıfının tüm nested sınıflarından
    /// const string'leri toplar.
    /// </summary>
    private static Dictionary<string, (string Resource, string Action)> DiscoverCodePermissions()
    {
        var result = new Dictionary<string, (string, string)>();
        var nestedTypes = typeof(Permissions).GetNestedTypes(
            BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);

        foreach (var nestedType in nestedTypes)
        {
            var resource = nestedType.Name;   // "Site", "Organization" — PascalCase

            var constants = nestedType.GetFields(
                BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string));

            foreach (var field in constants)
            {
                var value = (string)field.GetRawConstantValue()!;
                var action = field.Name;   // "Read", "Create" — PascalCase
                result[value] = (resource, action);
            }
        }

        return result;
    }
}

/// <summary>
/// Permission açıklamaları — kod'daki her sabitin Türkçe karşılığı.
/// Yeni izin eklerken BURAYA DA YAZ — yoksa default açıklama üretilir.
/// </summary>
internal static class PermissionDescriptions
{
    private static readonly Dictionary<string, string> _descriptions = new()
    {
        // System
        [Permissions.System.Read]        = "Sistem seviyesi bilgileri görüntüleme",
        [Permissions.System.Manage]      = "Sistem ayarlarını yönetme (tüm yetkiler)",
        [Permissions.System.Impersonate] = "Başka kullanıcı kılığında işlem yapma (destek)",

        // Organization
        [Permissions.Organization.Read]         = "Organizasyon detaylarını görüntüleme",
        [Permissions.Organization.Create]       = "Yeni organizasyon oluşturma",
        [Permissions.Organization.Update]       = "Organizasyon bilgilerini güncelleme",
        [Permissions.Organization.Delete]       = "Organizasyonu silme (soft-delete)",
        [Permissions.Organization.Analytics]    = "Organizasyon analitik ve raporlarını görme",
        [Permissions.Organization.BankManage]   = "Organizasyon banka hesaplarını yönetme",
        [Permissions.Organization.BranchManage] = "Organizasyon şubelerini yönetme",
        [Permissions.Organization.ContractSign] = "Servis sözleşmesi imzalama yetkisi",

        // Site
        [Permissions.Site.Read]           = "Site detaylarını görüntüleme",
        [Permissions.Site.Create]         = "Yeni site oluşturma",
        [Permissions.Site.Update]         = "Site bilgilerini güncelleme",
        [Permissions.Site.Delete]         = "Siteyi silme (soft-delete)",
        [Permissions.Site.Analytics]      = "Site analitik ve raporlarını görme",
        [Permissions.Site.StructureEdit]  = "Site yapısını (blok/BB/hissedar) düzenleme",
        [Permissions.Site.DocumentUpload] = "Site belge merkezine yükleme",
        [Permissions.Site.BankManage]     = "Site banka hesaplarını yönetme",

        // Period
        [Permissions.Period.Read]   = "BB dönem bilgilerini görüntüleme",
        [Permissions.Period.Create] = "Yeni BB dönemi oluşturma",
        [Permissions.Period.Update] = "BB dönemini güncelleme",
        [Permissions.Period.Close]  = "BB dönemini kapatma (malik devri)",

        // Person
        [Permissions.Person.Read]   = "Kişi bilgilerini görüntüleme",
        [Permissions.Person.Create] = "Yeni kişi oluşturma",
        [Permissions.Person.Update] = "Kişi bilgilerini güncelleme",

        // ServiceContract
        [Permissions.ServiceContract.Read]      = "Servis sözleşmesi görüntüleme",
        [Permissions.ServiceContract.Create]    = "Servis sözleşmesi oluşturma",
        [Permissions.ServiceContract.Update]    = "Servis sözleşmesi güncelleme",
        [Permissions.ServiceContract.Terminate] = "Servis sözleşmesi feshi",

        // Approval
        [Permissions.Approval.Approve]      = "Onay süreçlerini onaylama/reddetme",
        [Permissions.Approval.PolicyManage] = "Onay politikalarını yönetme",
    };

    public static string GetOrDefault(string key) =>
        _descriptions.TryGetValue(key, out var desc) ? desc : $"İzin: {key}";
}
