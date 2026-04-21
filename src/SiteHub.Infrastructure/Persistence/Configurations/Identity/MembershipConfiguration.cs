using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SiteHub.Domain.Identity;
using SiteHub.Domain.Identity.Authorization;

namespace SiteHub.Infrastructure.Persistence.Configurations.Identity;

public sealed class MembershipConfiguration : IEntityTypeConfiguration<Membership>
{
    public void Configure(EntityTypeBuilder<Membership> builder)
    {
        builder.ToTable("memberships", schema: "identity");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id)
            .HasColumnName("id")
            .HasConversion(id => id.Value, v => MembershipId.FromGuid(v));

        builder.Property(m => m.LoginAccountId)
            .HasColumnName("login_account_id")
            .HasConversion(id => id.Value, v => LoginAccountId.FromGuid(v))
            .IsRequired();

        builder.Property(m => m.ContextType)
            .HasColumnName("context_type")
            .HasConversion<short>()
            .IsRequired();

        builder.Property(m => m.ContextId)
            .HasColumnName("context_id");

        builder.Property(m => m.RoleId)
            .HasColumnName("role_id")
            .HasConversion(id => id.Value, v => RoleId.FromGuid(v))
            .IsRequired();

        builder.Property(m => m.ValidFrom)
            .HasColumnName("valid_from")
            .HasColumnType("timestamp with time zone");

        builder.Property(m => m.ValidTo)
            .HasColumnName("valid_to")
            .HasColumnType("timestamp with time zone");

        builder.Property(m => m.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        // Audit + soft-delete
        builder.Property(m => m.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(m => m.CreatedById).HasColumnName("created_by_id");
        builder.Property(m => m.CreatedByName).HasColumnName("created_by_name").HasMaxLength(300);
        builder.Property(m => m.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone");
        builder.Property(m => m.UpdatedById).HasColumnName("updated_by_id");
        builder.Property(m => m.UpdatedByName).HasColumnName("updated_by_name").HasMaxLength(300);
        builder.Property(m => m.DeletedAt)
            .HasColumnName("deleted_at")
            .HasColumnType("timestamp with time zone");
        builder.Property(m => m.DeletedById).HasColumnName("deleted_by_id");
        builder.Property(m => m.DeletedByName).HasColumnName("deleted_by_name").HasMaxLength(300);
        builder.Property(m => m.DeleteReason).HasColumnName("delete_reason").HasMaxLength(1000);

        builder.Ignore(m => m.DomainEvents);

        // FK: Membership → LoginAccount (Restrict — LoginAccount silinirse hata)
        builder.HasOne<LoginAccount>()
            .WithMany()
            .HasForeignKey(m => m.LoginAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        // FK: Membership → Role
        builder.HasOne<Role>()
            .WithMany()
            .HasForeignKey(m => m.RoleId)
            .OnDelete(DeleteBehavior.Restrict);

        // Index'ler — permission hesaplama sorgularının temeli
        builder.HasIndex(m => m.LoginAccountId);
        builder.HasIndex(m => new { m.ContextType, m.ContextId });
        builder.HasIndex(m => new { m.LoginAccountId, m.IsActive });
        builder.HasIndex(m => m.RoleId);
        builder.HasIndex(m => m.DeletedAt);
    }
}
