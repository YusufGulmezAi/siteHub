using MediatR;

namespace SiteHub.Application.Features.Authentication.TwoFactor;

/// <summary>
/// 2FA do\u011frulama komutu (ADR-0011 §4).
///
/// <para>Login ba\u015far\u0131l\u0131 oldu\u011funda Pending2FA session olu\u015fturuluyor.
/// Bu handler kullan\u0131c\u0131n\u0131n girdi\u011fi 6-haneli TOTP kodu ile session'\u0131
/// "tam yetki"ye \u00e7\u0131kar\u0131r.</para>
/// </summary>
public sealed record Verify2FACommand(Guid SessionId, string Code)
    : IRequest<Verify2FAResult>;

public sealed record Verify2FAResult(
    bool IsSuccess,
    Verify2FAFailureCode FailureCode = Verify2FAFailureCode.None)
{
    public static Verify2FAResult Success() => new(true);
    public static Verify2FAResult Failure(Verify2FAFailureCode code) => new(false, code);
}

public enum Verify2FAFailureCode
{
    None = 0,
    SessionNotFound = 1,
    SessionNotPending = 2,
    AccountNotFound = 3,
    TwoFactorNotEnabled = 4,
    InvalidCode = 5
}
