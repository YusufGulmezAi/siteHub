using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SiteHub.Application.Abstractions.Authentication;
using SiteHub.Domain.Identity;
using SiteHub.Domain.Identity.Authorization;

namespace SiteHub.Infrastructure.Persistence.Seed;

/// <summary>
/// Development ortamında hızlı test için varsayılan sistem yöneticisi seed eder.
///
/// <para>Production'da ASLA çalışmamalı (ASPNETCORE_ENVIRONMENT=Development kontrolü).</para>
///
/// <para>Oluşturulan kullanıcı:</para>
/// <list type="bullet">
///   <item>Person: "Sistem Yöneticisi" (TCKN: 11111111110, email: admin@sitehub.local)</item>
///   <item>LoginAccount: admin@sitehub.local / Admin123!</item>
///   <item>Membership: System + Sistem Yöneticisi rolü (32 izin)</item>
/// </list>
///
/// <para>İdempotent: Person zaten varsa atlanır.</para>
/// </summary>
public sealed class DevelopmentUsersSeeder
{
    // Test kullanıcısı sabit değerler — sadece development ortamda
    // TCKN "10000000146" checksum-valid test değeridir (gerçek kişiye ait değil).
    private const string AdminNationalId = "10000000146";
    private const string AdminFullName = "Sistem Yöneticisi";
    private const string AdminMobile = "+905551112233";
    private const string AdminEmail = "admin@sitehub.local";
    private const string AdminPassword = "Admin123!";

    private readonly SiteHubDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<DevelopmentUsersSeeder> _logger;

    public DevelopmentUsersSeeder(
        SiteHubDbContext db,
        IPasswordHasher passwordHasher,
        ILogger<DevelopmentUsersSeeder> logger)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Development test kullanıcısı seed başlatılıyor...");

        // 1. Person
        var adminNationalId = NationalId.CreateTckn(AdminNationalId);

        var existingPerson = await _db.Persons
            .FirstOrDefaultAsync(p => p.NationalId == adminNationalId, ct);

        Person person;
        if (existingPerson is not null)
        {
            person = existingPerson;
            _logger.LogInformation("Admin Person zaten var: {PersonId}.", person.Id);
        }
        else
        {
            person = Person.Create(
                nationalId: adminNationalId,
                fullName: AdminFullName,
                mobilePhone: AdminMobile,
                email: AdminEmail,
                kepAddress: null);
            _db.Persons.Add(person);
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Admin Person oluşturuldu: {PersonId}.", person.Id);
        }

        // 2. LoginAccount
        var existingAccount = await _db.LoginAccounts
            .FirstOrDefaultAsync(a => a.PersonId == person.Id, ct);

        LoginAccount account;
        if (existingAccount is not null)
        {
            account = existingAccount;
            _logger.LogInformation("Admin LoginAccount zaten var: {AccountId}.", account.Id);
        }
        else
        {
            var hash = _passwordHasher.Hash(AdminPassword);
            account = LoginAccount.Create(person.Id, AdminEmail, hash);
            _db.LoginAccounts.Add(account);
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Admin LoginAccount oluşturuldu: {AccountId}.", account.Id);
        }

        // 3. Membership (System scope + Sistem Yöneticisi rolü)
        var sistemAdminRole = await _db.Roles
            .FirstOrDefaultAsync(r =>
                r.IsSystem &&
                r.Scope == RoleScope.System &&
                r.Name == "Sistem Yöneticisi", ct);

        if (sistemAdminRole is null)
        {
            _logger.LogWarning(
                "'Sistem Yöneticisi' rolü bulunamadı — SystemRolesSeeder önce çalışmalı. Membership oluşturulmadı.");
            return;
        }

        var existingMembership = await _db.Memberships
            .FirstOrDefaultAsync(m =>
                m.LoginAccountId == account.Id &&
                m.ContextType == MembershipContextType.System &&
                m.RoleId == sistemAdminRole.Id, ct);

        if (existingMembership is not null)
        {
            _logger.LogInformation("Admin Membership zaten var: {MembershipId}.", existingMembership.Id);
        }
        else
        {
            var membership = Membership.Create(
                loginAccountId: account.Id,
                contextType: MembershipContextType.System,
                contextId: null,
                roleId: sistemAdminRole.Id);
            _db.Memberships.Add(membership);
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Admin Membership oluşturuldu: {MembershipId}.", membership.Id);
        }

        _logger.LogInformation(
            "Development test kullanıcısı hazır. Email: {Email}, Parola: {Password}.",
            AdminEmail, AdminPassword);
    }
}
