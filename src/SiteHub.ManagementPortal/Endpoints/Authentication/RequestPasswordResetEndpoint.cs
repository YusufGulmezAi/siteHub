using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using SiteHub.Application.Features.Authentication.PasswordReset;

namespace SiteHub.ManagementPortal.Endpoints.Authentication;

/// <summary>
/// <c>POST /auth/request-password-reset</c> — şifre sıfırlama talebi (ADR-0011 §5).
///
/// <para>Güvenlik: Hem bulunmuş hem bulunamamış durumda aynı sonuç döner
/// (user enumeration defense). Backend log'a yazar.</para>
/// </summary>
public static class RequestPasswordResetEndpoint
{
    public static void MapTo(IEndpointRouteBuilder group)
    {
        group.MapPost("/request-password-reset", HandleAsync)
            .WithName("RequestPasswordReset")
            .WithSummary("Şifre sıfırlama talebi")
            .Produces<RequestPasswordResetResponse>(StatusCodes.Status200OK);
    }

    public sealed record RequestBody(string Input, string Channel);

    public sealed record RequestPasswordResetResponse(
        bool Success,
        string Message);

    private static async Task<IResult> HandleAsync(
        RequestBody body,
        HttpContext http,
        IMediator mediator,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.Input) || string.IsNullOrWhiteSpace(body.Channel))
        {
            return TypedResults.BadRequest(new RequestPasswordResetResponse(
                Success: false,
                Message: "Kullanıcı bilgisi ve kanal zorunludur."));
        }

        if (!Enum.TryParse<ResetChannelChoice>(body.Channel, ignoreCase: true, out var channel))
        {
            return TypedResults.BadRequest(new RequestPasswordResetResponse(
                Success: false,
                Message: "Kanal geçersiz. 'Email' veya 'Sms' olmalı."));
        }

        var command = new RequestPasswordResetCommand(
            Input: body.Input,
            Channel: channel,
            IpAddress: http.Connection.RemoteIpAddress?.ToString() ?? "");

        await mediator.Send(command, ct);

        // Her zaman aynı cevap — enumeration defense
        return TypedResults.Ok(new RequestPasswordResetResponse(
            Success: true,
            Message: "Eğer kayıtlı bir hesap varsa, seçtiğiniz kanaldan talimatlar gönderildi."));
    }
}
