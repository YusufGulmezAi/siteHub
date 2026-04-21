using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using SiteHub.Application.Abstractions.Sessions;
using SiteHub.Domain.Identity.Sessions;
using SiteHub.Shared.Authentication;

namespace SiteHub.ManagementPortal.Endpoints.Authentication;

/// <summary>
/// <c>POST /auth/logout</c> — oturum kapatma.
///
/// <para>Akış:</para>
/// <list type="number">
///   <item>ClaimsPrincipal'dan SessionId oku.</item>
///   <item>Redis'ten session'ı sil (varsa).</item>
///   <item>Auth cookie + DeviceId cookie sil.</item>
///   <item>200 OK döner.</item>
/// </list>
///
/// <para>Session Redis'te yoksa bile cookie'ler temizlenir — "idempotent logout".</para>
/// </summary>
public static class LogoutEndpoint
{
    public static void MapTo(IEndpointRouteBuilder group)
    {
        group.MapPost("/logout", HandleAsync)
            .WithName("Logout")
            .WithSummary("Oturumu kapat")
            .Produces(StatusCodes.Status200OK);
    }

    private static async Task<IResult> HandleAsync(
        HttpContext http,
        ISessionStore sessionStore,
        CancellationToken ct)
    {
        var sessionIdClaim = http.User.FindFirstValue(SiteHubClaims.SessionId);
        if (SessionId.TryParse(sessionIdClaim, out var sessionId))
        {
            await sessionStore.DeleteAsync(sessionId, ct);
        }

        await http.SignOutAsync(CookieSchemes.Management);
        http.Response.Cookies.Delete(CookieSchemes.DeviceIdCookieName);

        return TypedResults.Ok(new { message = "Oturum kapatıldı." });
    }
}
