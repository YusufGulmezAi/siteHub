using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using SiteHub.Application.Features.Authentication.PasswordReset;

namespace SiteHub.ManagementPortal.Endpoints.Authentication;

/// <summary>
/// <c>POST /auth/reset-password</c> — token ile yeni şifre belirleme.
/// Başarıyla tamamlanırsa kullanıcının tüm aktif session'ları kapatılır.
/// </summary>
public static class ResetPasswordEndpoint
{
    public static void MapTo(IEndpointRouteBuilder group)
    {
        group.MapPost("/reset-password", HandleAsync)
            .WithName("ResetPassword")
            .WithSummary("Yeni şifre belirleme")
            .Produces<ResetPasswordResponse>(StatusCodes.Status200OK)
            .Produces<ResetPasswordResponse>(StatusCodes.Status400BadRequest);
    }

    public sealed record RequestBody(string Token, string NewPassword);

    public sealed record ResetPasswordResponse(
        bool Success,
        string? Code,
        string Message);

    private static async Task<IResult> HandleAsync(
        RequestBody body,
        HttpContext http,
        IMediator mediator,
        CancellationToken ct)
    {
        var command = new ResetPasswordCommand(
            Token: body.Token,
            NewPassword: body.NewPassword,
            IpAddress: http.Connection.RemoteIpAddress?.ToString() ?? "");

        var result = await mediator.Send(command, ct);

        if (result.IsSuccess)
        {
            return TypedResults.Ok(new ResetPasswordResponse(
                Success: true,
                Code: null,
                Message: "Şifreniz başarıyla değiştirildi. Lütfen yeni şifrenizle giriş yapın."));
        }

        var message = result.FailureCode switch
        {
            ResetPasswordFailureCode.InvalidToken     => "Geçersiz bağlantı.",
            ResetPasswordFailureCode.TokenNotFound    => "Bağlantı bulunamadı.",
            ResetPasswordFailureCode.TokenAlreadyUsed => "Bu bağlantı zaten kullanılmış.",
            ResetPasswordFailureCode.TokenExpired     => "Bağlantının süresi dolmuş. Lütfen yeniden şifre sıfırlama talebi oluşturun.",
            ResetPasswordFailureCode.WeakPassword     => "Şifre en az 8 karakter olmalı ve hem harf hem rakam içermeli.",
            ResetPasswordFailureCode.AccountNotFound  => "Hesap bulunamadı.",
            _                                         => "Şifre sıfırlanamadı."
        };

        return TypedResults.BadRequest(new ResetPasswordResponse(
            Success: false,
            Code: result.FailureCode.ToString(),
            Message: message));
    }
}
