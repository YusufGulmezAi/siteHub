using Microsoft.EntityFrameworkCore;
using SiteHub.Domain.Audit;
using SiteHub.Domain.Geography;
using SiteHub.Domain.Identity;
using SiteHub.Domain.Identity.Authorization;
using SiteHub.Domain.Tenancy.Organizations;

namespace SiteHub.Application.Abstractions.Persistence;

/// <summary>
/// Application katmanında kullanılan DbContext soyutlaması.
///
/// <para>Neden? Use case handler'ları (LoginHandler, CreateOrganizationHandler, ...) EF Core'a
/// doğrudan bağımlı olmasın diye. Test'lerde InMemoryDbContext veya mock ile replace edilebilir.</para>
///
/// <para>Implementation: SiteHubDbContext (Infrastructure) — <c>services.AddScoped&lt;ISiteHubDbContext&gt;(sp =&gt;
/// sp.GetRequiredService&lt;SiteHubDbContext&gt;())</c>.</para>
///
/// <para>NOT: DbSet'ler burada salt yazılabilir olmak zorunda değil — EF Core Add/Remove
/// DbSet üzerinden çalışıyor, interface'ten erişildiğinde de aynı davranış.</para>
/// </summary>
public interface ISiteHubDbContext
{
    // Tenancy
    DbSet<Organization> Organizations { get; }

    // Geography
    DbSet<Country> Countries { get; }
    DbSet<Region> Regions { get; }
    DbSet<Province> Provinces { get; }
    DbSet<District> Districts { get; }
    DbSet<Neighborhood> Neighborhoods { get; }
    DbSet<Address> Addresses { get; }

    // Identity
    DbSet<Person> Persons { get; }
    DbSet<LoginAccount> LoginAccounts { get; }
    DbSet<Permission> Permissions { get; }
    DbSet<Role> Roles { get; }
    DbSet<Membership> Memberships { get; }
    DbSet<PasswordResetToken> PasswordResetTokens { get; }

    // Audit
    DbSet<AuditEntry> AuditEntries { get; }

    /// <summary>
    /// Değişiklikleri kaydet (SaveChangesAsync wrapper).
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
