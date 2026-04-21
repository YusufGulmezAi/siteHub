using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SiteHub.Application.Abstractions.Authentication;
using SiteHub.Application.Abstractions.Persistence;
using SiteHub.Application.Abstractions.Sessions;
using SiteHub.Domain.Identity.Sessions;

namespace SiteHub.Application.Features.Authentication.TwoFactor;

public sealed class Verify2FAHandler : IRequestHandler<Verify2FACommand, Verify2FAResult>
{
    private readonly ISessionStore _sessionStore;
    private readonly ISiteHubDbContext _db;
    private readonly ITotpService _totp;
    private readonly I2FARateLimiter _rateLimiter;
    private readonly TimeProvider _time;
    private readonly ILogger<Verify2FAHandler> _logger;

    public Verify2FAHandler(
        ISessionStore sessionStore,
        ISiteHubDbContext db,
        ITotpService totp,
        I2FARateLimiter rateLimiter,
        TimeProvider time,
        ILogger<Verify2FAHandler> logger)
    {
        _sessionStore = sessionStore;
        _db = db;
        _totp = totp;
        _rateLimiter = rateLimiter;
        _time = time;
        _logger = logger;
    }

    public async Task<Verify2FAResult> Handle(Verify2FACommand cmd, CancellationToken ct)
    {
        var now = _time.GetUtcNow();

        // 1. Session bul
        var session = await _sessionStore.GetAsync(new SessionId(cmd.SessionId), ct);
        if (session is null)
        {
            _logger.LogWarning("Verify2FA: session bulunamadı ({SessionId}).", cmd.SessionId);
            return Verify2FAResult.Failure(Verify2FAFailureCode.SessionNotFound);
        }

        // 2. Pending2FA mı?
        if (!session.Pending2FA)
        {
            _logger.LogWarning(
                "Verify2FA: session zaten aktif ({SessionId}). Yeniden doğrulama beklenmiyordu.",
                cmd.SessionId);
            return Verify2FAResult.Failure(Verify2FAFailureCode.SessionNotPending);
        }

        // 3. Rate limit kontrolü — account bazlı
        var limiterStatus = await _rateLimiter.CheckAsync(session.LoginAccountId.Value, ct);
        if (limiterStatus.IsBlocked)
        {
            _logger.LogWarning(
                "Verify2FA: rate limit block (accountId={AccountId}, until={Until:O}).",
                session.LoginAccountId, limiterStatus.BlockedUntil);
            return Verify2FAResult.Failure(Verify2FAFailureCode.RateLimited);
        }

        // 4. LoginAccount'tan secret'ı al
        var account = await _db.LoginAccounts
            .FirstOrDefaultAsync(a => a.Id == session.LoginAccountId, ct);

        if (account is null)
            return Verify2FAResult.Failure(Verify2FAFailureCode.AccountNotFound);

        if (!account.TwoFactorEnabled || string.IsNullOrEmpty(account.TwoFactorSecret))
        {
            _logger.LogError(
                "Verify2FA: session pending ama account 2FA kapalı (accountId={AccountId}).",
                account.Id);
            return Verify2FAResult.Failure(Verify2FAFailureCode.TwoFactorNotEnabled);
        }

        // 5. TOTP kodu doğrula
        if (!_totp.VerifyCode(account.TwoFactorSecret, cmd.Code))
        {
            // Yanlış kod — rate limit sayacını artır
            var afterFail = await _rateLimiter.RecordFailedAttemptAsync(account.Id.Value, ct);

            _logger.LogWarning(
                "Verify2FA: kod geçersiz (accountId={AccountId}, attempts={Count}, blocked={Blocked}).",
                account.Id, afterFail.AttemptsSoFar, afterFail.IsBlocked);

            if (afterFail.IsBlocked)
                return Verify2FAResult.Failure(Verify2FAFailureCode.RateLimited);

            return Verify2FAResult.Failure(
                Verify2FAFailureCode.InvalidCode,
                attemptsRemaining: afterFail.AttemptsRemaining);
        }

        // 6. Başarılı — rate limit sayacını sıfırla
        await _rateLimiter.ResetAsync(account.Id.Value, ct);

        // 7. Session'ı "aktif" yap
        var updated = session.CompletePending2FA(now);
        await _sessionStore.SaveAsync(updated, ct);

        _logger.LogInformation(
            "2FA doğrulandı: accountId={AccountId}, sessionId={SessionId}.",
            account.Id, cmd.SessionId);

        return Verify2FAResult.Success();
    }
}
