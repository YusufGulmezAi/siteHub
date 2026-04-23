using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using SiteHub.Application.Abstractions.Authorization;
using SiteHub.Application.Abstractions.Sessions;
using SiteHub.Domain.Identity.Authorization;
using SiteHub.Domain.Identity.Sessions;
using SiteHub.Infrastructure.Authentication;
using SiteHub.Shared.Authentication;

namespace SiteHub.ManagementPortal.Services.Authorization;

/// <summary>
/// <see cref="ICurrentUserPermissionService"/> implementasyonu (F.6 C.2).
///
/// <para><b>DI scope:</b> Scoped. Blazor Server'da bu = Circuit scope. İlk sorguda
/// session Redis'ten çekilir (veya <c>HttpContext.Items</c>'ten), sonraki sorgular
/// circuit-local cache'den cevaplanır. Böylece bir sayfa render'ında 20 yerden
/// <c>HasAsync</c> çağrılsa bile Redis'e 1 kez gidilir.</para>
///
/// <para><b>Kaynak öncelik sırası:</b></para>
/// <list type="number">
///   <item><c>HttpContext.Items[SessionContextKey]</c> — <see cref="SessionValidationMiddleware"/>
///     HTTP request'te zaten session'ı buraya koyuyor. Çoğu durumda buradan okuyoruz (sıfır DB/Redis call).</item>
///   <item>Cookie'deki SessionId claim'i → <see cref="ISessionStore"/> (Redis).
///     SignalR circuit'te HttpContext.Items kaybolmuş olabilir; fallback bu.</item>
/// </list>
///
/// <para><b>Blazor Server notu:</b> SignalR event'leri arasında scoped service
/// aynı kalır (bir kullanıcı = bir circuit = bir scope). <see cref="IHttpContextAccessor"/>
/// SignalR event'lerinde de initial request'ten doldurulan HttpContext'e erişim veriyor
/// ancak bu güvenilir değildir; session'ı bir kez çekip cache'lemek en sağlam yol.</para>
/// </summary>
public sealed class CurrentUserPermissionService : ICurrentUserPermissionService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ISessionStore _sessionStore;

    // Circuit/request scope cache — ilk okumada doldurulur, sonraki çağrılarda kullanılır.
    private Session? _cached;
    private bool _attempted;

    public CurrentUserPermissionService(
        IHttpContextAccessor httpContextAccessor,
        ISessionStore sessionStore)
    {
        _httpContextAccessor = httpContextAccessor;
        _sessionStore = sessionStore;
    }

    public async Task<bool> HasAsync(
        string permission,
        MembershipContextType? contextType = null,
        Guid? contextId = null)
    {
        if (string.IsNullOrWhiteSpace(permission)) return false;

        var session = await EnsureSessionLoadedAsync();
        if (session?.Permissions is null) return false;

        return session.Permissions.Has(permission, contextType, contextId);
    }

    public async Task<PermissionSet?> GetPermissionSetAsync()
    {
        var session = await EnsureSessionLoadedAsync();
        return session?.Permissions;
    }

    public async Task<bool> IsAuthenticatedAsync()
    {
        var session = await EnsureSessionLoadedAsync();
        return session is not null;
    }

    private async Task<Session?> EnsureSessionLoadedAsync()
    {
        if (_attempted) return _cached;
        _attempted = true;

        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is null)
            return _cached = null;

        // 1. HttpContext.Items — SessionValidationMiddleware buraya koymuş olmalı
        if (httpContext.Items.TryGetValue(
                SessionValidationMiddleware.SessionContextKey, out var sessionObj) &&
            sessionObj is Session fromItems)
        {
            return _cached = fromItems;
        }

        // 2. Fallback — ClaimsPrincipal'dan SessionId oku, Redis'ten çek
        //    (Blazor Server circuit'te Items kaybolmuşsa)
        var user = httpContext.User;
        if (user?.Identity?.IsAuthenticated != true)
            return _cached = null;

        var sessionIdClaim = user.FindFirstValue(SiteHubClaims.SessionId);
        if (!SessionId.TryParse(sessionIdClaim, out var sessionId))
            return _cached = null;

        _cached = await _sessionStore.GetAsync(sessionId);
        return _cached;
    }
}
