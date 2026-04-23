using SiteHub.Domain.Identity.Authorization;
using SiteHub.Domain.Identity.Sessions;

namespace SiteHub.Application.Abstractions.Authorization;

/// <summary>
/// Mevcut kullanıcının permission'larını runtime'da sorgular (F.6 C.2).
///
/// <para>Implementasyon aktif session'dan (Redis'teki) PermissionSet'i okur ve
/// <see cref="PermissionSet.Has"/> ile sorguyu cevaplar. DB call yok, O(1) lookup.</para>
///
/// <para>Kullanım:</para>
/// <list type="bullet">
///   <item>Blazor component: <c>@inject ICurrentUserPermissionService Perms</c> +
///     <c>await Perms.HasAsync("site.update", MembershipContextType.Site, siteId)</c></item>
///   <item>API endpoint (MVP'de policy handler yok): aynı pattern ile direkt çağrı</item>
///   <item><c>HasPermission.razor</c> bileşeni kullanım ergonomisi için</item>
/// </list>
///
/// <para>Session yoksa (anonim kullanıcı) tüm sorular false döner.</para>
/// </summary>
public interface ICurrentUserPermissionService
{
    /// <summary>
    /// Kullanıcı verilen permission'a sahip mi?
    ///
    /// <para>Context parametresi verilmişse o context'te kontrol (System scope short-circuit
    /// ile otomatik geçiş dahil). Verilmemişse herhangi bir context'te varsa true.</para>
    /// </summary>
    Task<bool> HasAsync(
        string permission,
        MembershipContextType? contextType = null,
        Guid? contextId = null);

    /// <summary>
    /// Kullanıcının mevcut PermissionSet'ini döner (debug/inspect için).
    /// Session yoksa null.
    /// </summary>
    Task<PermissionSet?> GetPermissionSetAsync();

    /// <summary>
    /// Kullanıcı authenticated + permission set dolu mu?
    /// </summary>
    Task<bool> IsAuthenticatedAsync();
}
