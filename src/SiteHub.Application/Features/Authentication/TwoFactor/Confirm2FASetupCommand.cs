using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SiteHub.Application.Abstractions.Authentication;
using SiteHub.Application.Abstractions.Persistence;
using SiteHub.Application.Abstractions.Sessions;
using SiteHub.Domain.Identity;
using SiteHub.Domain.Identity.Sessions;
using SiteHub.Shared.Caching;

namespace SiteHub.Application.Features.Authentication.TwoFactor;

/// <summary>
/// 2FA kurulumunu onaylar: kullan\u0131c\u0131 authenticator'dan kodu girdi,
/// pending secret ile do\u011fruluyoruz. Ba\u015far\u0131l\u0131ysa account'a kaydederiz.
///
/// <para><b>G\u00fcvenlik etkisi:</b> Hesab\u0131n aktif session'\u0131 var ama Pending2FA=false
/// durumunda \u2014 \u00e7\u00fcnk\u00fc kullan\u0131c\u0131 zaten giri\u015fliydi kurulumu yaparken.
/// Yeni kural: <b>mevcut session'\u0131 AYNEN koruruz</b> (sonraki login'de 2FA istenir).
/// Kullan\u0131c\u0131y\u0131 \u015fu anki oturumdan \u00e7\u0131karmam\u0131z gereksiz.</para>
/// </summary>
public sealed record Confirm2FASetupCommand(Guid LoginAccountId, string Code)
    : IRequest<Confirm2FASetupResult>;

public sealed record Confirm2FASetupResult(
    bool IsSuccess,
    Confirm2FASetupFailureCode FailureCode = Confirm2FASetupFailureCode.None)
{
    public static Confirm2FASetupResult Success() => new(true);
    public static Confirm2FASetupResult Failure(Confirm2FASetupFailureCode c) => new(false, c);
}

public enum Confirm2FASetupFailureCode
{
    None = 0,
    AccountNotFound = 1,
    AlreadyEnabled = 2,
    NoPendingSetup = 3,   // Initiate hi\u00e7 \u00e7a\u011fr\u0131lmad\u0131 veya TTL bitti
    InvalidCode = 4
}

public sealed class Confirm2FASetupHandler
    : IRequestHandler<Confirm2FASetupCommand, Confirm2FASetupResult>
{
    private readonly ISiteHubDbContext _db;
    private readonly ITotpService _totp;
    private readonly ICacheStore _cache;
    private readonly TimeProvider _time;
    private readonly ILogger<Confirm2FASetupHandler> _logger;

    public Confirm2FASetupHandler(
        ISiteHubDbContext db,
        ITotpService totp,
        ICacheStore cache,
        TimeProvider time,
        ILogger<Confirm2FASetupHandler> logger)
    {
        _db = db;
        _totp = totp;
        _cache = cache;
        _time = time;
        _logger = logger;
    }

    public async Task<Confirm2FASetupResult> Handle(
        Confirm2FASetupCommand cmd, CancellationToken ct)
    {
        var accountId = LoginAccountId.FromGuid(cmd.LoginAccountId);
        var now = _time.GetUtcNow();

        var account = await _db.LoginAccounts
            .FirstOrDefaultAsync(a => a.Id == accountId, ct);

        if (account is null)
            return Confirm2FASetupResult.Failure(Confirm2FASetupFailureCode.AccountNotFound);

        if (account.TwoFactorEnabled)
            return Confirm2FASetupResult.Failure(Confirm2FASetupFailureCode.AlreadyEnabled);

        // Redis'ten pending secret'\u0131 al
        var cacheKey = Initiate2FASetupHandler.BuildPendingKey(accountId);
        var secret = await _cache.GetAsync<string>(cacheKey, ct);

        if (string.IsNullOrEmpty(secret))
        {
            _logger.LogWarning(
                "Confirm2FA: pending secret yok (accountId={AccountId}). TTL dolmu\u015f olabilir.",
                accountId);
            return Confirm2FASetupResult.Failure(Confirm2FASetupFailureCode.NoPendingSetup);
        }

        // TOTP kodu do\u011frula
        if (!_totp.VerifyCode(secret, cmd.Code))
        {
            _logger.LogWarning("Confirm2FA: kod yanl\u0131\u015f (accountId={AccountId}).", accountId);
            return Confirm2FASetupResult.Failure(Confirm2FASetupFailureCode.InvalidCode);
        }

        // Enable!
        account.EnableTwoFactor(secret, now);
        await _db.SaveChangesAsync(ct);

        // Pending key temizle
        await _cache.RemoveAsync(cacheKey, ct);

        _logger.LogInformation(
            "2FA etkinle\u015ftirildi: accountId={AccountId}.", accountId);

        return Confirm2FASetupResult.Success();
    }
}
