using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using SiteHub.Shared.Authentication;

namespace SiteHub.Infrastructure.Authentication;

/// <summary>
/// ASP.NET Core cookie authentication kurulumu (ADR-0011 §7).
///
/// <para>Iki scheme tanımlanır (MgmtAuth, ResidentAuth). Her scheme kendi
/// cookie adını kullanır → aynı browser'da iki portal ayrı oturum açabilir.</para>
///
/// <para>COOKIE AYARLARI:</para>
/// <list type="bullet">
///   <item><c>HttpOnly = true</c> → JavaScript okuyamaz (XSS savunması)</item>
///   <item><c>Secure = Always</c> → sadece HTTPS</item>
///   <item><c>SameSite = Strict</c> → CSRF savunması</item>
///   <item><c>SlidingExpiration = true</c> → aktivite varsa TTL uzar</item>
///   <item><c>ExpireTimeSpan = 15 dk</c> → Redis session TTL ile aynı</item>
/// </list>
///
/// <para>NOT: Session'ın asıl doğrulaması <c>SessionValidationMiddleware</c>'de yapılır.
/// Cookie sadece SessionId taşıyıcısıdır — gerçek state Redis'te.</para>
/// </summary>
public static class CookieAuthenticationExtensions
{
    public static IServiceCollection AddSiteHubCookieAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(CookieSchemes.Management)
            .AddCookie(CookieSchemes.Management, options =>
            {
                ConfigureBaseCookie(options, CookieSchemes.ManagementCookieName);
                options.LoginPath = "/login";
                options.AccessDeniedPath = "/access-denied";
                options.LogoutPath = "/logout";
            })
            .AddCookie(CookieSchemes.Resident, options =>
            {
                ConfigureBaseCookie(options, CookieSchemes.ResidentCookieName);
                options.LoginPath = "/login";
                options.AccessDeniedPath = "/access-denied";
                options.LogoutPath = "/logout";
            });

        services.AddAuthorization();
        return services;
    }

    private static void ConfigureBaseCookie(CookieAuthenticationOptions options, string cookieName)
    {
        options.Cookie.Name = cookieName;
        options.Cookie.HttpOnly = true;

        // Dev'de HTTP localhost ile test için SameAsRequest → HTTPS'te Secure=true, HTTP'de false.
        // Prod'da HTTPS zorunlu olacağı için Always'e denk geliyor.
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;

        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.IsEssential = true;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(15);
        options.SlidingExpiration = true;

        // Ajax/Fetch için 401 Unauthorized (redirect yerine)
        options.Events = new CookieAuthenticationEvents
        {
            OnRedirectToLogin = ctx =>
            {
                if (IsApiRequest(ctx.Request))
                {
                    ctx.Response.StatusCode = 401;
                    return Task.CompletedTask;
                }
                ctx.Response.Redirect(ctx.RedirectUri);
                return Task.CompletedTask;
            },
            OnRedirectToAccessDenied = ctx =>
            {
                if (IsApiRequest(ctx.Request))
                {
                    ctx.Response.StatusCode = 403;
                    return Task.CompletedTask;
                }
                ctx.Response.Redirect(ctx.RedirectUri);
                return Task.CompletedTask;
            }
        };
    }

    private static bool IsApiRequest(HttpRequest request)
        => request.Path.StartsWithSegments("/api") ||
           request.Headers["Accept"].ToString().Contains("application/json");
}
