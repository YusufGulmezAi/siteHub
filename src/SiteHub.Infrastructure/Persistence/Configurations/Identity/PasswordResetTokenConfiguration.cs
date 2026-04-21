using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SiteHub.Domain.Identity;

namespace SiteHub.Infrastructure.Persistence.Configurations.Identity;

public sealed class PasswordResetTokenConfiguration : IEntityTypeConfiguration<PasswordResetToken>
{
    public void Configure(EntityTypeBuilder<PasswordResetToken> builder)
    {
        builder.ToTable("password_reset_tokens", schema: "identity");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id)
            .HasColumnName("id")
            .HasConversion(id => id.Value, v => PasswordResetTokenId.FromGuid(v));

        builder.Property(t => t.LoginAccountId)
            .HasColumnName("login_account_id")
            .HasConversion(id => id.Value, v => LoginAccountId.FromGuid(v))
            .IsRequired();

        builder.Property(t => t.TokenHash)
            .HasColumnName("token_hash")
            .HasMaxLength(128)  // SHA-256 hex = 64, SHA-512 = 128, Base64 farklı — generous
            .UseCollation(SiteHubDbContext.TurkishCsAs)
            .IsRequired();

        builder.Property(t => t.Channel)
            .HasColumnName("channel")
            .HasConversion<short>()
            .IsRequired();

        builder.Property(t => t.ExpiresAt)
            .HasColumnName("expires_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(t => t.UsedAt)
            .HasColumnName("used_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(t => t.UsedFromIp)
            .HasColumnName("used_from_ip")
            .HasMaxLength(50);

        builder.Property(t => t.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(t => t.RequestedFromIp)
            .HasColumnName("requested_from_ip")
            .HasMaxLength(50);

        // Indexes
        // 1. LoginAccountId — "bu hesap için açık token var mı" sorgusu
        builder.HasIndex(t => t.LoginAccountId)
            .HasDatabaseName("ix_password_reset_tokens_login_account");

        // 2. TokenHash — verify sırasında hash ile arama (unique değil — collision imkansıza yakın
        //    ama tek kullanımlık kural zaten UsedAt ile korunuyor)
        builder.HasIndex(t => t.TokenHash)
            .HasDatabaseName("ix_password_reset_tokens_token_hash");

        // 3. ExpiresAt — cleanup job'ı için (gece eski token'ları sil)
        builder.HasIndex(t => t.ExpiresAt)
            .HasDatabaseName("ix_password_reset_tokens_expires_at");

        // FK: LoginAccount (cascade değil — account silinirse token da atıl kalabilir)
        builder.HasOne<LoginAccount>()
            .WithMany()
            .HasForeignKey(t => t.LoginAccountId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
