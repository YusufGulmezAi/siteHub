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
    private readonly TimeProvider _time;
    private readonly ILogger<Verify2FAHandler> _logger;

    public Verify2FAHandler(
        ISessionStore sessionStore,
        ISiteHubDbContext db,
        ITotpService totp,
        TimeProvider time,
        ILogger<Verify2FAHandler> logger)
    {
        _sessionStore = sessionStore;
        _db = db;
        _totp = totp;
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
            _logger.LogWarning("Verify2FA: session bulunamad\u0131 ({SessionId}).", cmd.SessionId);
            return Verify2FAResult.Failure(Verify2FAFailureCode.SessionNotFound);
        }

        // 2. Pending2FA m\u0131?
        if (!session.Pending2FA)
        {
            _logger.LogWarning(
                "Verify2FA: session zaten aktif ({SessionId}). Yeniden do\u011frulama beklenmiyordu.",
                cmd.SessionId);
            return Verify2FAResult.Failure(Verify2FAFailureCode.SessionNotPending);
        }

        // 3. LoginAccount'tan secret'\u0131 al
        var account = await _db.LoginAccounts
            .FirstOrDefaultAsync(a => a.Id == session.LoginAccountId, ct);

        if (account is null)
            return Verify2FAResult.Failure(Verify2FAFailureCode.AccountNotFound);

        if (!account.TwoFactorEnabled || string.IsNullOrEmpty(account.TwoFactorSecret))
        {
            _logger.LogError(
                "Verify2FA: session pending ama account 2FA kapal\u0131 (accountId={AccountId}).",
                account.Id);
            return Verify2FAResult.Failure(Verify2FAFailureCode.TwoFactorNotEnabled);
        }

        // 4. TOTP kodu do\u011frula
        if (!_totp.VerifyCode(account.TwoFactorSecret, cmd.Code))
        {
            _logger.LogWarning(
                "Verify2FA: kod ge\u00e7ersiz (accountId={AccountId}, sessionId={SessionId}).",
                account.Id, cmd.SessionId);
            return Verify2FAResult.Failure(Verify2FAFailureCode.InvalidCode);
        }

        // 5. Session'\u0131 "aktif" yap
        var updated = session.CompletePending2FA(now);
        await _sessionStore.SaveAsync(updated, ct);

        _logger.LogInformation(
            "2FA do\u011fruland\u0131: accountId={AccountId}, sessionId={SessionId}.",
            account.Id, cmd.SessionId);

        return Verify2FAResult.Success();
    }
}
