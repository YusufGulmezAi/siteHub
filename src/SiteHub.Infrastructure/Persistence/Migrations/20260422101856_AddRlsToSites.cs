using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SiteHub.Infrastructure.Persistence.Migrations;

/// <summary>
/// <c>tenancy.sites</c> tablosuna PostgreSQL Row-Level Security uygular
/// (ADR-0002-v2 + ADR-0014 §5).
///
/// <para><b>Site tablosu tenant-SCOPED</b> (Organization tenant KÖK'ün aksine):</para>
/// <list type="bullet">
///   <item><c>organization_id</c> kolonu var — her Site bir Organization'a bağlı</item>
///   <item>Policy standart pattern kullanır: <c>organization_id = current_organization_id</c></item>
///   <item>Site context'te ek kontrol: <c>id = current_site_id</c> (kullanıcı kendi site'ını görür)</item>
/// </list>
///
/// <para><b>Koşullar (OR ile — biri karşılanırsa satır görünür):</b></para>
/// <list type="number">
///   <item>Bootstrap/impersonation modu (<c>app.is_admin_impersonating='true'</c>) — seeder'lar
///         ve sistem admin destek modu bypass.</item>
///   <item>System kullanıcı (<c>app.is_system_user='true'</c>) tüm Site'ları görür
///         (kiracı yönetim paneli, global raporlar için).</item>
///   <item>Organization context: sadece kendi Organization'ının Site'larını görür
///         (<c>organization_id = current_organization_id</c>).</item>
///   <item>Site context: sadece kendi Site'ını görür (<c>id = current_site_id</c>).
///         Not: Site context'te HttpTenantContext F.4 sayesinde parent OrganizationId de
///         resolve edilir, fakat RLS policy'de kendi satırını görmek için direkt id match
///         daha net ve cycle-risk'siz.</item>
/// </list>
///
/// <para><b>Dev DB'de mevcut veri:</b> Faz F.3 canlı testinde eklenen 2 Site (Yıldız Sitesi,
/// Güneş Apartmanı — ABC Yonetim altında). Bu migration sonrası:</para>
/// <list type="bullet">
///   <item>Sistem Admin login'de bu 2 Site görünmeli (is_system_user=true)</item>
///   <item>Session variables boş (docker exec) → count=0 (fail-closed)</item>
///   <item>Organization context ABC Yonetim → 2 Site görünür</item>
///   <item>Organization context DEF Sitem → 0 Site (onun Site'ı yok)</item>
/// </list>
/// </summary>
public partial class AddRlsToSites : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            -- RLS aktive et + tablo sahibine de uygula (FORCE)
            ALTER TABLE tenancy.sites ENABLE ROW LEVEL SECURITY;
            ALTER TABLE tenancy.sites FORCE ROW LEVEL SECURITY;

            -- Policy: 4 koşuldan biri karşılanırsa kayıt görünür
            CREATE POLICY sites_access ON tenancy.sites
                FOR ALL
                USING (
                    -- 1) Bootstrap/impersonation modu bypass
                    current_setting('app.is_admin_impersonating', true) = 'true'

                    -- 2) System kullanıcı tüm Site'ları görür
                    OR current_setting('app.is_system_user', true) = 'true'

                    -- 3) Organization context: kendi org'unun Site'ları
                    OR (current_setting('app.current_organization_id', true) != ''
                        AND organization_id::text = current_setting('app.current_organization_id', true))

                    -- 4) Site context: sadece kendi Site (direkt id match, resolver gerektirmez)
                    OR (current_setting('app.current_site_id', true) != ''
                        AND id::text = current_setting('app.current_site_id', true))
                );
        ");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            DROP POLICY IF EXISTS sites_access ON tenancy.sites;
            ALTER TABLE tenancy.sites DISABLE ROW LEVEL SECURITY;
        ");
    }
}
