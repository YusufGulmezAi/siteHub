using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SiteHub.Domain.Geography;

namespace SiteHub.Infrastructure.Persistence.Configurations.Geography;

public sealed class NeighborhoodConfiguration : IEntityTypeConfiguration<Neighborhood>
{
    public void Configure(EntityTypeBuilder<Neighborhood> builder)
    {
        builder.ToTable("neighborhoods", schema: "geography");

        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id)
            .HasColumnName("id")
            .HasConversion(id => id.Value, v => NeighborhoodId.FromGuid(v));

        builder.Property(n => n.DistrictId)
            .HasColumnName("district_id")
            .HasConversion(id => id.Value, v => DistrictId.FromGuid(v))
            .IsRequired();

        builder.Property(n => n.ExternalId)
            .HasColumnName("external_id")
            .IsRequired();

        builder.Property(n => n.Name)
            .HasColumnName("name")
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(n => n.PostalCode)
            .HasColumnName("postal_code")
            .HasMaxLength(5)
            .UseCollation(SiteHubDbContext.TurkishCsAs);   // deterministic for equality

        builder.Ignore(n => n.DomainEvents);

        builder.HasOne<District>()
            .WithMany()
            .HasForeignKey(n => n.DistrictId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(n => n.DistrictId);
        builder.HasIndex(n => new { n.DistrictId, n.ExternalId }).IsUnique();
        builder.HasIndex(n => n.PostalCode);
        builder.HasIndex(n => n.Name);
    }
}
