using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SiteHub.Domain.Geography;
using SiteHub.Domain.Identity;
using SiteHub.Domain.Tenancy.Organizations;
using SiteHub.Domain.Tenancy.Sites;

namespace SiteHub.Infrastructure.Persistence.Configurations;

/// <summary>
/// Site aggregate için EF Core mapping.
///
/// Şema: <c>tenancy.sites</c> — tenant hiyerarşisinin ikinci katmanı için ayrı şema.
/// FK'lar: <c>public.organizations</c>, <c>geography.provinces</c>, <c>geography.districts</c>.
///
/// Unique index'ler:
/// - <c>code</c> globally unique (silinmişler hariç)
/// - <c>(organization_id, name)</c> — aynı org içinde Site ismi tekrarlanamaz
///
/// NOT: RLS policy F.5'te ayrı migration ile eklenecek.
///
/// <b>F.6 C.3 hotfix2:</b> TaxId converter <c>NationalId.Parse</c>'dan
/// <c>CreateVknRelaxed</c>'e çevrildi. Application katmanında Relaxed ile
/// yazıyoruz (sahte VKN'ler checksum'dan geçmez) — converter Parse kullanınca
/// okurken aynı VKN'ler patlıyor (PROJECT_STATE §5.5 Öğrenim 6 genişletildi).
/// OrganizationConfiguration'da zaten Relaxed vardı, Site'ta eksikti.
/// </summary>
public sealed class SiteConfiguration : IEntityTypeConfiguration<Site>
{
    public void Configure(EntityTypeBuilder<Site> builder)
    {
        builder.ToTable("sites", schema: "tenancy");

        // ─── Strongly-typed ID ──────────────────────────────────────────
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id)
            .HasColumnName("id")
            .HasConversion(id => id.Value, v => SiteId.FromGuid(v));

        // ─── Parent Organization FK ─────────────────────────────────────
        builder.Property(s => s.OrganizationId)
            .HasColumnName("organization_id")
            .HasConversion(id => id.Value, v => OrganizationId.FromGuid(v))
            .IsRequired();

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(s => s.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict); // Organization silinince Site'lar düşmez

        // ─── Code (6 haneli Feistel) ────────────────────────────────────
        builder.Property(s => s.Code)
            .HasColumnName("code")
            .IsRequired();

        // ─── Kimlik alanları ────────────────────────────────────────────
        builder.Property(s => s.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(s => s.CommercialTitle)
            .HasColumnName("commercial_title")
            .HasMaxLength(500);

        // ─── Adres (hibrit) ─────────────────────────────────────────────
        builder.Property(s => s.Address)
            .HasColumnName("address")
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(s => s.ProvinceId)
            .HasColumnName("province_id")
            .HasConversion(id => id.Value, v => ProvinceId.FromGuid(v))
            .IsRequired();

        builder.HasOne<Province>()
            .WithMany()
            .HasForeignKey(s => s.ProvinceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(s => s.DistrictId)
            .HasColumnName("district_id")
            .HasConversion(
                id => id!.Value.Value,              // SiteId? → Guid? (null korunur)
                v => (DistrictId?)DistrictId.FromGuid(v));

        builder.HasOne<District>()
            .WithMany()
            .HasForeignKey(s => s.DistrictId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        // ─── Finans ─────────────────────────────────────────────────────
        builder.Property(s => s.Iban)
            .HasColumnName("iban")
            .HasMaxLength(26)
            .UseCollation(SiteHubDbContext.TurkishCsAs); // deterministik (equality/index için)

        // TaxId — opsiyonel VKN, OrganizationConfiguration ile aynı pattern.
        //
        // DB'den okurken CreateVknRelaxed kullanılır — checksum YOK. Sebep:
        // Application katmanında CreateVknRelaxed ile yazıyoruz (dev test
        // kolaylığı, rastgele 10 hane kabul), dolayısıyla DB'de checksum'dan
        // geçmeyen VKN'ler de bulunabilir. Parse/CreateVkn (checksum'lı)
        // kullansak okurken patlar. İlerde banka entegrasyonunda Gelir
        // İdaresi servisi açılınca bu converter de sıkılaştırılır.
        builder.Property(s => s.TaxId)
            .HasColumnName("tax_id")
            .HasMaxLength(11)
            .UseCollation(SiteHubDbContext.TurkishCsAs)
            .HasConversion(
                id => id!.Value,
                str => NationalId.CreateVknRelaxed(str));
        // Nullable — DB'de NULL allowed

        // ─── Durum ──────────────────────────────────────────────────────
        builder.Property(s => s.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        // ─── SearchText (deterministic collation) ───────────────────────
        builder.Property(s => s.SearchText)
            .HasColumnName("search_text")
            .HasMaxLength(2000)
            .UseCollation(SiteHubDbContext.TurkishCsAs)
            .IsRequired();

        // ─── Audit (IAuditable) ─────────────────────────────────────────
        builder.Property(s => s.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();
        builder.Property(s => s.CreatedById).HasColumnName("created_by_id");
        builder.Property(s => s.CreatedByName)
            .HasColumnName("created_by_name")
            .HasMaxLength(300);

        builder.Property(s => s.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone");
        builder.Property(s => s.UpdatedById).HasColumnName("updated_by_id");
        builder.Property(s => s.UpdatedByName)
            .HasColumnName("updated_by_name")
            .HasMaxLength(300);

        // ─── Soft-delete (ISoftDeletable) ───────────────────────────────
        builder.Property(s => s.DeletedAt)
            .HasColumnName("deleted_at")
            .HasColumnType("timestamp with time zone");
        builder.Property(s => s.DeletedById).HasColumnName("deleted_by_id");
        builder.Property(s => s.DeletedByName)
            .HasColumnName("deleted_by_name")
            .HasMaxLength(300);
        builder.Property(s => s.DeleteReason)
            .HasColumnName("delete_reason")
            .HasMaxLength(1000);

        builder.Ignore(s => s.DomainEvents);

        // ─── Index'ler ──────────────────────────────────────────────────
        // Code: global unique (silinmişler hariç)
        builder.HasIndex(s => s.Code).IsUnique()
            .HasFilter("deleted_at IS NULL")
            .HasDatabaseName("ix_sites_code_unique");

        // (OrganizationId, Name): aynı org içinde Site ismi unique olmalı
        builder.HasIndex(s => new { s.OrganizationId, s.Name })
            .IsUnique()
            .HasFilter("deleted_at IS NULL")
            .HasDatabaseName("ix_sites_org_name_unique");

        // Parent lookup için
        builder.HasIndex(s => s.OrganizationId)
            .HasDatabaseName("ix_sites_organization_id");

        // Aranabilirlik performansı
        builder.HasIndex(s => s.SearchText)
            .HasDatabaseName("ix_sites_search_text");

        // Aktif filtreleme ve dashboard'lar için
        builder.HasIndex(s => s.IsActive)
            .HasDatabaseName("ix_sites_is_active");
        builder.HasIndex(s => s.DeletedAt)
            .HasDatabaseName("ix_sites_deleted_at");

        // Opsiyonel kullanım: TaxId unique mi değil mi?
        // Karar: unique DEĞİL — iki farklı site aynı yönetim firmasına ait olabilir
        // veya farklı org'lar aynı VKN kullanıyor olabilir (bu edge-case).
        // Sadece arama performansı için plain index yeterli.
        builder.HasIndex(s => s.TaxId)
            .HasDatabaseName("ix_sites_tax_id");
    }
}
