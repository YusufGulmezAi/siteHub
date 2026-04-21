using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SiteHub.Domain.Geography;

namespace SiteHub.Infrastructure.Persistence.Configurations.Geography;

public sealed class AddressConfiguration : IEntityTypeConfiguration<Address>
{
    public void Configure(EntityTypeBuilder<Address> builder)
    {
        builder.ToTable("addresses", schema: "geography");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id)
            .HasColumnName("id")
            .HasConversion(id => id.Value, v => AddressId.FromGuid(v));

        builder.Property(a => a.NeighborhoodId)
            .HasColumnName("neighborhood_id")
            .HasConversion(id => id.Value, v => NeighborhoodId.FromGuid(v))
            .IsRequired();

        builder.Property(a => a.AddressLine1)
            .HasColumnName("address_line_1")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(a => a.AddressLine2)
            .HasColumnName("address_line_2")
            .HasMaxLength(500);

        builder.Property(a => a.PostalCode)
            .HasColumnName("postal_code")
            .HasMaxLength(5)
            .UseCollation(SiteHubDbContext.TurkishCsAs);

        // ─── Audit alanları (IAuditable) ────────────────────────────────
        builder.Property(a => a.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();
        builder.Property(a => a.CreatedById).HasColumnName("created_by_id");
        builder.Property(a => a.CreatedByName)
            .HasColumnName("created_by_name")
            .HasMaxLength(300);

        builder.Property(a => a.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone");
        builder.Property(a => a.UpdatedById).HasColumnName("updated_by_id");
        builder.Property(a => a.UpdatedByName)
            .HasColumnName("updated_by_name")
            .HasMaxLength(300);

        // ─── Soft-delete (ISoftDeletable) ───────────────────────────────
        builder.Property(a => a.DeletedAt)
            .HasColumnName("deleted_at")
            .HasColumnType("timestamp with time zone");
        builder.Property(a => a.DeletedById).HasColumnName("deleted_by_id");
        builder.Property(a => a.DeletedByName)
            .HasColumnName("deleted_by_name")
            .HasMaxLength(300);
        builder.Property(a => a.DeleteReason)
            .HasColumnName("delete_reason")
            .HasMaxLength(1000);

        builder.Ignore(a => a.DomainEvents);
        builder.Ignore(a => a.IsDeleted);

        builder.HasOne<Neighborhood>()
            .WithMany()
            .HasForeignKey(a => a.NeighborhoodId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => a.NeighborhoodId);
        builder.HasIndex(a => a.PostalCode);
        builder.HasIndex(a => a.DeletedAt);
    }
}
