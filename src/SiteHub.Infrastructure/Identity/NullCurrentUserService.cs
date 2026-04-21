using SiteHub.Application.Abstractions.Audit;

namespace SiteHub.Infrastructure.Identity;

/// <summary>
/// ICurrentUserService'in GEÇİCİ implementasyonu.
///
/// Henüz authentication kurulmadığı için tüm işlemler "anonim" görünür.
/// Identity + Cookie auth eklendiğinde bu sınıf genişletilecek:
///   - HttpContextAccessor.HttpContext.User.Claims'ten okunacak
///   - NameIdentifier → UserId
///   - Name/Email → UserName
/// </summary>
public sealed class NullCurrentUserService : ICurrentUserService
{
    public Guid? UserId => null;
    public string? UserName => null;
}
