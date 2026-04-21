using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SiteHub.Application.Abstractions.Authentication;
using SiteHub.Application.Abstractions.Persistence;
using SiteHub.Domain.Identity;
using SiteHub.Shared.Caching;

namespace SiteHub.Application.Features.Authentication.TwoFactor;

public sealed class Initiate2FASetupHandler
    : IRequestHandler<Initiate2FASetupCommand, Initiate2FASetupResult>
{
    // Pending setup TTL \u2014 kullan\u0131c\u0131 bu s\u00fcre i\u00e7inde authenticator'a ekleyip confirm'le yazmal\u0131
    private static readonly TimeSpan PendingTtl = TimeSpan.FromMinutes(5);
    private const string IssuerName = "SiteHub";

    private readonly ISiteHubDbContext _db;
    private readonly ITotpService _totp;
    private readonly ICacheStore _cache;
    private readonly ILogger<Initiate2FASetupHandler> _logger;

    public Initiate2FASetupHandler(
        ISiteHubDbContext db,
        ITotpService totp,
        ICacheStore cache,
        ILogger<Initiate2FASetupHandler> logger)
    {
        _db = db;
        _totp = totp;
        _cache = cache;
        _logger = logger;
    }

    public async Task<Initiate2FASetupResult> Handle(
        Initiate2FASetupCommand cmd, CancellationToken ct)
    {
        var accountId = LoginAccountId.FromGuid(cmd.LoginAccountId);

        var account = await _db.LoginAccounts
            .FirstOrDefaultAsync(a => a.Id == accountId, ct);

        if (account is null)
            return Initiate2FASetupResult.Failure(Initiate2FASetupFailureCode.AccountNotFound);

        if (account.TwoFactorEnabled)
            return Initiate2FASetupResult.Failure(Initiate2FASetupFailureCode.AlreadyEnabled);

        // Secret \u00fcret
        var secret = _totp.GenerateSecret();
        var otpUri = _totp.BuildOtpAuthUri(secret, account.LoginEmail, IssuerName);

        // Redis'e ge\u00e7ici kaydet \u2014 confirm flow'unda okunacak
        var cacheKey = BuildPendingKey(accountId);
        await _cache.SetAsync(cacheKey, secret, PendingTtl, ct);

        _logger.LogInformation(
            "2FA setup ba\u015flat\u0131ld\u0131: accountId={AccountId}, TTL={Ttl}.",
            accountId, PendingTtl);

        return Initiate2FASetupResult.Success(secret, otpUri);
    }

    internal static string BuildPendingKey(LoginAccountId id) =>
        $"twofactor:pending:{id.Value}";
}
