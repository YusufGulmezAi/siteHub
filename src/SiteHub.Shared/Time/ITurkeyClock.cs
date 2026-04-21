namespace SiteHub.Shared.Time;

/// <summary>
/// Türkiye saati (Europe/Istanbul) için zaman servisi.
///
/// KULLANIM KURALI:
/// - Backend iş mantığı UTC ile çalışır (TimeProvider.GetUtcNow())
/// - UI'da görüntüleme / kullanıcıya gösterme için bu servis kullanılır
/// - Asla DateTime.Now veya DateTimeOffset.Now kullanma!
///
/// Test edilebilirlik için interface üzerinden inject edilir.
/// </summary>
public interface ITurkeyClock
{
    /// <summary>Europe/Istanbul TimeZoneInfo.</summary>
    TimeZoneInfo Zone { get; }

    /// <summary>Şu an Türkiye saatiyle (UTC+3 veya yaz saati).</summary>
    DateTimeOffset Now { get; }

    /// <summary>Şu an UTC olarak.</summary>
    DateTimeOffset UtcNow { get; }

    /// <summary>UTC zamanı Türkiye saatine çevirir.</summary>
    DateTimeOffset ToLocal(DateTimeOffset utcTime);

    /// <summary>UTC zamanı Türkiye saatine çevirir ve DateTime olarak döner.</summary>
    DateTime ToLocalDateTime(DateTimeOffset utcTime);

    /// <summary>
    /// UTC zamanı Türkiye saatiyle formatlar.
    /// Varsayılan format: "dd.MM.yyyy HH:mm" (Türk kullanıcılarının alışık olduğu).
    /// </summary>
    string Format(DateTimeOffset utcTime, string? format = null);

    /// <summary>
    /// Türkiye saatindeki ay başını UTC DateTimeOffset olarak döner.
    /// Ay/yıl raporlamalarda kullanılır.
    /// </summary>
    DateTimeOffset StartOfMonthInTurkey(int year, int month);
}
