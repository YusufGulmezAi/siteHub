using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SiteHub.Domain.Geography;

namespace SiteHub.Infrastructure.Persistence.Configurations.Geography;

public sealed class ProvinceConfiguration : IEntityTypeConfiguration<Province>
{
    public void Configure(EntityTypeBuilder<Province> builder)
    {
        builder.ToTable("provinces", schema: "geography");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id)
            .HasColumnName("id")
            .HasConversion(id => id.Value, v => ProvinceId.FromGuid(v));

        builder.Property(p => p.RegionId)
            .HasColumnName("region_id")
            .HasConversion(id => id.Value, v => RegionId.FromGuid(v))
            .IsRequired();

        builder.Property(p => p.ExternalId)
            .HasColumnName("external_id")
            .IsRequired();

        builder.Property(p => p.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(p => p.PlateCode)
            .HasColumnName("plate_code")
            .HasMaxLength(3)
            .UseCollation(SiteHubDbContext.TurkishCsAs)
            .IsRequired();

        builder.Ignore(p => p.DomainEvents);

        builder.HasOne<Region>()
            .WithMany()
            .HasForeignKey(p => p.RegionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => p.RegionId);
        builder.HasIndex(p => p.ExternalId).IsUnique();
        builder.HasIndex(p => p.Name);
    }
}
