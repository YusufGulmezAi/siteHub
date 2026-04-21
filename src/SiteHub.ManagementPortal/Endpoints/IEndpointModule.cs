using Microsoft.AspNetCore.Routing;

namespace SiteHub.ManagementPortal.Endpoints;

/// <summary>
/// Endpoint modülü — ilgili endpoint'leri tek route group altında toplar.
///
/// <para>Kullanım deseni: Her modül kendi dosyasında, kendi route prefix'i ve auth kuralı ile.
/// Endpoint registrationı <see cref="EndpointRegistration"/> tarafından reflection ile keşfedilir.</para>
///
/// <para>Örnek:</para>
/// <code>
/// public sealed class AuthenticationEndpoints : IEndpointModule
/// {
///     public void MapEndpoints(IEndpointRouteBuilder app)
///     {
///         var group = app.MapGroup("/auth").AllowAnonymous();
///         LoginEndpoint.MapTo(group);
///         LogoutEndpoint.MapTo(group);
///     }
/// }
/// </code>
/// </summary>
public interface IEndpointModule
{
    void MapEndpoints(IEndpointRouteBuilder app);
}
