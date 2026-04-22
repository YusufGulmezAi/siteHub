using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SiteHub.Infrastructure.Persistence.Migrations;

/// <summary>
/// identity.roles tablosuna PostgreSQL Row-Level Security uygular (ADR-0002-v2 + ADR-0014).
///
/// <para><b>Koşullar (OR ile):</b></para>
/// <list type="number">
///   <item>Bootstrap/impersonation modu (<c>app.is_admin_impersonating='true'</c>) — seeder'lar
///         ve sistem admin destek modu için bypass.</item>
///   <item>Sistem rolleri (<c>is_system=true</c>) — tüm organization'lar kullanabilir.</item>
///   <item>Organization'ın custom rolleri (<c>organization_id = current_organization_id</c>).</item>
///   <item>ServiceOrganization'ın custom rolleri (ileride kullanılacak).</item>
/// </list>
///
/// <para><b>FORCE RLS:</b> Tablo sahibi (sitehub user) bile RLS'ye tabi olur. Debug için
/// bypass gerekirse superuser ile bağlan veya impersonation modunu kullan.</para>
///
/// <para><c>current_setting(..., true)</c> → değişken set edilmemişse NULL döner.
/// NULL karşılaştırmaları false olur → fail-closed davranış.</para>
/// </summary>
public partial class AddRlsToIdentityRoles : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            -- RLS aktive et + tablo sahibine de uygula (FORCE)
            ALTER TABLE identity.roles ENABLE ROW LEVEL SECURITY;
            ALTER TABLE identity.roles FORCE ROW LEVEL SECURITY;

            -- Policy: 4 koşuldan biri karşılanırsa kayıt görünür
            CREATE POLICY roles_tenant_isolation ON identity.roles
                FOR ALL
                USING (
                    -- 1) Bootstrap/impersonation modu
                    current_setting('app.is_admin_impersonating', true) = 'true'

                    -- 2) Sistem rolleri — herkese açık
                    OR is_system = true

                    -- 3) Kendi organization'ının custom rolleri
                    OR (organization_id IS NOT NULL
                        AND organization_id::text = current_setting('app.current_organization_id', true)
                        AND current_setting('app.current_organization_id', true) != '')

                    -- 4) Kendi service organization'ının custom rolleri
                    -- (ServiceOrganization context henüz yok, ileride eklenir)
                    OR (service_organization_id IS NOT NULL
                        AND service_organization_id::text = current_setting('app.current_site_id', true)
                        AND current_setting('app.current_site_id', true) != '')
                );
        ");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            DROP POLICY IF EXISTS roles_tenant_isolation ON identity.roles;
            ALTER TABLE identity.roles DISABLE ROW LEVEL SECURITY;
            -- NOT: NO FORCE ayrı bir komut değil, DISABLE zaten force'u da kaldırır
        ");
    }
}
