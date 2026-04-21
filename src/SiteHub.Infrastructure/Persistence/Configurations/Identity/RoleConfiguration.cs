using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SiteHub.Domain.Identity.Authorization;
using SiteHub.Domain.Tenancy.Organizations;

namespace SiteHub.Infrastructure.Persistence.Configurations.Identity;

public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("roles", schema: "identity");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id)
            .HasColumnName("id")
            .HasConversion(id => id.Value, v => RoleId.FromGuid(v));

        builder.Property(r => r.Name)
            .HasColumnName("name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(r => r.Description)
            .HasColumnName("description")
            .HasMaxLength(500);

        builder.Property(r => r.Scope)
            .HasColumnName("scope")
            .HasConversion<short>()
            .IsRequired();

        builder.Property(r => r.IsSystem)
            .HasColumnName("is_system")
            .IsRequired();

        builder.Property(r => r.OrganizationId)
            .HasColumnName("organization_id")
            .HasConversion(
                id => id.HasValue ? id.Value.Value : (Guid?)null,
                v => v.HasValue ? OrganizationId.FromGuid(v.Value) : null);

        builder.Property(r => r.ServiceOrganizationId)
            .HasColumnName("service_organization_id");

        // Audit + soft-delete (AuditableAggregateRoot'tan gelen alanlar)
        builder.Property(r => r.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();
        builder.Property(r => r.CreatedById).HasColumnName("created_by_id");
        builder.Property(r => r.CreatedByName).HasColumnName("created_by_name").HasMaxLength(300);
        builder.Property(r => r.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone");
        builder.Property(r => r.UpdatedById).HasColumnName("updated_by_id");
        builder.Property(r => r.UpdatedByName).HasColumnName("updated_by_name").HasMaxLength(300);
        builder.Property(r => r.DeletedAt)
            .HasColumnName("deleted_at")
            .HasColumnType("timestamp with time zone");
        builder.Property(r => r.DeletedById).HasColumnName("deleted_by_id");
        builder.Property(r => r.DeletedByName).HasColumnName("deleted_by_name").HasMaxLength(300);
        builder.Property(r => r.DeleteReason).HasColumnName("delete_reason").HasMaxLength(1000);

        builder.Ignore(r => r.DomainEvents);

        // ─── Permissions (owned collection — aggregate'in parçası) ──────
        builder.HasMany(r => r.Permissions)
            .WithOne()
            .HasForeignKey(rp => rp.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        // Optional FK: Role → Organization (organizasyon-özel rol için)
        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(r => r.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => new { r.Scope, r.IsSystem });
        builder.HasIndex(r => r.OrganizationId);
        builder.HasIndex(r => r.ServiceOrganizationId);
        builder.HasIndex(r => r.Name);
    }
}
