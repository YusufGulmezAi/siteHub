using Microsoft.AspNetCore.Http;
using SiteHub.Application.Abstractions.Context;
using SiteHub.Domain.Identity.Sessions;
using SiteHub.Infrastructure.Authentication;

namespace SiteHub.Infrastructure.Context;

/// <summary>
/// <see cref="ICurrentUser"/>'ın HTTP tabanlı implementasyonu.
///
/// <para>Session'ı <c>SessionValidationMiddleware</c>'in doldurduğu
/// <c>HttpContext.Items["SiteHub:Session"]</c> slotundan okur.
/// Session yoksa (login sayfası veya unauthenticated istek) tüm alanlar null döner.</para>
///
/// <para>SCOPED (per-request). Blazor Server'da her SignalR circuit (= browser sekmesi)
/// kendi request'ini taşır, o yüzden scoped servis yeterli.</para>
/// </summary>
public sealed class HttpCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _http;

    public HttpCurrentUser(IHttpContextAccessor http) => _http = http;

    private Session? Session => _http.HttpContext?.Items[SessionValidationMiddleware.SessionContextKey] as Session;

    public bool IsAuthenticated => Session is not null;
    public Guid? SessionId => Session?.SessionId.Value;
    public Guid? LoginAccountId => Session?.LoginAccountId.Value;
    public Guid? PersonId => Session?.PersonId.Value;
    public string? FullName => Session?.FullName;
    public string? Email => Session?.Email;
    public int MembershipCount => Session?.AvailableContexts.Count ?? 0;
    public bool Pending2FA => Session?.Pending2FA ?? false;

    public string Initials
    {
        get
        {
            var name = Session?.FullName;
            if (string.IsNullOrWhiteSpace(name)) return "?";

            var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return "?";
            if (parts.Length == 1) return parts[0][..1].ToUpperInvariant();

            // İlk ve son kelimenin baş harfi → "Ahmet Mehmet Yılmaz" → "AY"
            var first = parts[0][..1];
            var last = parts[^1][..1];
            return (first + last).ToUpperInvariant();
        }
    }
}
