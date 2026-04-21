namespace SiteHub.Application.Abstractions.Audit;

/// <summary>
/// O anda işlem yapan kullanıcının bilgilerini sağlar.
///
/// Interceptor (SaveChanges), audit log yazarken bunu okur.
/// Controller/Blazor component'lerinden de okunabilir (current user kimdir?).
///
/// Implementation: Infrastructure/Identity/CurrentUserService.cs
///   - HttpContext.User.Claims'ten okur (Cookie auth)
///   - Blazor Server: AuthenticationStateProvider'dan
///
/// DI lifetime: Scoped — HTTP request / Blazor circuit başına yeni instance.
///
/// Anonymous kullanıcı (login yok) veya system işlemi durumunda UserId null'dır.
/// </summary>
public interface ICurrentUserService
{
    /// <summary>Giriş yapmış kullanıcının ID'si. Anonim ise null.</summary>
    Guid? UserId { get; }

    /// <summary>Giriş yapmış kullanıcının görüntülenecek adı (tam ad veya email).</summary>
    string? UserName { get; }

    /// <summary>Kullanıcı giriş yapmış mı?</summary>
    bool IsAuthenticated => UserId.HasValue;
}
