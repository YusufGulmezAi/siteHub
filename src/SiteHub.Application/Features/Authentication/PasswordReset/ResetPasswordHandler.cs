using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SiteHub.Application.Abstractions.Authentication;
using SiteHub.Application.Abstractions.Persistence;
using SiteHub.Application.Abstractions.Sessions;

namespace SiteHub.Application.Features.Authentication.PasswordReset;

/// <summary>
/// Şifre sıfırlama uygulama handler'ı.
///
/// <para>Akış:</para>
/// <list type="number">
///   <item>Token hash'le.</item>
///   <item>DB'de hash ile arama — token bulunmuş, kullanılmamış, expire olmamış.</item>
///   <item>Yeni parola policy check (uzunluk, karmaşıklık).</item>
///   <item>LoginAccount hash'ini güncelle.</item>
///   <item>Token'ı kullanılmış olarak işaretle.</item>
///   <item>Kullanıcının tüm aktif session'larını kapat (ADR-0011 §7.5).</item>
/// </list>
/// </summary>
public sealed class ResetPasswordHandler
    : IRequestHandler<ResetPasswordCommand, ResetPasswordResult>
{
    private const int MinPasswordLength = 8;

    private readonly ISiteHubDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ISessionStore _sessionStore;
    private readonly TimeProvider _time;
    private readonly ILogger<ResetPasswordHandler> _logger;

    public ResetPasswordHandler(
        ISiteHubDbContext db,
        IPasswordHasher passwordHasher,
        ISessionStore sessionStore,
        TimeProvider time,
        ILogger<ResetPasswordHandler> logger)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _sessionStore = sessionStore;
        _time = time;
        _logger = logger;
    }

    public async Task<ResetPasswordResult> Handle(ResetPasswordCommand cmd, CancellationToken ct)
    {
        var now = _time.GetUtcNow();

        // 1. Validation
        if (string.IsNullOrWhiteSpace(cmd.Token) || cmd.Token.Length < 6)
            return ResetPasswordResult.Failure(ResetPasswordFailureCode.InvalidToken);

        if (!IsPasswordStrong(cmd.NewPassword))
            return ResetPasswordResult.Failure(ResetPasswordFailureCode.WeakPassword);

        // 2. Token bul
        var tokenHash = TokenHasher.Hash(cmd.Token);
        var tokenRecord = await _db.PasswordResetTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

        if (tokenRecord is null)
        {
            _logger.LogWarning("ResetPassword: token bulunamadı (IP: {Ip}).", cmd.IpAddress);
            return ResetPasswordResult.Failure(ResetPasswordFailureCode.TokenNotFound);
        }

        if (tokenRecord.UsedAt.HasValue)
        {
            _logger.LogWarning(
                "ResetPassword: token zaten kullanılmış (tokenId={TokenId}, usedAt={UsedAt}).",
                tokenRecord.Id, tokenRecord.UsedAt);
            return ResetPasswordResult.Failure(ResetPasswordFailureCode.TokenAlreadyUsed);
        }

        if (!tokenRecord.IsUsable(now))
        {
            _logger.LogWarning(
                "ResetPassword: token expire olmuş (tokenId={TokenId}, expiresAt={Expires}).",
                tokenRecord.Id, tokenRecord.ExpiresAt);
            return ResetPasswordResult.Failure(ResetPasswordFailureCode.TokenExpired);
        }

        // 3. LoginAccount bul
        var account = await _db.LoginAccounts
            .FirstOrDefaultAsync(a => a.Id == tokenRecord.LoginAccountId, ct);

        if (account is null)
        {
            _logger.LogWarning(
                "ResetPassword: LoginAccount yok (accountId={AccountId}).",
                tokenRecord.LoginAccountId);
            return ResetPasswordResult.Failure(ResetPasswordFailureCode.AccountNotFound);
        }

        // 4. Şifreyi güncelle
        var newHash = _passwordHasher.Hash(cmd.NewPassword);
        account.ChangePasswordHash(newHash);

        // 5. Token'ı kullanıldı olarak işaretle
        tokenRecord.MarkAsUsed(now, cmd.IpAddress);

        await _db.SaveChangesAsync(ct);

        // 6. Tüm aktif session'ları kapat (ADR-0011 §7.5)
        var closedSessions = await _sessionStore.DeleteByLoginAccountAsync(account.Id, ct);

        _logger.LogInformation(
            "Şifre sıfırlandı: accountId={AccountId}, kapatılan session={Count}, IP: {Ip}.",
            account.Id, closedSessions.Count, cmd.IpAddress);

        return ResetPasswordResult.Success();
    }

    /// <summary>
    /// MVP parola policy'si — minimum 8 karakter, en az 1 harf + 1 rakam.
    /// İleride <c>IPasswordPolicy</c> service ile abstract edilebilir.
    /// </summary>
    private static bool IsPasswordStrong(string password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinPasswordLength)
            return false;

        bool hasLetter = false;
        bool hasDigit = false;
        foreach (var ch in password)
        {
            if (char.IsLetter(ch)) hasLetter = true;
            else if (char.IsDigit(ch)) hasDigit = true;
            if (hasLetter && hasDigit) return true;
        }
        return false;
    }
}
