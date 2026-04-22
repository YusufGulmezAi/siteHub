using Microsoft.AspNetCore.Http;

namespace SiteHub.ManagementPortal.Services.Api;

/// <summary>
/// HttpClient üzerinden API çağırırken browser'ın auth cookie'sini (ör. <c>.SiteHub.Mgmt</c>)
/// ve DeviceId cookie'sini header'a ekler. Aksi takdirde API endpoint'leri 401 döner çünkü
/// HttpClient default'ta kendi cookie container'ını kullanır, browser cookie'lerini bilmez.
///
/// <para><b>Neden gerekli:</b> Blazor Server'da kullanıcı browser'da oturum açıyor (cookie
/// browser'da); Blazor component API'ye HttpClient ile çağrı yapıyor (server tarafı). Bu ikisi
/// arasındaki köprü bu handler.</para>
///
/// <para><b>Nasıl çalışır:</b> <see cref="IHttpContextAccessor"/> ile current request'in
/// cookie'lerini okur, aynı cookie'leri outgoing HTTP isteğinin <c>Cookie</c> header'ına koyar.
/// SessionValidationMiddleware normal akıştaymış gibi session'ı doğrular.</para>
///
/// <para><b>Güvenlik:</b> Yalnızca aynı host'a giden isteklerde geçerli (loopback, kendi API).
/// Cookie'ler başka host'a ifşa olmaz çünkü SameSite=Strict ve HttpClient BaseAddress
/// kısıtlıdır (Program.cs'te localhost set edilir).</para>
/// </summary>
internal sealed class CookieForwardingHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CookieForwardingHandler(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var httpContext = _httpContextAccessor.HttpContext;

        // HttpContext yok (arka plan görevleri, Hangfire vs.) → cookie eklenmez,
        // endpoint 401 döner. Bu doğru davranış: arka plan işleri kullanıcı scope'unda
        // çağırmamalı.
        if (httpContext is not null && httpContext.Request.Cookies.Count > 0)
        {
            var cookieHeader = string.Join("; ",
                httpContext.Request.Cookies.Select(c => $"{c.Key}={c.Value}"));

            if (!string.IsNullOrEmpty(cookieHeader))
            {
                request.Headers.Remove("Cookie"); // defensive
                request.Headers.Add("Cookie", cookieHeader);
            }
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
