using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SiteHub.Domain.Geography;

namespace SiteHub.Infrastructure.Persistence.Configurations.Geography;

public sealed class RegionConfiguration : IEntityTypeConfiguration<Region>
{
    public void Configure(EntityTypeBuilder<Region> builder)
    {
        builder.ToTable("regions", schema: "geography");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id)
            .HasColumnName("id")
            .HasConversion(id => id.Value, v => RegionId.FromGuid(v));

        builder.Property(r => r.CountryId)
            .HasColumnName("country_id")
            .HasConversion(id => id.Value, v => CountryId.FromGuid(v))
            .IsRequired();

        builder.Property(r => r.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(r => r.Code)
            .HasColumnName("code")
            .HasMaxLength(50)
            .UseCollation(SiteHubDbContext.TurkishCsAs)
            .IsRequired();

        builder.Property(r => r.DisplayOrder)
            .HasColumnName("display_order")
            .IsRequired();

        builder.Ignore(r => r.DomainEvents);

        // FK → Country (explicit, navigation property yok — referans veri, sade)
        builder.HasOne<Country>()
            .WithMany()
            .HasForeignKey(r => r.CountryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => r.CountryId);
        builder.HasIndex(r => new { r.CountryId, r.Code }).IsUnique();
    }
}
