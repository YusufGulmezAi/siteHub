using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SiteHub.Domain.Identity;
using SiteHub.Domain.Tenancy.Organizations;

namespace SiteHub.Infrastructure.Persistence.Configurations;

public sealed class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.ToTable("organizations", schema: "public");

        // Strongly-typed ID → Guid converter
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id)
            .HasColumnName("id")
            .HasConversion(id => id.Value, v => OrganizationId.FromGuid(v));

        // Görünür alanlar — tr_ci_ai (case+accent insensitive, equality/ORDER BY için)
        builder.Property(o => o.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(o => o.CommercialTitle)
            .HasColumnName("commercial_title")
            .HasMaxLength(500)
            .IsRequired();

        // TaxId — deterministic collation (unique index için zorunlu)
        builder.Property(o => o.TaxId)
            .HasColumnName("tax_id")
            .HasMaxLength(11)
            .UseCollation(SiteHubDbContext.TurkishCsAs)
            .HasConversion(
                id => id != null ? id.Value : null,
                str => str != null ? NationalId.Parse(str) : null);

        builder.Property(o => o.Address).HasColumnName("address").HasMaxLength(1000);
        builder.Property(o => o.Phone).HasColumnName("phone").HasMaxLength(30);
        builder.Property(o => o.Email).HasColumnName("email").HasMaxLength(320);

        builder.Property(o => o.IsActive).HasColumnName("is_active").IsRequired();

        // ─── SearchText — DETERMINISTIC collation, partial search için ─────
        // ILIKE / LIKE operatörleri non-deterministic collation'da çalışmaz.
        // Bu kolon TurkishCsAs (deterministic) kullanır → ILIKE sorunsuz.
        // İçerik zaten C# tarafında Türkçe lower'lanmış (TurkishNormalizer),
        // dolayısıyla "Şişli" yazılınca "şişli" olarak arar, equality gider.
        builder.Property(o => o.SearchText)
            .HasColumnName("search_text")
            .HasMaxLength(2000)  // tüm alanların birleşimi için yer
            .UseCollation(SiteHubDbContext.TurkishCsAs)
            .IsRequired();

        // ─── Audit alanları (IAuditable) ────────────────────────────────
        builder.Property(o => o.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();
        builder.Property(o => o.CreatedById).HasColumnName("created_by_id");
        builder.Property(o => o.CreatedByName)
            .HasColumnName("created_by_name")
            .HasMaxLength(300);

        builder.Property(o => o.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone");
        builder.Property(o => o.UpdatedById).HasColumnName("updated_by_id");
        builder.Property(o => o.UpdatedByName)
            .HasColumnName("updated_by_name")
            .HasMaxLength(300);

        // ─── Soft-delete (ISoftDeletable) ───────────────────────────────
        builder.Property(o => o.DeletedAt)
            .HasColumnName("deleted_at")
            .HasColumnType("timestamp with time zone");
        builder.Property(o => o.DeletedById).HasColumnName("deleted_by_id");
        builder.Property(o => o.DeletedByName)
            .HasColumnName("deleted_by_name")
            .HasMaxLength(300);
        builder.Property(o => o.DeleteReason)
            .HasColumnName("delete_reason")
            .HasMaxLength(1000);

        builder.Ignore(o => o.DomainEvents);

        // ─── Index'ler ──────────────────────────────────────────────────
        builder.HasIndex(o => o.TaxId).IsUnique()
            .HasFilter("tax_id IS NOT NULL AND deleted_at IS NULL");
        builder.HasIndex(o => o.Name);
        builder.HasIndex(o => o.IsActive);
        builder.HasIndex(o => o.DeletedAt);
        builder.HasIndex(o => o.SearchText);  // partial search performansı için
    }
}
