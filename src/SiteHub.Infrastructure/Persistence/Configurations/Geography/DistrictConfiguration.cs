using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SiteHub.Domain.Geography;

namespace SiteHub.Infrastructure.Persistence.Configurations.Geography;

public sealed class DistrictConfiguration : IEntityTypeConfiguration<District>
{
    public void Configure(EntityTypeBuilder<District> builder)
    {
        builder.ToTable("districts", schema: "geography");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id)
            .HasColumnName("id")
            .HasConversion(id => id.Value, v => DistrictId.FromGuid(v));

        builder.Property(d => d.ProvinceId)
            .HasColumnName("province_id")
            .HasConversion(id => id.Value, v => ProvinceId.FromGuid(v))
            .IsRequired();

        builder.Property(d => d.ExternalId)
            .HasColumnName("external_id")
            .IsRequired();

        builder.Property(d => d.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Ignore(d => d.DomainEvents);

        builder.HasOne<Province>()
            .WithMany()
            .HasForeignKey(d => d.ProvinceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(d => d.ProvinceId);
        builder.HasIndex(d => d.ExternalId).IsUnique();
        builder.HasIndex(d => new { d.ProvinceId, d.Name });
    }
}
