using MediatR;

namespace SiteHub.Application.Features.Authentication.PasswordReset;

/// <summary>
/// Şifre sıfırlama talebi (ADR-0011 §5.1).
///
/// <para>Input — tek alan, TCKN/VKN/YKN/Email/Mobile auto-detect.
/// Channel — kullanıcının seçtiği kanal (Email/Sms).</para>
///
/// <para>Kullanıcı yok sa veya kanal eşleşmiyor sa bile <b>200 OK</b> dönülür
/// (user enumeration attack savunması). Sadece log'a yazılır.</para>
/// </summary>
public sealed record RequestPasswordResetCommand(
    string Input,
    ResetChannelChoice Channel,
    string IpAddress) : IRequest<RequestPasswordResetResult>;

public enum ResetChannelChoice
{
    Email = 1,
    Sms = 2
}

/// <summary>
/// Handler her zaman aynı şeyi döner — kullanıcının bulunup bulunmadığını ifşa etmez.
/// UI'da herhangi bir durumda "Talimatlar gönderildi, e-postanızı/telefonunuzu kontrol edin."
/// gibi genel bir mesaj gösterilir.
/// </summary>
public sealed record RequestPasswordResetResult(
    bool AlwaysTrue = true);
