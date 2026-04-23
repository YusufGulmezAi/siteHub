using SiteHub.Domain.Identity;
using SiteHub.Domain.Identity.Sessions;

namespace SiteHub.Application.Abstractions.Authorization;

/// <summary>
/// Bir kullanıcının effective permission set'ini hesaplar (ADR-0011 §8.1, F.6 C.2).
///
/// <para>Login zamanında bir kez çağrılır; sonuç <see cref="Session.Permissions"/>
/// içinde Redis'e serialize olur. Her request'te tekrar hesaplanmaz (performans).</para>
///
/// <para>Permission değişimi (role'e izin ekleme, membership değişimi) session'a
/// hemen yansımaz — 15 dk TTL süresi ya da kullanıcı logout/login yapınca yenilenir.
/// Anlık invalidation MVP dışı (ADR-0011 §8.3 ileri faz).</para>
/// </summary>
public interface IPermissionComputer
{
    Task<PermissionSet> ComputeAsync(
        LoginAccountId loginAccountId,
        DateTimeOffset now,
        CancellationToken ct = default);
}
