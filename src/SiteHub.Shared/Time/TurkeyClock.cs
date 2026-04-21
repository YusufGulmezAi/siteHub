namespace SiteHub.Shared.Time;

using System.Globalization;

/// <summary>
/// ITurkeyClock'un üretim implementasyonu.
///
/// .NET 8+ TimeProvider abstraction'ı kullanır — bu sayede:
/// - Production'da TimeProvider.System (gerçek saat)
/// - Testte FakeTimeProvider (zaman kontrolü)
///
/// DI kaydı:
///   services.AddSingleton(TimeProvider.System);
///   services.AddSingleton&lt;ITurkeyClock, TurkeyClock&gt;();
/// </summary>
public sealed class TurkeyClock : ITurkeyClock
{
    // IANA timezone kodu — Linux/macOS üzerinde çalışır.
    // Windows'ta .NET otomatik olarak "Turkey Standard Time"a map eder.
    private const string TurkeyZoneId = "Europe/Istanbul";

    private const string DefaultFormat = "dd.MM.yyyy HH:mm";

    // Türkçe kültür — tarih/saat formatlarken 'dddd', 'MMMM' gibi tokenlar
    // Türkçe (Pazartesi, Ocak) olarak gelsin.
    private static readonly CultureInfo TurkishCulture = CultureInfo.GetCultureInfo("tr-TR");

    private readonly TimeProvider _timeProvider;

    public TurkeyClock(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
        Zone = TimeZoneInfo.FindSystemTimeZoneById(TurkeyZoneId);
    }

    public TimeZoneInfo Zone { get; }

    public DateTimeOffset UtcNow => _timeProvider.GetUtcNow();

    public DateTimeOffset Now => TimeZoneInfo.ConvertTime(UtcNow, Zone);

    public DateTimeOffset ToLocal(DateTimeOffset utcTime)
        => TimeZoneInfo.ConvertTime(utcTime, Zone);

    public DateTime ToLocalDateTime(DateTimeOffset utcTime)
        => TimeZoneInfo.ConvertTime(utcTime, Zone).DateTime;

    public string Format(DateTimeOffset utcTime, string? format = null)
        => ToLocal(utcTime).ToString(format ?? DefaultFormat, TurkishCulture);

    public DateTimeOffset StartOfMonthInTurkey(int year, int month)
    {
        var localStart = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var offset = Zone.GetUtcOffset(localStart);
        return new DateTimeOffset(localStart, offset);
    }
}
