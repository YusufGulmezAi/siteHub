using Microsoft.AspNetCore.Routing;

namespace SiteHub.ManagementPortal.Endpoints.Authentication;

/// <summary>
/// <c>/auth</c> route grubu — login, logout, whoami (test amaçlı).
///
/// <para>Bu grup anonymous erişime açıktır — auth cookie set/clear eden endpoint'ler
/// kendi içlerinde zaten auth mantığını yönetir.</para>
/// </summary>
public sealed class AuthenticationEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth")
            .WithTags("Authentication")
            .AllowAnonymous()
            .DisableAntiforgery();  // Minimal API POST endpoint'leri — CORS/AJAX için sağ çıkış

        LoginEndpoint.MapTo(group);
        LogoutEndpoint.MapTo(group);
        WhoAmIEndpoint.MapTo(group);
    }
}
