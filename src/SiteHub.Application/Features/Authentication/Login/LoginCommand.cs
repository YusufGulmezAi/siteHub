using MediatR;

namespace SiteHub.Application.Features.Authentication.Login;

/// <summary>
/// Login isteği. Input tek alan — TCKN/VKN/YKN/Email/Mobile parser ile ayrıştırılır.
/// Şifre her zaman plain text olarak gelir (HTTPS üzerinden).
///
/// <para>ClientContext: IP, UserAgent, DeviceId (cookie'den veya yeni oluşturulur).
/// Controller/Blazor component bunları HttpContext'ten toplayıp geçirir.</para>
///
/// <para>Bu Command handler'ın sonucu Session oluşturur, cookie için SessionId/DeviceId döner,
/// eski session'ları kapatır (tek oturum).</para>
/// </summary>
public sealed record LoginCommand(
    string Input,
    string Password,
    LoginClientContext ClientContext) : IRequest<LoginResult>;

public sealed record LoginClientContext(
    string IpAddress,
    string UserAgent,
    bool IsMobile,
    string? ExistingDeviceId);
