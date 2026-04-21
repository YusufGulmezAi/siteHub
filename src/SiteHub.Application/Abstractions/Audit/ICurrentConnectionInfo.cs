namespace SiteHub.Application.Abstractions.Audit;

/// <summary>
/// O anki HTTP request'in / Blazor circuit'in ağ bilgileri.
///
/// Audit kaydına "nereden" yazmak için kullanılır.
///
/// Implementation: Infrastructure/Connection/CurrentConnectionInfoService.cs
///   - ASP.NET Core: IHttpContextAccessor.HttpContext.Connection.RemoteIpAddress
///   - X-Forwarded-For, X-Real-IP header'larına da bakar (reverse proxy ardında)
///   - User-Agent header
///   - Blazor Server circuit için circuit başlarken snapshot alınır
///
/// Gelecekte: IP'den şehir çözümü (MaxMind GeoLite2) EKLENECEK.
/// </summary>
public interface ICurrentConnectionInfo
{
    /// <summary>İşlemin yapıldığı IP adresi. "127.0.0.1", "185.x.x.x", vs.</summary>
    string? IpAddress { get; }

    /// <summary>Tarayıcı / cihaz bilgisi — User-Agent header.</summary>
    string? UserAgent { get; }

    /// <summary>Bu request'in benzersiz ID'si (correlation). Aynı request'teki tüm audit kayıtları ilintilenir.</summary>
    string? CorrelationId { get; }

    // TODO (v2): şehir/ülke bilgisi — IP2Location veya MaxMind GeoLite2
    //  string? City { get; }
    //  string? Country { get; }
}
