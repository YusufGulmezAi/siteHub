using MediatR;

namespace SiteHub.Application.Features.Authentication.PasswordReset;

/// <summary>
/// Şifre sıfırlama uygulama komutu (ADR-0011 §5.1 adım 6-8).
///
/// <para>Email linkinden veya SMS kodundan gelen token + yeni şifre ile çağrılır.
/// Token doğrulanır → hash güncellenir → <b>tüm aktif session'lar kapatılır</b>
/// (§7.5 güvenlik gereksinimi).</para>
/// </summary>
public sealed record ResetPasswordCommand(
    string Token,
    string NewPassword,
    string IpAddress) : IRequest<ResetPasswordResult>;

public sealed record ResetPasswordResult(
    bool IsSuccess,
    ResetPasswordFailureCode FailureCode = ResetPasswordFailureCode.None)
{
    public static ResetPasswordResult Success() => new(true);
    public static ResetPasswordResult Failure(ResetPasswordFailureCode code) => new(false, code);
}

public enum ResetPasswordFailureCode
{
    None = 0,

    /// <summary>Token boş, kısa, format hatası.</summary>
    InvalidToken = 1,

    /// <summary>Token DB'de yok.</summary>
    TokenNotFound = 2,

    /// <summary>Token daha önce kullanılmış.</summary>
    TokenAlreadyUsed = 3,

    /// <summary>Token süresi dolmuş.</summary>
    TokenExpired = 4,

    /// <summary>Şifre politikasına uymuyor.</summary>
    WeakPassword = 5,

    /// <summary>LoginAccount bulunamadı (token'ın referans ettiği hesap silinmiş vs.).</summary>
    AccountNotFound = 6
}
