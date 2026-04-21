namespace SiteHub.Application.Abstractions.Context;

/// <summary>
/// Aktif bağlamın (context) türü.
/// Kullanıcı combobox'tan hangi seviyede çalışacağını seçer.
/// </summary>
public enum ActiveContextType
{
    /// <summary>Sistem geneli — sadece System Membership sahipleri için.</summary>
    System = 0,

    /// <summary>Kiracı (Yönetim Firması) seviyesinde çalışıyor.</summary>
    Organization = 1,

    /// <summary>Tek bir Site seviyesinde çalışıyor.</summary>
    Site = 2,

    /// <summary>Tek bir Bağımsız Bölüm (Unit) seviyesinde — malik/sakin portalı.</summary>
    Unit = 3
}

/// <summary>
/// Aktif bağlamın bilgisi.
/// Request/circuit-scoped bir servistir.
/// </summary>
public sealed record ActiveContext(ActiveContextType Type, Guid Id, string DisplayName);

/// <summary>
/// Şu anki kullanıcının, şu sekmede çalıştığı aktif bağlamı veren servis.
///
/// ÇOK ÖNEMLİ: Blazor Server'da bu servis SCOPED olarak kaydedilir.
/// Her SignalR circuit (= her tarayıcı sekmesi) kendi Active Context'ine sahiptir.
/// Bu sayede kullanıcı aynı tarayıcıda farklı sekmelerde farklı bağlamlarda
/// çalışabilir; bağlamlar birbirine karışmaz.
///
/// Güvenlik: UI'daki context seçimi yalnızca UX içindir. Her CQRS command/query
/// ContextAuthorizationBehavior tarafından yetki açısından ayrıca denetlenir.
/// </summary>
public interface IActiveContextAccessor
{
    /// <summary>Mevcut aktif bağlam (varsa). Login sonrası default context atanır.</summary>
    ActiveContext? Current { get; }

    /// <summary>
    /// Bağlamı değiştirir. Yetki kontrolü yapar — kullanıcı bu bağlama erişebilmeli.
    /// UI'ın çağıracağı ana metot.
    /// </summary>
    Task SwitchAsync(ActiveContextType type, Guid id, CancellationToken cancellationToken = default);
}
