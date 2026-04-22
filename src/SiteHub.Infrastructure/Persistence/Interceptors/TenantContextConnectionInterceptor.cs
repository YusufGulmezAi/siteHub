using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using SiteHub.Application.Abstractions.Tenancy;

namespace SiteHub.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Her <see cref="DbConnection"/> açıldığında PostgreSQL session variable'larını set eder
/// (ADR-0014 §3.c). Bu değişkenler RLS policy'lerinde <c>current_setting(...)</c> ile okunur.
///
/// <para><b>Neden gerekli:</b> PostgreSQL connection pool'dan gelen bir bağlantı önceki
/// kullanıcının session variable'larını taşıyabilir. Her açılışta set EDİLMELİ. Unutulursa
/// RLS'de "başka kullanıcının verisini görme" açığı oluşur.</para>
///
/// <para><b>Fail-closed davranış:</b> TenantContext boş değerler döndürüyorsa
/// (örn. login sayfası, logout sonrası) session variable'lar boş string olarak set edilir.
/// RLS policy'leri bu durumda hiçbir kayıt döndürmez — güvenli default.</para>
///
/// <para><b>Session variable'ları:</b></para>
/// <list type="bullet">
///   <item><c>app.context_type</c>: None / System / Organization / Site / Resident</item>
///   <item><c>app.current_organization_id</c>: OrganizationId veya ''</item>
///   <item><c>app.current_site_id</c>: SiteId veya ''</item>
///   <item><c>app.resident_person_id</c>: ResidentPersonId veya ''</item>
///   <item><c>app.is_admin_impersonating</c>: 'true' / 'false'</item>
///   <item><c>app.is_system_user</c>: 'true' / 'false'</item>
/// </list>
///
/// <para><b>Lifecycle:</b> Scoped — her request/circuit için ayrı <see cref="ITenantContext"/>
/// alınır ve connection açılışında değerler set edilir.</para>
///
/// <para><b>Güvenlik notu:</b> <c>set_config(key, value, is_local)</c> çağrısında 3. parametre
/// <c>false</c> kullanılır (session-level). Connection kapanırken değerler
/// <see cref="ConnectionClosingAsync"/> ile resetlenir ki bir sonraki kullanıcı temiz başlasın.</para>
/// </summary>
public sealed class TenantContextConnectionInterceptor : DbConnectionInterceptor
{
    private readonly ITenantContext _tenant;
    private readonly ILogger<TenantContextConnectionInterceptor> _logger;

    public TenantContextConnectionInterceptor(
        ITenantContext tenant,
        ILogger<TenantContextConnectionInterceptor> logger)
    {
        _tenant = tenant;
        _logger = logger;
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await SetSessionVariablesAsync(connection, cancellationToken);
    }

    public override async ValueTask<InterceptionResult> ConnectionClosingAsync(
        DbConnection connection,
        ConnectionEventData eventData,
        InterceptionResult result)
    {
        // Connection havuza iade edilmeden önce session variable'ları resetle.
        // Böylece bir sonraki kullanıcı temiz başlar; unutulursa RLS policy
        // empty string görür ve hiçbir kayıt dönmez (fail-closed).
        try
        {
            await ResetSessionVariablesAsync(connection, CancellationToken.None);
        }
        catch (Exception ex)
        {
            // Connection zaten kapanıyor; hata olursa sadece logla, akışı etkileme
            _logger.LogWarning(ex,
                "TenantContextConnectionInterceptor: session variable resetlenemedi (connection kapanıyor).");
        }

        return result;
    }

    private async Task SetSessionVariablesAsync(DbConnection conn, CancellationToken ct)
    {
        // ITenantContext değerlerini string'e çevir (null'ları boş string yap)
        var contextType = _tenant.ContextType.ToString();
        var orgId = _tenant.OrganizationId?.ToString() ?? "";
        var siteId = _tenant.SiteId?.ToString() ?? "";
        var residentId = _tenant.ResidentPersonId?.ToString() ?? "";
        var loginAccountId = _tenant.LoginAccountId?.ToString() ?? "";
        var isImpersonating = _tenant.IsAdminImpersonating ? "true" : "false";
        var isSystem = _tenant.IsSystemUser ? "true" : "false";

        // Tek bir SQL cümlesiyle hepsini set ediyoruz — connection round-trip minimize.
        // set_config(key, value, is_local=false) → session-level değişken.
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT
              set_config('app.context_type', @ctx_type, false),
              set_config('app.current_organization_id', @org_id, false),
              set_config('app.current_site_id', @site_id, false),
              set_config('app.resident_person_id', @resident_id, false),
              set_config('app.current_login_account_id', @login_id, false),
              set_config('app.is_admin_impersonating', @is_imp, false),
              set_config('app.is_system_user', @is_sys, false);
        ";

        AddParam(cmd, "@ctx_type", contextType);
        AddParam(cmd, "@org_id", orgId);
        AddParam(cmd, "@site_id", siteId);
        AddParam(cmd, "@resident_id", residentId);
        AddParam(cmd, "@login_id", loginAccountId);
        AddParam(cmd, "@is_imp", isImpersonating);
        AddParam(cmd, "@is_sys", isSystem);

        await cmd.ExecuteNonQueryAsync(ct);

        _logger.LogDebug(
            "Tenant context session variable set: type={Type}, org={Org}, site={Site}, login={Login}, impersonating={Imp}, system={Sys}.",
            contextType,
            orgId == "" ? "(null)" : orgId,
            siteId == "" ? "(null)" : siteId,
            loginAccountId == "" ? "(null)" : loginAccountId,
            isImpersonating,
            isSystem);
    }

    private static async Task ResetSessionVariablesAsync(DbConnection conn, CancellationToken ct)
    {
        // Tüm değişkenleri boş string'e set et → RLS policy'leri fail-closed davranır
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT
              set_config('app.context_type', '', false),
              set_config('app.current_organization_id', '', false),
              set_config('app.current_site_id', '', false),
              set_config('app.resident_person_id', '', false),
              set_config('app.current_login_account_id', '', false),
              set_config('app.is_admin_impersonating', 'false', false),
              set_config('app.is_system_user', 'false', false);
        ";
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static void AddParam(DbCommand cmd, string name, string value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value;
        cmd.Parameters.Add(p);
    }
}
