namespace SiteHub.Application.Abstractions.Context;

/// <summary>
/// Şu an giriş yapmış kullanıcının özet bilgisi (Session'dan çıkarılır).
///
/// <para>Blazor component'ler bu servisi <c>@inject ICurrentUser Current</c>
/// ile kullanır. Login sayfasında (henüz session yok) değerler null döner.</para>
/// </summary>
public interface ICurrentUser
{
    /// <summary>Giriş yapmış mı?</summary>
    bool IsAuthenticated { get; }

    /// <summary>Session ID (Redis key).</summary>
    Guid? SessionId { get; }

    /// <summary>LoginAccount ID.</summary>
    Guid? LoginAccountId { get; }

    /// <summary>Person ID.</summary>
    Guid? PersonId { get; }

    /// <summary>Kullanıcının tam adı (Person.FullName).</summary>
    string? FullName { get; }

    /// <summary>Login email.</summary>
    string? Email { get; }

    /// <summary>Avatar harfleri — "Ahmet Yılmaz" → "AY".</summary>
    string Initials { get; }

    /// <summary>Sahip olduğu membership sayısı (UI'da badge göstermek için).</summary>
    int MembershipCount { get; }

    /// <summary>
    /// Session 2FA bekliyor mu? True ise kullan\u0131c\u0131 sadece
    /// <c>/auth/verify-2fa</c> sayfas\u0131na y\u00f6nlendirilmeli.
    /// </summary>
    bool Pending2FA { get; }
}
