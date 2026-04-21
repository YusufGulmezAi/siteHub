using Microsoft.AspNetCore.Http;
using SiteHub.Application.Abstractions.Audit;

namespace SiteHub.Infrastructure.Connection;

/// <summary>
/// ICurrentConnectionInfo'nun HTTP tabanlı implementasyonu.
///
/// HttpContextAccessor ile o anki request'ten IP, user-agent, correlation-id'yi okur.
/// Reverse proxy ardında (nginx, Azure Front Door, AWS ALB) çalıştığımız için
/// X-Forwarded-For başlığını kontrol eder.
///
/// Blazor Server'da: ilk HTTP request (SignalR handshake) bu bilgileri taşır.
/// Circuit'in ömrü boyunca HttpContextAccessor null olabilir — güvenli null check.
/// </summary>
public sealed class HttpCurrentConnectionInfo : ICurrentConnectionInfo
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpCurrentConnectionInfo(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? IpAddress
    {
        get
        {
            var ctx = _httpContextAccessor.HttpContext;
            if (ctx is null) return null;

            // Reverse proxy ardında: gerçek client IP X-Forwarded-For'da
            var forwarded = ctx.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(forwarded))
            {
                // "client, proxy1, proxy2" → ilk entry en dış client
                return forwarded.Split(',')[0].Trim();
            }

            return ctx.Connection.RemoteIpAddress?.ToString();
        }
    }

    public string? UserAgent =>
        _httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString();

    public string? CorrelationId
    {
        get
        {
            var ctx = _httpContextAccessor.HttpContext;
            if (ctx is null) return null;

            // ASP.NET Core default: TraceIdentifier (request başına benzersiz)
            // İleride: Middleware ile X-Correlation-ID header'ından alabiliriz
            return ctx.TraceIdentifier;
        }
    }
}
