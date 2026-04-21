using System.Security.Cryptography;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SiteHub.Application.Abstractions.Authentication;
using SiteHub.Application.Abstractions.Persistence;
using SiteHub.Application.Abstractions.Sessions;
using SiteHub.Domain.Identity;
using SiteHub.Domain.Identity.Authorization;
using SiteHub.Domain.Identity.Sessions;

namespace SiteHub.Application.Features.Authentication.Login;

/// <summary>
/// Login use case handler (ADR-0011 §3).
///
/// <para>Akış (sırayla):</para>
/// <list type="number">
///   <item>Input tipini tespit (TCKN/VKN/YKN/Email/Mobile). Mobile → OTP gerekir (MVP'de yok).</item>
///   <item>Person bul (tipe göre NationalId / Email / MobilePhone).</item>
///   <item>LoginAccount bul (Person 1:1 LoginAccount).</item>
///   <item>Lockout check — kilitliyse parolayı bile denemeyiz.</item>
///   <item>Parola doğrula; yanlışsa FailedLoginCount artar, eşikte auto-lockout.</item>
///   <item>IsActive + ValidFrom/To + IpWhitelist + LoginSchedule kontrolleri (ayrı hata kodları).</item>
///   <item>Rehash gerekirse otomatik rehash.</item>
///   <item>Memberships topla (session snapshot için).</item>
///   <item>Eski session'ları kapat (tek oturum kuralı).</item>
///   <item>Yeni DeviceId + Session oluştur, Redis'e yaz.</item>
/// </list>
///
/// <para>GÜVENLİK:</para>
/// <list type="bullet">
///   <item>Person bulunamadı ⇒ sahte hash verify çalıştırılır (timing attack savunması)
///   + <c>InvalidCredentials</c> döner (enumeration attack savunması).</item>
///   <item>Lockout eşiği: 5 yanlış deneme → 15 dk kilit.</item>
/// </list>
/// </summary>
public sealed class LoginHandler : IRequestHandler<LoginCommand, LoginResult>
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    // Timing attack savunması için sahte hash (format v3, rastgele değer).
    // Person bulunamazsa bile sabit süre harcanması amacıyla verify çağırılır.
    private const string FakeHashForTimingDefense =
        "AQAAAAIAAYagAAAAELsRqRPEmJQWjsWxaHYPx3Cp3tRLb7DfEL0mYzuErv8Rj4tPqE1qJj1B2R3Lk==";

    private readonly ISiteHubDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ISessionStore _sessionStore;
    private readonly TimeProvider _time;
    private readonly ILogger<LoginHandler> _logger;

    public LoginHandler(
        ISiteHubDbContext db,
        IPasswordHasher passwordHasher,
        ISessionStore sessionStore,
        TimeProvider time,
        ILogger<LoginHandler> logger)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _sessionStore = sessionStore;
        _time = time;
        _logger = logger;
    }

    public async Task<LoginResult> Handle(LoginCommand command, CancellationToken ct)
    {
        var now = _time.GetUtcNow();

        // 1. Input tipi
        var inputType = LoginInputParser.Detect(command.Input);
        if (inputType == LoginInputType.Unknown)
        {
            _logger.LogWarning("Login başarısız: input formatı tanınmadı (IP: {Ip}).",
                command.ClientContext.IpAddress);
            return LoginResult.Failure(LoginFailureCode.InvalidInputFormat);
        }

        if (inputType == LoginInputType.Mobile)
        {
            _logger.LogInformation("Mobile login denemesi — OTP akışı MVP'de yok.");
            return LoginResult.Failure(LoginFailureCode.OtpRequired);
        }

        var normalizedInput = LoginInputParser.Normalize(command.Input, inputType);

        // 2. Person bul
        var person = await FindPersonAsync(normalizedInput, inputType, ct);
        if (person is null)
        {
            _passwordHasher.Verify(FakeHashForTimingDefense, command.Password);
            _logger.LogWarning("Login başarısız: Person yok (type={Type}, IP: {Ip}).",
                inputType, command.ClientContext.IpAddress);
            return LoginResult.Failure(LoginFailureCode.InvalidCredentials);
        }

        // 3. LoginAccount bul
        var account = await _db.LoginAccounts
            .FirstOrDefaultAsync(a => a.PersonId == person.Id, ct);

        if (account is null)
        {
            _passwordHasher.Verify(FakeHashForTimingDefense, command.Password);
            _logger.LogWarning("Person {PersonId} için LoginAccount yok.", person.Id);
            return LoginResult.Failure(LoginFailureCode.InvalidCredentials);
        }

        // 4. Lockout check
        if (account.LockoutUntil.HasValue && now < account.LockoutUntil.Value)
        {
            _logger.LogWarning("Hesap kilitli: {AccountId}, {Until}'a kadar.",
                account.Id, account.LockoutUntil);
            return LoginResult.Failure(LoginFailureCode.AccountLocked);
        }

        // 5. Parola doğrula
        var verifyResult = _passwordHasher.Verify(account.PasswordHash, command.Password);
        if (verifyResult == PasswordVerificationResult.Failed)
        {
            account.RecordFailedLogin(now, MaxFailedAttempts, LockoutDuration);
            await _db.SaveChangesAsync(ct);

            _logger.LogWarning(
                "Login başarısız: yanlış parola (account={AccountId}, count={Count}).",
                account.Id, account.FailedLoginCount);

            return account.LockoutUntil.HasValue && now < account.LockoutUntil.Value
                ? LoginResult.Failure(LoginFailureCode.AccountLocked)
                : LoginResult.Failure(LoginFailureCode.InvalidCredentials);
        }

        // 6. Hesap durumu (ayrı hata kodları)
        if (!account.IsActive)
            return LoginResult.Failure(LoginFailureCode.AccountInactive);

        if (account.ValidFrom.HasValue && now < account.ValidFrom.Value)
            return LoginResult.Failure(LoginFailureCode.AccountOutOfValidity);
        if (account.ValidTo.HasValue && now > account.ValidTo.Value)
            return LoginResult.Failure(LoginFailureCode.AccountOutOfValidity);

        if (!CidrMatcher.IsIpInAnyRange(command.ClientContext.IpAddress, account.IpWhitelist))
        {
            _logger.LogWarning("IP whitelist engeledi: {Ip} (account={AccountId}).",
                command.ClientContext.IpAddress, account.Id);
            return LoginResult.Failure(LoginFailureCode.IpNotAllowed);
        }

        // LoginSchedule — MVP'de parsing yok (JSON evaluator v2'de).

        // 7. Başarılı — sayacı sıfırla
        account.RecordSuccessfulLogin(now, command.ClientContext.IpAddress);

        if (verifyResult == PasswordVerificationResult.SuccessRehashNeeded)
        {
            account.ChangePasswordHash(_passwordHasher.Hash(command.Password));
            _logger.LogInformation("LoginAccount {AccountId} parolası rehash edildi.", account.Id);
        }

        // 8. Memberships topla
        var memberships = await LoadMembershipsAsync(account.Id, now, ct);

        // 9. Eski session'ları kapat (tek oturum)
        var closedOld = await _sessionStore.DeleteByLoginAccountAsync(account.Id, ct);

        // 10. DeviceId + Session
        var deviceId = GenerateDeviceId();

        var session = Session.Create(
            loginAccountId: account.Id,
            personId: person.Id,
            deviceId: deviceId,
            ipAddress: command.ClientContext.IpAddress,
            userAgent: command.ClientContext.UserAgent,
            isMobile: command.ClientContext.IsMobile,
            availableContexts: memberships,
            now: now);

        await _sessionStore.SaveAsync(session, ct);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Login başarılı: person={PersonId}, account={AccountId}, session={SessionId}, eski kapatıldı={Count}.",
            person.Id, account.Id, session.SessionId, closedOld.Count);

        return LoginResult.Success(session.SessionId, deviceId, closedOld);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────

    private Task<Person?> FindPersonAsync(string normalizedInput, LoginInputType type, CancellationToken ct)
    {
        return type switch
        {
            LoginInputType.Tckn or LoginInputType.Vkn or LoginInputType.Ykn
                => _db.Persons.FirstOrDefaultAsync(p => p.NationalId.Value == normalizedInput, ct),

            LoginInputType.Email
                => _db.Persons.FirstOrDefaultAsync(p => p.Email == normalizedInput, ct),

            LoginInputType.Mobile
                => _db.Persons.FirstOrDefaultAsync(p => p.MobilePhone == normalizedInput, ct),

            _ => Task.FromResult<Person?>(null)
        };
    }

    private async Task<IReadOnlyList<MembershipSummary>> LoadMembershipsAsync(
        LoginAccountId accountId, DateTimeOffset now, CancellationToken ct)
    {
        var raw = await (from m in _db.Memberships
                         join r in _db.Roles on m.RoleId equals r.Id
                         where m.LoginAccountId == accountId && m.IsActive
                         select new { Membership = m, Role = r })
            .AsNoTracking()
            .ToListAsync(ct);

        var result = new List<MembershipSummary>(raw.Count);

        foreach (var x in raw)
        {
            if (!x.Membership.IsEffectiveAt(now)) continue;

            // MVP placeholder — context adı ileride Organization/Site tablolarından resolve
            var displayName = x.Membership.ContextType switch
            {
                MembershipContextType.System => "Sistem",
                MembershipContextType.Organization => "Organizasyon",
                MembershipContextType.Site => "Site",
                MembershipContextType.Branch => "Şube",
                MembershipContextType.ServiceOrganization => "Servis Firması",
                _ => "Bilinmiyor"
            };

            result.Add(new MembershipSummary(
                MembershipId: x.Membership.Id.Value,
                ContextType: (int)x.Membership.ContextType,
                ContextId: x.Membership.ContextId,
                ContextCode: null,
                ContextDisplayName: displayName,
                RoleId: x.Role.Id.Value,
                RoleName: x.Role.Name));
        }

        return result;
    }

    /// <summary>
    /// 32-byte crypto-random deviceId (base64-url). Cookie + Session'da saklanır.
    /// </summary>
    private static string GenerateDeviceId()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
