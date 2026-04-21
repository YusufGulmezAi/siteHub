using Microsoft.EntityFrameworkCore;
using SiteHub.Domain.Audit;
using SiteHub.Domain.Common;
using SiteHub.Domain.Geography;
using SiteHub.Domain.Identity;
using SiteHub.Domain.Identity.Authorization;
using SiteHub.Domain.Tenancy.Organizations;

namespace SiteHub.Infrastructure.Persistence;

/// <summary>
/// SiteHub'ın ana DbContext'i.
///
/// Şemalar:
/// - public: ortak tablolar (organizations, identity, vb.)
/// - audit: denetim tabloları (entity_changes)
/// - tenant_&lt;site_id&gt;: her site için ayrı schema (ileride, ADR-0002)
/// </summary>
public class SiteHubDbContext(DbContextOptions<SiteHubDbContext> options) : DbContext(options), Application.Abstractions.Persistence.ISiteHubDbContext
{
    // ─── Collation adları ────────────────────────────────────────────────
    public const string TurkishCiAi = "tr_ci_ai";
    public const string TurkishCsAs = "tr_cs_as";

    // ─── Tenancy (ortak schema) ──────────────────────────────────────────
    public DbSet<Organization> Organizations => Set<Organization>();

    // ─── Geography (geography schema) — Referans veri ─────────────────────
    public DbSet<Country> Countries => Set<Country>();
    public DbSet<Region> Regions => Set<Region>();
    public DbSet<Province> Provinces => Set<Province>();
    public DbSet<District> Districts => Set<District>();
    public DbSet<Neighborhood> Neighborhoods => Set<Neighborhood>();
    public DbSet<Address> Addresses => Set<Address>();

    // ─── Identity (identity schema) — ADR-0011 ────────────────────────────
    public DbSet<Person> Persons => Set<Person>();
    public DbSet<LoginAccount> LoginAccounts => Set<LoginAccount>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Membership> Memberships => Set<Membership>();

    // ─── Audit (audit schema) ────────────────────────────────────────────
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("public");

        // ─── Türkçe collation'ları tanımla (ADR-0009) ───────────────────
        modelBuilder.HasCollation(
            name: TurkishCiAi,
            locale: "tr-TR-u-ks-level1",
            provider: "icu",
            deterministic: false);

        modelBuilder.HasCollation(
            name: TurkishCsAs,
            locale: "tr-TR-x-icu",
            provider: "icu",
            deterministic: true);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SiteHubDbContext).Assembly);

        // ─── Kod üretim sequence'ları (ADR-0012 §11) ────────────────────
        // Feistel cipher input'u olarak kullanılır — obfuscate edilerek
        // entity kodları üretilir (Organization, Site, Unit, UnitPeriod).
        // START 1 INCREMENT 1 — monoton artan, çakışmasız.
        modelBuilder.HasSequence<long>("seq_organization_code").StartsAt(1).IncrementsBy(1);
        modelBuilder.HasSequence<long>("seq_site_code").StartsAt(1).IncrementsBy(1);
        modelBuilder.HasSequence<long>("seq_unit_code").StartsAt(1).IncrementsBy(1);
        modelBuilder.HasSequence<long>("seq_unit_period_code").StartsAt(1).IncrementsBy(1);

        // ─── Global query filter: ISoftDeletable entity'lerden silinenleri gizle (ADR-0006) ───
        // Bu sayede: dbContext.Firms.ToList() → silinenler DAHİL DEĞİL (otomatik)
        // Silinenleri de görmek: dbContext.Firms.IgnoreQueryFilters().ToList()
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
            {
                var method = typeof(SiteHubDbContext)
                    .GetMethod(nameof(ApplySoftDeleteFilter),
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                    .MakeGenericMethod(entityType.ClrType);

                method.Invoke(null, new object[] { modelBuilder });
            }
        }
    }

    // Generic helper — tip-güvenli bir şekilde query filter uygular
    private static void ApplySoftDeleteFilter<TEntity>(ModelBuilder builder)
        where TEntity : class, ISoftDeletable
    {
        builder.Entity<TEntity>().HasQueryFilter(e => e.DeletedAt == null);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        configurationBuilder
            .Properties<DateTimeOffset>()
            .HaveColumnType("timestamp with time zone");

        configurationBuilder
            .Properties<string>()
            .UseCollation(TurkishCiAi);
    }
}
