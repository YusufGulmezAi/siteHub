using MediatR;

namespace SiteHub.Application.Features.Authentication.TwoFactor;

/// <summary>
/// 2FA kurulumu ba\u015flat\u0131r. Secret \u00fcretir, Redis'e 5 dk TTL ile kaydeder
/// ve QR olu\u015fturulmas\u0131 i\u00e7in <c>otpauth://</c> URI d\u00f6ner.
///
/// <para>DB'ye secret yaz\u0131lmaz — kullan\u0131c\u0131 confirm etmezse g\u00f6zk\u00f6zd\u00fc kaybolur.</para>
/// </summary>
public sealed record Initiate2FASetupCommand(Guid LoginAccountId)
    : IRequest<Initiate2FASetupResult>;

public sealed record Initiate2FASetupResult(
    bool IsSuccess,
    string? Secret = null,         // UI'da "manuel ekle" i\u00e7in g\u00f6sterilir
    string? OtpAuthUri = null,     // QR \u00fcretmek i\u00e7in
    Initiate2FASetupFailureCode FailureCode = Initiate2FASetupFailureCode.None)
{
    public static Initiate2FASetupResult Success(string secret, string otpAuthUri) =>
        new(true, secret, otpAuthUri);

    public static Initiate2FASetupResult Failure(Initiate2FASetupFailureCode code) =>
        new(false, FailureCode: code);
}

public enum Initiate2FASetupFailureCode
{
    None = 0,
    AccountNotFound = 1,
    AlreadyEnabled = 2
}
