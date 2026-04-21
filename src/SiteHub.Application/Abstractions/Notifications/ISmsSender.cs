namespace SiteHub.Application.Abstractions.Notifications;

/// <summary>
/// SMS gönderme soyutlaması.
///
/// <para>MVP'de implementation: <c>ConsoleSmsSender</c> (log'a yazdırır, gerçek SMS yok).
/// Prod'da: Netgsm, İletimerkezi, Twilio gibi bir sağlayıcı.</para>
///
/// <para>SMS provider seçimi iş kararı — bu interface değişmeyecek, sadece
/// implementation'ı swap ederiz.</para>
/// </summary>
public interface ISmsSender
{
    /// <summary>
    /// SMS gönderir. Telefon E.164 formatında (+905321234567).
    /// Message 160 karakterden uzunsa sağlayıcı otomatik çoklu SMS'e böler.
    /// </summary>
    Task SendAsync(string e164Phone, string message, CancellationToken ct = default);
}
