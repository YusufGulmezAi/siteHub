using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using SiteHub.Application.Abstractions.Context;
using SiteHub.Application.Features.Authentication.TwoFactor;

namespace SiteHub.ManagementPortal.Endpoints.Authentication;

public static class TwoFactorSetupEndpoints
{
    public static void MapTo(IEndpointRouteBuilder group)
    {
        group.MapPost("/setup-2fa/initiate", InitiateAsync)
            .WithName("Initiate2FASetup");

        group.MapPost("/setup-2fa/confirm", ConfirmAsync)
            .WithName("Confirm2FASetup");

        group.MapPost("/setup-2fa/disable", DisableAsync)
            .WithName("Disable2FA");
    }

    public sealed record InitiateResponse(
        bool Success,
        string? Secret,
        string? OtpAuthUri,
        string? Code,
        string? Message);

    public sealed record ConfirmRequestBody(string Code);
    public sealed record ConfirmResponse(bool Success, string? Code, string Message);

    public sealed record DisableRequestBody(string Code);
    public sealed record DisableResponse(bool Success, string? Code, string Message);

    private static async Task<IResult> InitiateAsync(
        HttpContext http,
        IMediator mediator,
        ICurrentUser currentUser,
        CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated || currentUser.LoginAccountId is null)
        {
            return TypedResults.Unauthorized();
        }

        var result = await mediator.Send(
            new Initiate2FASetupCommand(currentUser.LoginAccountId.Value),
            ct);

        if (result.IsSuccess)
        {
            return TypedResults.Ok(new InitiateResponse(
                Success: true,
                Secret: result.Secret,
                OtpAuthUri: result.OtpAuthUri,
                Code: null,
                Message: null));
        }

        var msg = result.FailureCode switch
        {
            Initiate2FASetupFailureCode.AccountNotFound => "Hesap bulunamad\u0131.",
            Initiate2FASetupFailureCode.AlreadyEnabled  => "2FA zaten etkin.",
            _                                            => "2FA kurulumu ba\u015flat\u0131lamad\u0131."
        };

        return TypedResults.BadRequest(new InitiateResponse(
            Success: false,
            Secret: null,
            OtpAuthUri: null,
            Code: result.FailureCode.ToString(),
            Message: msg));
    }

    private static async Task<IResult> ConfirmAsync(
        ConfirmRequestBody body,
        IMediator mediator,
        ICurrentUser currentUser,
        CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated || currentUser.LoginAccountId is null)
            return TypedResults.Unauthorized();

        var result = await mediator.Send(
            new Confirm2FASetupCommand(currentUser.LoginAccountId.Value, body.Code),
            ct);

        if (result.IsSuccess)
        {
            return TypedResults.Ok(new ConfirmResponse(
                true, null, "2FA ba\u015far\u0131yla etkinle\u015ftirildi."));
        }

        var msg = result.FailureCode switch
        {
            Confirm2FASetupFailureCode.AccountNotFound => "Hesap bulunamad\u0131.",
            Confirm2FASetupFailureCode.AlreadyEnabled  => "2FA zaten etkin.",
            Confirm2FASetupFailureCode.NoPendingSetup  => "Kurulum s\u00fcresi doldu. L\u00fctfen yeniden ba\u015flat\u0131n.",
            Confirm2FASetupFailureCode.InvalidCode     => "Kod hatal\u0131. Authenticator'dan yeni bir kod al\u0131n.",
            _                                           => "Onaylanamad\u0131."
        };

        return TypedResults.BadRequest(new ConfirmResponse(
            false, result.FailureCode.ToString(), msg));
    }

    private static async Task<IResult> DisableAsync(
        DisableRequestBody body,
        IMediator mediator,
        ICurrentUser currentUser,
        CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated || currentUser.LoginAccountId is null)
            return TypedResults.Unauthorized();

        var result = await mediator.Send(
            new Disable2FACommand(currentUser.LoginAccountId.Value, body.Code),
            ct);

        if (result.IsSuccess)
        {
            return TypedResults.Ok(new DisableResponse(
                true, null, "2FA devre d\u0131\u015f\u0131 b\u0131rak\u0131ld\u0131."));
        }

        var msg = result.FailureCode switch
        {
            Disable2FAFailureCode.AccountNotFound => "Hesap bulunamad\u0131.",
            Disable2FAFailureCode.NotEnabled      => "2FA zaten kapal\u0131.",
            Disable2FAFailureCode.InvalidCode     => "Kod hatal\u0131.",
            _                                      => "Kapat\u0131lamad\u0131."
        };

        return TypedResults.BadRequest(new DisableResponse(
            false, result.FailureCode.ToString(), msg));
    }
}
