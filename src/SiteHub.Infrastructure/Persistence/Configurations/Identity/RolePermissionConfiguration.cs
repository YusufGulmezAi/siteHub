using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SiteHub.Domain.Identity.Authorization;

namespace SiteHub.Infrastructure.Persistence.Configurations.Identity;

public sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("role_permissions", schema: "identity");

        // Composite PK: (RoleId, PermissionId)
        builder.HasKey(rp => new { rp.RoleId, rp.PermissionId });

        builder.Property(rp => rp.RoleId)
            .HasColumnName("role_id")
            .HasConversion(id => id.Value, v => RoleId.FromGuid(v))
            .IsRequired();

        builder.Property(rp => rp.PermissionId)
            .HasColumnName("permission_id")
            .HasConversion(id => id.Value, v => PermissionId.FromGuid(v))
            .IsRequired();

        builder.Property(rp => rp.GrantedAt)
            .HasColumnName("granted_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        // FK: RolePermission → Permission (Role FK Role config'inde)
        builder.HasOne<Permission>()
            .WithMany()
            .HasForeignKey(rp => rp.PermissionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(rp => rp.PermissionId);
    }
}
