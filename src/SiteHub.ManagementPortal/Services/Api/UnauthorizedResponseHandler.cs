using System.Net;
using Microsoft.Extensions.Logging;
using SiteHub.ManagementPortal.Services.Authentication;

namespace SiteHub.ManagementPortal.Services.Api;

/// <summary>
/// API'den 401 Unauthorized dönünce <see cref="IAuthenticationEventService"/> event'ini
/// tetikler (F.6 Madde 9).
///
/// <para><b>Session identifier:</b> Event'e outgoing Cookie header'ından çıkarılan
/// auth cookie değeri geçirilir. Bu değer browser cookie'siyle aynıdır —
/// MainLayout aynı cookie değerini kendi HttpContext'inden okuyup karşılaştırır
/// (circuit'ler arası izolasyon).</para>
///
/// <para><b>Handler sırası (Program.cs):</b>
/// <c>CookieForwardingHandler</c> (cookie'leri header'a ekler) → <c>UnauthorizedResponseHandler</c>
/// (401 kontrolü). Cookie handler önce register edilir, Unauthorized sonra register edilir.
/// Pipeline'da Unauthorized önce çalışır ama Cookie header zaten request'te olduğu için
/// biz okuyabiliriz.</para>
///
/// <para><b>Ignore URL'leri:</b> <c>/auth/login</c>, <c>/auth/logout</c>, <c>/auth/verify-2fa</c>
/// — bu endpoint'lerde 401 normaldir, dialog açılmamalı.</para>
/// </summary>
internal sealed class UnauthorizedResponseHandler : DelegatingHandler
{
    // Auth cookie adı (AddSiteHubCookieAuthentication'da tanımlı)
    private const string AuthCookieName = ".SiteHub.Mgmt";

    private static readonly string[] IgnoredPathPrefixes = new[]
    {
        "/auth/login",
        "/auth/logout",
        "/auth/verify-2fa",
    };

    private readonly IAuthenticationEventService _authEvents;
    private readonly ILogger<UnauthorizedResponseHandler> _logger;

    public UnauthorizedResponseHandler(
        IAuthenticationEventService authEvents,
        ILogger<UnauthorizedResponseHandler> logger)
    {
        _authEvents = authEvents;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Cookie header'dan session identifier'ı çıkar — SONRASINDA request gönderilir
        // (base.SendAsync request'i değiştirmiyor, sadece response bekliyoruz)
        var sessionIdentifier = ExtractSessionIdentifier(request);

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode != HttpStatusCode.Unauthorized)
            return response;

        if (ShouldIgnore(request.RequestUri))
        {
            _logger.LogDebug("401 yoksayıldı (auth endpoint): {Uri}", request.RequestUri);
            return response;
        }

        if (string.IsNullOrEmpty(sessionIdentifier))
        {
            _logger.LogWarning(
                "401 alındı ama session identifier yok — cookie forwarding çalışmamış olabilir. URI: {Uri}",
                request.RequestUri);
            return response;
        }

        _logger.LogWarning("401 Unauthorized: {Uri} — SessionExpired event tetikleniyor.",
            request.RequestUri);

        try
        {
            await _authEvents.RaiseSessionExpiredAsync(sessionIdentifier);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SessionExpired event tetiklenirken hata.");
        }

        return response;
    }

    /// <summary>
    /// Outgoing request'in Cookie header'ından <c>.SiteHub.Mgmt</c> değerini çıkarır.
    /// CookieForwardingHandler browser cookie'sini kopyalamıştı — o header burada var.
    /// </summary>
    private static string? ExtractSessionIdentifier(HttpRequestMessage request)
    {
        if (!request.Headers.TryGetValues("Cookie", out var cookieHeaders))
            return null;

        foreach (var header in cookieHeaders)
        {
            // Cookie header formatı: "name1=value1; name2=value2; ..."
            foreach (var part in header.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = part.Trim();
                var eqIndex = trimmed.IndexOf('=');
                if (eqIndex <= 0) continue;

                var name = trimmed.Substring(0, eqIndex);
                if (string.Equals(name, AuthCookieName, StringComparison.Ordinal))
                {
                    return trimmed.Substring(eqIndex + 1);
                }
            }
        }

        return null;
    }

    private static bool ShouldIgnore(Uri? uri)
    {
        if (uri is null) return false;

        var path = uri.AbsolutePath;
        foreach (var prefix in IgnoredPathPrefixes)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
