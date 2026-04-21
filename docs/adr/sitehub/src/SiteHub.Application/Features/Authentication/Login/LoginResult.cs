using SiteHub.Domain.Identity.Sessions;

namespace SiteHub.Application.Features.Authentication.Login;

/// <summary>
/// Login işleminin sonucu — başarı veya hata.
///
/// <para>Controller/Blazor bu sonuca göre cookie set eder + redirect yapar veya hata gösterir.</para>
///
/// <para>IsSuccess=true ise: SessionId + DeviceId set edilir, cookie'lere yazılır.
/// IsSuccess=false ise: FailureCode set edilir (AccountInactive, IpNotAllowed, ...) —
/// UI bu koda göre Türkçe mesaj gösterir.</para>
/// </summary>
public sealed record LoginResult
{
    public required bool IsSuccess { get; init; }
    public LoginFailureCode FailureCode { get; init; }

    // Başarı durumunda dolu
    public SessionId? SessionId { get; init; }
    public string? DeviceId { get; init; }

    // Kaç eski session kapatıldı (SignalR broadcast için — faz 3'te)
    public IReadOnlyList<SessionId> ClosedOldSessions { get; init; } = Array.Empty<SessionId>();

    public static LoginResult Success(SessionId sessionId, string deviceId, IReadOnlyList<SessionId> closedOld) =>
        new()
        {
            IsSuccess = true,
            SessionId = sessionId,
            DeviceId = deviceId,
            ClosedOldSessions = closedOld
        };

    public static LoginResult Failure(LoginFailureCode code) => new()
    {
        IsSuccess = false,
        FailureCode = code
    };
}

/// <summary>
/// Login başarısızlık nedenleri (ADR-0011 §3.2).
/// Her kod için audit.security_events'e EventType yazılır.
/// </summary>
public enum LoginFailureCode
{
    None = 0,

    /// <summary>Input formatı tanınamadı (TCKN/VKN/Email/Mobile hiçbirine uymuyor).</summary>
    InvalidInputFormat = 1,

    /// <summary>Hesap bulunamadı VEYA şifre yanlış. Birlikte döner (enumeration attack savunması).</summary>
    InvalidCredentials = 2,

    /// <summary>Hesap aktif değil (IsActive=false).</summary>
    AccountInactive = 3,

    /// <summary>Şu an ValidFrom/ValidTo aralığında değil.</summary>
    AccountOutOfValidity = 4,

    /// <summary>IP whitelist dolu + şimdiki IP içinde değil.</summary>
    IpNotAllowed = 5,

    /// <summary>LoginSchedule'a göre şu an giriş saatinde değil.</summary>
    ScheduleBlocked = 6,

    /// <summary>Hesap lockout süresinde (çok fazla yanlış şifre).</summary>
    AccountLocked = 7,

    /// <summary>Mobile login — OTP gerekli (henüz MVP'de değil).</summary>
    OtpRequired = 8,

    /// <summary>2FA gerekli (henüz MVP'de değil).</summary>
    TwoFactorRequired = 9,
}
