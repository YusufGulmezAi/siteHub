using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using SiteHub.Application.Features.Authentication.Login;
using SiteHub.Shared.Authentication;

namespace SiteHub.ManagementPortal.Endpoints.Authentication;

/// <summary>
/// <c>POST /auth/login</c> — kullanıcı girişi (ADR-0011 §3).
///
/// <para>Akış:</para>
/// <list type="number">
///   <item>Request → LoginCommand dispatch (MediatR).</item>
///   <item>Handler session yaratır + DeviceId üretir (ADR-0011 §7.6).</item>
///   <item>Başarılıysa: auth cookie + DeviceId cookie set edilir.</item>
///   <item>Cookie <c>SessionValidationMiddleware</c> tarafından her request'te kontrol edilir.</item>
/// </list>
/// </summary>
public static class LoginEndpoint
{
    public static void MapTo(IEndpointRouteBuilder group)
    {
        group.MapPost("/login", HandleAsync)
            .WithName("Login")
            .WithSummary("Kullanıcı girişi")
            .Produces<LoginResponse>(StatusCodes.Status200OK)
            .Produces<LoginErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status400BadRequest);
    }

    public sealed record LoginRequest(string Input, string Password);

    public sealed record LoginResponse(
        string SessionId,
        int ClosedOldSessionCount,
        bool RequiresTwoFactor);

    public sealed record LoginErrorResponse(string Code, string Message);

    private static async Task<IResult> HandleAsync(
        LoginRequest request,
        HttpContext http,
        IMediator mediator,
        CancellationToken ct)
    {
        // Client context
        var clientContext = new LoginClientContext(
            IpAddress: http.Connection.RemoteIpAddress?.ToString() ?? "",
            UserAgent: http.Request.Headers.UserAgent.ToString(),
            IsMobile: IsMobileUserAgent(http.Request.Headers.UserAgent.ToString()),
            ExistingDeviceId: http.Request.Cookies[CookieSchemes.DeviceIdCookieName]);

        var command = new LoginCommand(request.Input, request.Password, clientContext);
        var result = await mediator.Send(command, ct);

        if (!result.IsSuccess)
        {
            return Results.Json(
                new LoginErrorResponse(
                    Code: result.FailureCode.ToString(),
                    Message: MapErrorMessage(result.FailureCode)),
                statusCode: MapErrorStatus(result.FailureCode));
        }

        // Claims set et — SessionValidationMiddleware bu claim'leri okuyor
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, result.SessionId!.Value.ToString()),
            new(SiteHubClaims.SessionId, result.SessionId!.Value.ToString()),
            new(SiteHubClaims.DeviceId, result.DeviceId!)
        };

        var identity = new ClaimsIdentity(claims, CookieSchemes.Management);
        var principal = new ClaimsPrincipal(identity);

        await http.SignInAsync(
            CookieSchemes.Management,
            principal,
            new AuthenticationProperties { IsPersistent = false });

        // DeviceId cookie — 1 yıl ömürlü (bir sonraki login'e kadar)
        // SecurePolicy = false dev'de HTTP için; prod'da HTTPS zaten zorunlu olacak.
        http.Response.Cookies.Append(
            CookieSchemes.DeviceIdCookieName,
            result.DeviceId!,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = http.Request.IsHttps,
                SameSite = SameSiteMode.Strict,
                MaxAge = TimeSpan.FromDays(365),
                IsEssential = true
            });

        return TypedResults.Ok(new LoginResponse(
            SessionId: result.SessionId!.Value.ToString(),
            ClosedOldSessionCount: result.ClosedOldSessions.Count,
            RequiresTwoFactor: result.RequiresTwoFactor));
    }

    private static int MapErrorStatus(LoginFailureCode code) => code switch
    {
        LoginFailureCode.InvalidInputFormat => StatusCodes.Status400BadRequest,
        LoginFailureCode.OtpRequired or LoginFailureCode.TwoFactorRequired
            => StatusCodes.Status200OK,      // OTP akışı (henüz yok) — 200 devam anlamında
        _ => StatusCodes.Status401Unauthorized
    };

    private static string MapErrorMessage(LoginFailureCode code) => code switch
    {
        LoginFailureCode.InvalidInputFormat    => "Kullanıcı bilgisi formatı tanınmadı (TCKN, email, telefon veya VKN).",
        LoginFailureCode.InvalidCredentials    => "Kullanıcı bilgisi veya parola hatalı.",
        LoginFailureCode.AccountInactive       => "Hesap devre dışı bırakılmış.",
        LoginFailureCode.AccountOutOfValidity  => "Hesap şu an geçerlilik dışında.",
        LoginFailureCode.IpNotAllowed          => "Bu IP adresinden giriş izni yok.",
        LoginFailureCode.ScheduleBlocked       => "Giriş saatine uygun değil.",
        LoginFailureCode.AccountLocked         => "Hesap çok sayıda hatalı deneme nedeniyle kilitli.",
        LoginFailureCode.OtpRequired           => "SMS doğrulama kodu gerekli.",
        LoginFailureCode.TwoFactorRequired     => "İki faktörlü doğrulama gerekli.",
        _                                      => "Giriş yapılamadı."
    };

    private static bool IsMobileUserAgent(string ua)
    {
        if (string.IsNullOrEmpty(ua)) return false;
        return ua.Contains("Mobile", StringComparison.OrdinalIgnoreCase)
            || ua.Contains("Android", StringComparison.OrdinalIgnoreCase)
            || ua.Contains("iPhone", StringComparison.OrdinalIgnoreCase);
    }
}
