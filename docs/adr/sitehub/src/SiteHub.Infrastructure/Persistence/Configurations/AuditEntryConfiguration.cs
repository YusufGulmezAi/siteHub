using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SiteHub.Domain.Audit;

namespace SiteHub.Infrastructure.Persistence.Configurations;

public sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        // Ayrı schema — 'audit' (ortak tablolardan izole)
        builder.ToTable("entity_changes", schema: "audit");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Timestamp)
            .HasColumnName("timestamp")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(x => x.EntityType)
            .HasColumnName("entity_type")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.EntityId).HasColumnName("entity_id").IsRequired();

        builder.Property(x => x.Operation)
            .HasColumnName("operation")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.UserId).HasColumnName("user_id");
        builder.Property(x => x.UserName)
            .HasColumnName("user_name")
            .HasMaxLength(300);

        builder.Property(x => x.IpAddress)
            .HasColumnName("ip_address")
            .HasMaxLength(45); // IPv6 uzun hali
        builder.Property(x => x.UserAgent)
            .HasColumnName("user_agent")
            .HasMaxLength(500);
        builder.Property(x => x.CorrelationId)
            .HasColumnName("correlation_id")
            .HasMaxLength(100);

        builder.Property(x => x.ContextType)
            .HasColumnName("context_type")
            .HasMaxLength(50);
        builder.Property(x => x.ContextId).HasColumnName("context_id");

        // JSONB - Postgres'in yerel JSON tipi. Sorgulanabilir, indekslenebilir.
        // ÖNEMLİ: default string convention (tr_ci_ai collation) otomatik uygulanır
        // ama jsonb tipinde collation DESTEKLENMEZ. Bu yüzden explicit olarak temizliyoruz.
        var changesProperty = builder.Property(x => x.ChangesJson)
            .HasColumnName("changes")
            .HasColumnType("jsonb")
            .IsRequired();
        changesProperty.Metadata.SetCollation(null);

        // Sorgu performansı için index'ler — yaygın audit sorguları:
        builder.HasIndex(x => x.Timestamp);                              // "son değişiklikler"
        builder.HasIndex(x => new { x.EntityType, x.EntityId });         // "Firm X'in geçmişi"
        builder.HasIndex(x => x.UserId);                                 // "kullanıcı Y ne yaptı"
        builder.HasIndex(x => x.CorrelationId);                          // "bu request'teki tüm olaylar"
    }
}
