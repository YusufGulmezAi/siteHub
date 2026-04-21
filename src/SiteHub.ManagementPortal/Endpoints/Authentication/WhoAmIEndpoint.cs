using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using SiteHub.Domain.Identity.Sessions;
using SiteHub.Infrastructure.Authentication;

namespace SiteHub.ManagementPortal.Endpoints.Authentication;

/// <summary>
/// <c>GET /auth/me</c> — aktif session'ın özet bilgisini döner.
///
/// <para>Kullanıcı login değilse 401 döner (<c>SessionValidationMiddleware</c> + cookie auth sayesinde).
/// Smoke test için kullanışlı — login akışının başarılı olduğunu hızlıca doğrulamak için.</para>
/// </summary>
public static class WhoAmIEndpoint
{
    public static void MapTo(IEndpointRouteBuilder group)
    {
        group.MapGet("/me", HandleAsync)
            .WithName("WhoAmI")
            .WithSummary("Aktif oturum bilgisi")
            .RequireAuthorization()
            .Produces<WhoAmIResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);
    }

    public sealed record WhoAmIResponse(
        string SessionId,
        string LoginAccountId,
        string PersonId,
        string IpAddress,
        string LoginAt,
        string LastActivityAt,
        int MembershipCount);

    private static IResult HandleAsync(HttpContext http)
    {
        if (http.Items[SessionValidationMiddleware.SessionContextKey] is not Session session)
            return TypedResults.Unauthorized();

        return TypedResults.Ok(new WhoAmIResponse(
            SessionId: session.SessionId.ToString(),
            LoginAccountId: session.LoginAccountId.ToString(),
            PersonId: session.PersonId.ToString(),
            IpAddress: session.IpAddress,
            LoginAt: session.LoginAt.ToString("o"),
            LastActivityAt: session.LastActivityAt.ToString("o"),
            MembershipCount: session.AvailableContexts.Count));
    }
}
