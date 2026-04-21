using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using SiteHub.Application.Abstractions.Sessions;
using SiteHub.Domain.Identity.Sessions;
using SiteHub.Shared.Authentication;

namespace SiteHub.Infrastructure.Authentication;

/// <summary>
/// Her request için session'ı doğrulayan middleware (ADR-0011 §7.2).
///
/// <para>Akış:</para>
/// <list type="number">
///   <item>ClaimsPrincipal'dan SessionId claim'i oku (cookie'den gelir).</item>
///   <item>Yoksa middleware atla (anonim request).</item>
///   <item>Redis'ten Session çek. Yoksa → sign out + ileri git (auth middleware login'e yönlendirir).</item>
///   <item>IP match? Uymuyorsa → session sil + sign out (IpChanged).</item>
///   <item>DeviceId cookie match? Uymuyorsa → session sil + sign out (DeviceMismatch).</item>
///   <item>Session'ı <c>HttpContext.Items["Session"]</c> içine koy → downstream kullanır.</item>
/// </list>
///
/// <para>Bu middleware <c>UseAuthentication</c>'dan SONRA, <c>UseAuthorization</c>'dan ÖNCE
/// çalışmalıdır.</para>
/// </summary>
public sealed class SessionValidationMiddleware
{
    public const string SessionContextKey = "SiteHub:Session";

    private readonly RequestDelegate _next;
    private readonly ILogger<SessionValidationMiddleware> _logger;

    public SessionValidationMiddleware(
        RequestDelegate next,
        ILogger<SessionValidationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ISessionStore sessionStore)
    {
        // Auth middleware ClaimsPrincipal'i doldurmuş olmalı.
        var user = context.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        var sessionIdClaim = user.FindFirstValue(SiteHubClaims.SessionId);
        if (!SessionId.TryParse(sessionIdClaim, out var sessionId))
        {
            // Cookie var ama SessionId claim'i yok — geçersiz.
            await SignOutAsync(context, "Eksik SessionId claim'i.");
            return;
        }

        var session = await sessionStore.GetAsync(sessionId, context.RequestAborted);
        if (session is null)
        {
            // Redis'te yok → expired veya başka yerden silindi (yeni login gibi).
            _logger.LogInformation(
                "Session {SessionId} Redis'te yok, sign out ediliyor.", sessionId);
            await SignOutAsync(context, "Session bulunamadı.");
            return;
        }

        // IP karşılaştır (ADR-0011 §7.4 — zero tolerance)
        var currentIp = context.Connection.RemoteIpAddress?.ToString() ?? "";
        if (!string.Equals(session.IpAddress, currentIp, StringComparison.Ordinal))
        {
            _logger.LogWarning(
                "Session {SessionId} IP değişti: orijinal={Original}, şimdi={Current}. Kapatılıyor.",
                sessionId, session.IpAddress, currentIp);
            await sessionStore.DeleteAsync(sessionId, context.RequestAborted);
            await SignOutAsync(context, "IP değişimi.");
            return;
        }

        // DeviceId karşılaştır (ADR-0011 §7.6 — cookie çalınma savunması)
        var cookieDeviceId = context.Request.Cookies[CookieSchemes.DeviceIdCookieName];
        if (!string.Equals(session.DeviceId, cookieDeviceId, StringComparison.Ordinal))
        {
            _logger.LogWarning(
                "Session {SessionId} DeviceId uyuşmuyor. Kapatılıyor.", sessionId);
            await sessionStore.DeleteAsync(sessionId, context.RequestAborted);
            await SignOutAsync(context, "Device mismatch.");
            return;
        }

        // Session downstream için context'e yerleştir
        context.Items[SessionContextKey] = session;

        // Pending2FA ise sadece belirli path'lere izin ver
        if (session.Pending2FA && !IsAllowedPathForPending2FA(context.Request.Path))
        {
            // UI: /verify-2fa'ya y\u00f6nlendir
            // API: 403 d\u00f6n
            if (context.Request.Path.StartsWithSegments("/api") ||
                context.Request.Headers["Accept"].ToString().Contains("application/json"))
            {
                context.Response.StatusCode = 403;
                await context.Response.WriteAsync("2FA required.");
                return;
            }

            context.Response.Redirect("/verify-2fa");
            return;
        }

        await _next(context);
    }

    /// <summary>
    /// Pending2FA session'da hangi path'ler izinli?
    /// - <c>/verify-2fa</c> sayfas\u0131 (UI)
    /// - <c>/auth/verify-2fa</c> endpoint (API)
    /// - <c>/auth/logout</c> (kullan\u0131c\u0131 vazge\u00e7erse)
    /// - Static assets (CSS, JS, fonts, framework scripts)
    /// - Blazor framework (SignalR hub, client-side scripts)
    /// </summary>
    private static bool IsAllowedPathForPending2FA(PathString path)
    {
        if (!path.HasValue) return false;
        var p = path.Value!;

        return p.StartsWith("/verify-2fa", StringComparison.OrdinalIgnoreCase)
            || p.StartsWith("/auth/verify-2fa", StringComparison.OrdinalIgnoreCase)
            || p.StartsWith("/auth/logout", StringComparison.OrdinalIgnoreCase)
            || p.StartsWith("/_blazor", StringComparison.OrdinalIgnoreCase)
            || p.StartsWith("/_framework", StringComparison.OrdinalIgnoreCase)
            || p.StartsWith("/_content", StringComparison.OrdinalIgnoreCase)
            || p.EndsWith(".css", StringComparison.OrdinalIgnoreCase)
            || p.EndsWith(".js", StringComparison.OrdinalIgnoreCase)
            || p.EndsWith(".ico", StringComparison.OrdinalIgnoreCase)
            || p.EndsWith(".woff", StringComparison.OrdinalIgnoreCase)
            || p.EndsWith(".woff2", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task SignOutAsync(HttpContext context, string reason)
    {
        // Auth cookie'yi sil — hangi scheme olduğu fark etmez, ikisini de dener
        try
        {
            await context.SignOutAsync(CookieSchemes.Management);
        }
        catch { /* Scheme kayıtlı değilse atla */ }

        try
        {
            await context.SignOutAsync(CookieSchemes.Resident);
        }
        catch { /* Scheme kayıtlı değilse atla */ }

        context.Response.Cookies.Delete(CookieSchemes.DeviceIdCookieName);

        // API ise 401, UI ise login redirect (auth middleware halleder)
        if (context.Request.Path.StartsWithSegments("/api") ||
            context.Request.Headers["Accept"].ToString().Contains("application/json"))
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsync($"Session invalidated: {reason}");
        }
        else
        {
            context.Response.Redirect("/login");
        }
    }
}
