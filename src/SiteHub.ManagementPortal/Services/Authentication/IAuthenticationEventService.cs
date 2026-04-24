namespace SiteHub.ManagementPortal.Services.Authentication;

/// <summary>
/// Blazor circuit'ler arasında authentication event'lerini yayınlar (F.6 Madde 9).
///
/// <para><b>DI scope:</b> Singleton. HTTP request handler'ları ile SignalR circuit'leri
/// farklı DI scope'larda çalışır; aynı instance'ı paylaşabilmek için Singleton zorunlu.</para>
///
/// <para><b>Multi-user broadcast problemi:</b> Singleton olduğu için A kullanıcısının
/// 401'inde B kullanıcısına da event gider. Bunu engellemek için event'e
/// <see cref="SessionExpiredEventArgs.SessionIdentifier"/> eklendi. Her MainLayout
/// subscriber'ı sadece kendi session identifier'ına ait event'e tepki verir.</para>
///
/// <para><b>Kullanım:</b></para>
/// <code>
/// // Handler tarafı (401 yakalayınca):
/// await _events.RaiseSessionExpiredAsync(sessionIdentifier);
///
/// // Layout tarafı (OnInitialized'de):
/// _events.SessionExpired += OnSessionExpired;
///
/// // Layout tarafı (handler):
/// private Task OnSessionExpired(SessionExpiredEventArgs e)
/// {
///     if (e.SessionIdentifier != _mySessionIdentifier) return Task.CompletedTask;
///     return ShowDialogAsync();
/// }
/// </code>
/// </summary>
public interface IAuthenticationEventService
{
    /// <summary>Bir session için 401 geldi — o session'a ait circuit'ler dialog göstermeli.</summary>
    event Func<SessionExpiredEventArgs, Task>? SessionExpired;

    /// <summary>
    /// SessionExpired event'ini tetikler. Aynı <paramref name="sessionIdentifier"/> için
    /// ikinci çağrı yoksayılır (tek-tetikleme — paralel 401'lerde dialog bir kez açılır).
    /// </summary>
    /// <param name="sessionIdentifier">
    /// Session'ı tanımlayan opak string (ör. auth cookie değeri). Layout subscriber'ları
    /// kendi identifier'ları ile eşleştirip filtreler.
    /// </param>
    Task RaiseSessionExpiredAsync(string sessionIdentifier);
}

/// <summary>
/// SessionExpired event parametreleri.
/// </summary>
public sealed record SessionExpiredEventArgs(string SessionIdentifier);
