using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SiteHub.Domain.Geography;
using SiteHub.Domain.Identity;

namespace SiteHub.Infrastructure.Persistence.Configurations.Identity;

public sealed class PersonConfiguration : IEntityTypeConfiguration<Person>
{
    public void Configure(EntityTypeBuilder<Person> builder)
    {
        builder.ToTable("persons", schema: "identity");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id)
            .HasColumnName("id")
            .HasConversion(id => id.Value, v => PersonId.FromGuid(v));

        // ─── NationalId value object ────────────────────────────────────
        // İki ayrı kolon: value + type
        builder.Property(p => p.NationalId)
            .HasColumnName("national_id")
            .HasMaxLength(11)
            .UseCollation(SiteHubDbContext.TurkishCsAs)   // deterministic (unique index için)
            .HasConversion(
                v => v.Value,
                str => NationalId.Parse(str))
            .IsRequired();

        builder.Property(p => p.PersonType)
            .HasColumnName("person_type")
            .HasConversion<short>()
            .IsRequired();

        // ─── Görünür alanlar ────────────────────────────────────────────
        builder.Property(p => p.FullName)
            .HasColumnName("full_name")
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(p => p.MobilePhone)
            .HasColumnName("mobile_phone")
            .HasMaxLength(20)
            .UseCollation(SiteHubDbContext.TurkishCsAs)   // equality için deterministic
            .IsRequired();

        builder.Property(p => p.Email)
            .HasColumnName("email")
            .HasMaxLength(320)
            .UseCollation(SiteHubDbContext.TurkishCsAs);  // equality + case-insensitive hash için

        builder.Property(p => p.KepAddress)
            .HasColumnName("kep_address")
            .HasMaxLength(320)
            .UseCollation(SiteHubDbContext.TurkishCsAs);

        builder.Property(p => p.ProfilePhotoUrl)
            .HasColumnName("profile_photo_url")
            .HasMaxLength(1000);

        builder.Property(p => p.NotificationAddressId)
            .HasColumnName("notification_address_id")
            .HasConversion(
                id => id.HasValue ? id.Value.Value : (Guid?)null,
                v => v.HasValue ? AddressId.FromGuid(v.Value) : null);

        builder.Property(p => p.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        // ─── SearchText ─────────────────────────────────────────────────
        builder.Property(p => p.SearchText)
            .HasColumnName("search_text")
            .HasMaxLength(2000)
            .UseCollation(SiteHubDbContext.TurkishCsAs)
            .IsRequired();

        // ─── Audit ──────────────────────────────────────────────────────
        builder.Property(p => p.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();
        builder.Property(p => p.CreatedById).HasColumnName("created_by_id");
        builder.Property(p => p.CreatedByName)
            .HasColumnName("created_by_name").HasMaxLength(300);

        builder.Property(p => p.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone");
        builder.Property(p => p.UpdatedById).HasColumnName("updated_by_id");
        builder.Property(p => p.UpdatedByName)
            .HasColumnName("updated_by_name").HasMaxLength(300);

        // ─── Soft-delete (Person FİZİKSEL silinmez ama ISoftDeletable'tan gelir) ────
        builder.Property(p => p.DeletedAt)
            .HasColumnName("deleted_at")
            .HasColumnType("timestamp with time zone");
        builder.Property(p => p.DeletedById).HasColumnName("deleted_by_id");
        builder.Property(p => p.DeletedByName)
            .HasColumnName("deleted_by_name").HasMaxLength(300);
        builder.Property(p => p.DeleteReason)
            .HasColumnName("delete_reason").HasMaxLength(1000);

        builder.Ignore(p => p.DomainEvents);

        // ─── FK: NotificationAddress (opsiyonel) ────────────────────────
        builder.HasOne<Address>()
            .WithMany()
            .HasForeignKey(p => p.NotificationAddressId)
            .OnDelete(DeleteBehavior.SetNull);

        // ─── Index'ler ──────────────────────────────────────────────────
        // Sistem geneli unique — aynı TCKN/VKN ile 2 Person yaratılamaz
        builder.HasIndex(p => p.NationalId).IsUnique()
            .HasFilter("deleted_at IS NULL");
        builder.HasIndex(p => p.MobilePhone);
        builder.HasIndex(p => p.Email);
        builder.HasIndex(p => p.IsActive);
        builder.HasIndex(p => p.SearchText);
        builder.HasIndex(p => p.DeletedAt);
    }
}
