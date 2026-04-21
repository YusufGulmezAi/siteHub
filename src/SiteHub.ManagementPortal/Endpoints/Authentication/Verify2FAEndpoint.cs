using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using SiteHub.Application.Abstractions.Authentication;
using SiteHub.Application.Abstractions.Context;
using SiteHub.Application.Features.Authentication.TwoFactor;

namespace SiteHub.ManagementPortal.Endpoints.Authentication;

/// <summary>
/// <c>POST /auth/verify-2fa</c> — Pending2FA session'da TOTP kodu doğrulama.
///
/// <para>Bu endpoint Pending2FA session'dan erişilebilir
/// (<see cref="SessionValidationMiddleware"/> bu path'i beyaz listede tutar).</para>
/// </summary>
public static class Verify2FAEndpoint
{
    public static void MapTo(IEndpointRouteBuilder group)
    {
        group.MapPost("/verify-2fa", HandleAsync)
            .WithName("Verify2FA")
            .WithSummary("Login sonrası 2FA kodu doğrula")
            .Produces<Verify2FAResponse>(StatusCodes.Status200OK)
            .Produces<Verify2FAResponse>(StatusCodes.Status400BadRequest);
    }

    public sealed record RequestBody(string Code);
    public sealed record Verify2FAResponse(bool Success, string? Code, string Message);

    private static async Task<IResult> HandleAsync(
        RequestBody body,
        IMediator mediator,
        ICurrentUser currentUser,
        IOptions<LoginSecurityOptions> options,
        CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated || currentUser.SessionId is null)
        {
            return TypedResults.BadRequest(new Verify2FAResponse(
                false, "NoSession", "Aktif oturum bulunamadı. Lütfen yeniden giriş yapın."));
        }

        var result = await mediator.Send(
            new Verify2FACommand(currentUser.SessionId.Value, body.Code), ct);

        if (result.IsSuccess)
        {
            return TypedResults.Ok(new Verify2FAResponse(
                true, null, "Doğrulama başarılı."));
        }

        var blockMinutes = options.Value.TwoFactorBlockMinutes;

        var message = result.FailureCode switch
        {
            Verify2FAFailureCode.SessionNotFound     => "Oturum bulunamadı. Yeniden giriş yapın.",
            Verify2FAFailureCode.SessionNotPending   => "Bu oturum için 2FA beklenmiyor.",
            Verify2FAFailureCode.AccountNotFound     => "Hesap bulunamadı.",
            Verify2FAFailureCode.TwoFactorNotEnabled => "2FA etkin değil.",
            Verify2FAFailureCode.InvalidCode         => BuildInvalidCodeMessage(result.AttemptsRemaining, blockMinutes),
            Verify2FAFailureCode.RateLimited         => $"Çok sayıda yanlış deneme. Güvenlik için {blockMinutes} dakika bekleyip tekrar deneyin.",
            _                                        => "Doğrulama başarısız."
        };

        return TypedResults.BadRequest(new Verify2FAResponse(
            Success: false,
            Code: result.FailureCode.ToString(),
            Message: message));
    }

    /// <summary>
    /// Kalan deneme hakkını kullanıcıya belirten mesaj.
    /// Türkçe çoğul: "1 hakkınız", "3 hakkınız" (Türkçe'de sayı sonrası kelime tekil kalır).
    /// </summary>
    private static string BuildInvalidCodeMessage(int attemptsRemaining, int blockMinutes) => attemptsRemaining switch
    {
        <= 0 => "Kod hatalı.",
        1    => $"Kod hatalı. Son 1 deneme hakkınız kaldı, sonrasında hesap {blockMinutes} dakika kilitlenir.",
        _    => $"Kod hatalı. {attemptsRemaining} deneme hakkınız kaldı."
    };
}
