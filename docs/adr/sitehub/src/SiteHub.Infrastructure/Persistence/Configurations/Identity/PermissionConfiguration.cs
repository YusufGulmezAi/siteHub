using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SiteHub.Domain.Identity.Authorization;

namespace SiteHub.Infrastructure.Persistence.Configurations.Identity;

public sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("permissions", schema: "identity");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id)
            .HasColumnName("id")
            .HasConversion(id => id.Value, v => PermissionId.FromGuid(v));

        builder.Property(p => p.Key)
            .HasColumnName("key")
            .HasMaxLength(100)
            .UseCollation(SiteHubDbContext.TurkishCsAs)  // unique index + equality
            .IsRequired();

        builder.Property(p => p.Resource)
            .HasColumnName("resource")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(p => p.Action)
            .HasColumnName("action")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(p => p.Description)
            .HasColumnName("description")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(p => p.DeprecatedAt)
            .HasColumnName("deprecated_at")
            .HasColumnType("timestamp with time zone");

        builder.Ignore(p => p.IsDeprecated);
        builder.Ignore(p => p.DomainEvents);

        builder.HasIndex(p => p.Key).IsUnique();
        builder.HasIndex(p => p.Resource);
        builder.HasIndex(p => p.DeprecatedAt);
    }
}
