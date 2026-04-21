using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SiteHub.Application.Abstractions.Authentication;
using SiteHub.Application.Abstractions.Persistence;
using SiteHub.Domain.Identity;

namespace SiteHub.Application.Features.Authentication.TwoFactor;

/// <summary>
/// 2FA'y\u0131 devre d\u0131\u015f\u0131 b\u0131rak\u0131r. G\u00fcvenlik i\u00e7in:
/// 1. \u015eu anki ge\u00e7erli TOTP kodu gerekir (kullan\u0131c\u0131 ger\u00e7ekten authenticator'\u0131nda).
/// 2. Parolay\u0131 da almadan disable etmek mant\u0131kl\u0131 de\u011fil \u2014 session hijack savunmas\u0131.
///
/// <para>MVP: sadece TOTP kodu isteriz. Parola do\u011frulamas\u0131 Faz sonras\u0131.</para>
/// </summary>
public sealed record Disable2FACommand(Guid LoginAccountId, string Code)
    : IRequest<Disable2FAResult>;

public sealed record Disable2FAResult(
    bool IsSuccess,
    Disable2FAFailureCode FailureCode = Disable2FAFailureCode.None)
{
    public static Disable2FAResult Success() => new(true);
    public static Disable2FAResult Failure(Disable2FAFailureCode c) => new(false, c);
}

public enum Disable2FAFailureCode
{
    None = 0,
    AccountNotFound = 1,
    NotEnabled = 2,
    InvalidCode = 3
}

public sealed class Disable2FAHandler : IRequestHandler<Disable2FACommand, Disable2FAResult>
{
    private readonly ISiteHubDbContext _db;
    private readonly ITotpService _totp;
    private readonly ILogger<Disable2FAHandler> _logger;

    public Disable2FAHandler(
        ISiteHubDbContext db,
        ITotpService totp,
        ILogger<Disable2FAHandler> logger)
    {
        _db = db;
        _totp = totp;
        _logger = logger;
    }

    public async Task<Disable2FAResult> Handle(Disable2FACommand cmd, CancellationToken ct)
    {
        var accountId = LoginAccountId.FromGuid(cmd.LoginAccountId);

        var account = await _db.LoginAccounts
            .FirstOrDefaultAsync(a => a.Id == accountId, ct);

        if (account is null)
            return Disable2FAResult.Failure(Disable2FAFailureCode.AccountNotFound);

        if (!account.TwoFactorEnabled || string.IsNullOrEmpty(account.TwoFactorSecret))
            return Disable2FAResult.Failure(Disable2FAFailureCode.NotEnabled);

        if (!_totp.VerifyCode(account.TwoFactorSecret, cmd.Code))
        {
            _logger.LogWarning("Disable2FA: kod yanl\u0131\u015f (accountId={AccountId}).", accountId);
            return Disable2FAResult.Failure(Disable2FAFailureCode.InvalidCode);
        }

        account.DisableTwoFactor();
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("2FA devre d\u0131\u015f\u0131 b\u0131rak\u0131ld\u0131: accountId={AccountId}.", accountId);

        return Disable2FAResult.Success();
    }
}
