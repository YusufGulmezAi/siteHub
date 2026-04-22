using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SiteHub.Infrastructure.Persistence.Migrations;

/// <summary>
/// <c>public.organizations</c> tablosuna PostgreSQL Row-Level Security uygular
/// (ADR-0002-v2 + ADR-0014 §5).
///
/// <para><b>Özel Durum:</b> Organizations tablosu tenant-scoped DEĞİL, tenant <b>kökü</b>dür.
/// Yani <c>organization_id</c> kolonu yok — satırın <c>id</c>'si kendisi Organization'dır.
/// Bu yüzden policy standart pattern'den farklı: "kendi org'u" = <c>id = current_organization_id</c>.</para>
///
/// <para><b>Koşullar (OR ile):</b></para>
/// <list type="number">
///   <item>Bootstrap/impersonation modu (<c>app.is_admin_impersonating='true'</c>) — seeder'lar
///         ve sistem admin destek modu bypass.</item>
///   <item>System kullanıcı (<c>app.is_system_user='true'</c>) tüm org'ları görür
///         (kiracı yönetim paneli için gerek).</item>
///   <item>Organization context'te olan kullanıcı sadece kendi org'unu görür
///         (<c>id = current_organization_id</c>).</item>
/// </list>
///
/// <para><b>Bilinçli eksik:</b> Site/Resident/ServiceOrganization context'te
/// <c>current_organization_id</c> session variable'ı boş olduğu için organizations tablosu
/// hiç görünmez. Bu <b>kabul edilen geçici davranış</b>; A.4.b'de Site → Organization resolver
/// eklenince düzeltilecek. Şu an tek aktif Organization context Admin login'i, o da
/// <c>is_system_user=true</c> kuralı ile tümünü görür.</para>
///
/// <para><b>Login Etki Analizi:</b> Login akışı <c>organizations</c> tablosuna erişmiyor
/// (sadece <c>login_accounts</c>, <c>memberships</c>, <c>roles</c>). Dolayısıyla bu migration
/// login'i bozmaz.</para>
/// </summary>
public partial class AddRlsToOrganizations : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            -- RLS aktive et + tablo sahibine de uygula (FORCE)
            ALTER TABLE public.organizations ENABLE ROW LEVEL SECURITY;
            ALTER TABLE public.organizations FORCE ROW LEVEL SECURITY;

            -- Policy: 3 koşuldan biri karşılanırsa kayıt görünür
            CREATE POLICY organizations_access ON public.organizations
                FOR ALL
                USING (
                    -- 1) Bootstrap/impersonation modu bypass
                    current_setting('app.is_admin_impersonating', true) = 'true'

                    -- 2) System kullanıcı (System Admin) tüm organizations'ı görür
                    OR current_setting('app.is_system_user', true) = 'true'

                    -- 3) Organization context: sadece kendi org'u
                    OR (current_setting('app.current_organization_id', true) != ''
                        AND id::text = current_setting('app.current_organization_id', true))
                );
        ");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            DROP POLICY IF EXISTS organizations_access ON public.organizations;
            ALTER TABLE public.organizations DISABLE ROW LEVEL SECURITY;
        ");
    }
}
