using Hangfire.Dashboard;
using Microsoft.AspNetCore.Http;

namespace SiteHub.Infrastructure.BackgroundJobs;

/// <summary>
/// Hangfire Dashboard yetkilendirme filtresi.
///
/// <para>Sadece <b>localhost</b>'tan gelen istekleri kabul eder. Başka IP'den
/// erişim denenirse Hangfire 401/403 döner.</para>
///
/// <para>Dev'de ::1 / 127.0.0.1 ile çalışır. Prod'da dış IP'lerden erişim otomatik bloke.
/// İleride System Admin permission check eklenebilir.</para>
/// </summary>
public sealed class LocalhostOnlyDashboardAuthFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        var remoteIp = httpContext.Connection.RemoteIpAddress;

        if (remoteIp is null) return false;

        // IPv4 ya da IPv6 loopback
        return System.Net.IPAddress.IsLoopback(remoteIp);
    }
}
