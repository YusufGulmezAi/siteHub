using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SiteHub.Domain.Geography;

namespace SiteHub.Infrastructure.Persistence.Configurations.Geography;

public sealed class CountryConfiguration : IEntityTypeConfiguration<Country>
{
    public void Configure(EntityTypeBuilder<Country> builder)
    {
        builder.ToTable("countries", schema: "geography");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id)
            .HasColumnName("id")
            .HasConversion(id => id.Value, v => CountryId.FromGuid(v));

        // ISO kodu: deterministic collation (unique index için)
        builder.Property(c => c.IsoCode)
            .HasColumnName("iso_code")
            .HasMaxLength(2)
            .UseCollation(SiteHubDbContext.TurkishCsAs)
            .IsRequired();

        builder.Property(c => c.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(c => c.PhonePrefix)
            .HasColumnName("phone_prefix")
            .HasMaxLength(10);

        builder.Property(c => c.DisplayOrder)
            .HasColumnName("display_order")
            .IsRequired();

        builder.Property(c => c.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Ignore(c => c.DomainEvents);

        builder.HasIndex(c => c.IsoCode).IsUnique();
        builder.HasIndex(c => c.DisplayOrder);
    }
}
