using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SiteHub.Domain.Identity;

namespace SiteHub.Infrastructure.Persistence.Configurations.Identity;

public sealed class LoginAccountConfiguration : IEntityTypeConfiguration<LoginAccount>
{
    public void Configure(EntityTypeBuilder<LoginAccount> builder)
    {
        builder.ToTable("login_accounts", schema: "identity");

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id)
            .HasColumnName("id")
            .HasConversion(id => id.Value, v => LoginAccountId.FromGuid(v));

        builder.Property(l => l.PersonId)
            .HasColumnName("person_id")
            .HasConversion(id => id.Value, v => PersonId.FromGuid(v))
            .IsRequired();

        builder.Property(l => l.LoginEmail)
            .HasColumnName("login_email")
            .HasMaxLength(320)
            .UseCollation(SiteHubDbContext.TurkishCsAs)   // deterministic — unique index için
            .IsRequired();

        builder.Property(l => l.PasswordHash)
            .HasColumnName("password_hash")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(l => l.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Property(l => l.ValidFrom)
            .HasColumnName("valid_from")
            .HasColumnType("timestamp with time zone");

        builder.Property(l => l.ValidTo)
            .HasColumnName("valid_to")
            .HasColumnType("timestamp with time zone");

        builder.Property(l => l.IpWhitelist)
            .HasColumnName("ip_whitelist")
            .HasMaxLength(2000);

        builder.Property(l => l.LoginScheduleJson)
            .HasColumnName("login_schedule_json")
            .HasColumnType("jsonb")
            .UseCollation(null!);  // JSONB collation desteklemez — convention'dan gelen tr_ci_ai'yi temizle

        builder.Property(l => l.LastLoginAt)
            .HasColumnName("last_login_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(l => l.LastLoginIp)
            .HasColumnName("last_login_ip")
            .HasMaxLength(45);   // IPv6 max 39, güvenli payda 45

        builder.Property(l => l.FailedLoginCount)
            .HasColumnName("failed_login_count")
            .IsRequired();

        builder.Property(l => l.LockoutUntil)
            .HasColumnName("lockout_until")
            .HasColumnType("timestamp with time zone");

        // Audit
        builder.Property(l => l.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();
        builder.Property(l => l.CreatedById).HasColumnName("created_by_id");
        builder.Property(l => l.CreatedByName)
            .HasColumnName("created_by_name").HasMaxLength(300);

        builder.Property(l => l.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone");
        builder.Property(l => l.UpdatedById).HasColumnName("updated_by_id");
        builder.Property(l => l.UpdatedByName)
            .HasColumnName("updated_by_name").HasMaxLength(300);

        // Soft-delete (LoginAccount soft-delete desteklenir — hesap silme KVKK için)
        builder.Property(l => l.DeletedAt)
            .HasColumnName("deleted_at")
            .HasColumnType("timestamp with time zone");
        builder.Property(l => l.DeletedById).HasColumnName("deleted_by_id");
        builder.Property(l => l.DeletedByName)
            .HasColumnName("deleted_by_name").HasMaxLength(300);
        builder.Property(l => l.DeleteReason)
            .HasColumnName("delete_reason").HasMaxLength(1000);

        builder.Ignore(l => l.DomainEvents);

        // FK: LoginAccount → Person (1:1, ama Person tarafında navigation yok)
        builder.HasOne<Person>()
            .WithMany()
            .HasForeignKey(l => l.PersonId)
            .OnDelete(DeleteBehavior.Restrict);   // Person silinemez

        // ─── Index'ler ──────────────────────────────────────────────────
        // Login email SİSTEM GENELİ unique (aktif kayıtlar arasında)
        builder.HasIndex(l => l.LoginEmail).IsUnique()
            .HasFilter("deleted_at IS NULL");

        // Bir Person en fazla 1 aktif LoginAccount'a sahip olabilir
        builder.HasIndex(l => l.PersonId).IsUnique()
            .HasFilter("deleted_at IS NULL");

        builder.HasIndex(l => l.IsActive);
        builder.HasIndex(l => l.LockoutUntil);
        builder.HasIndex(l => l.DeletedAt);
    }
}
