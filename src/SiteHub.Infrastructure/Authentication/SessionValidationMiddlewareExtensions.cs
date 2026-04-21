using Microsoft.AspNetCore.Builder;

namespace SiteHub.Infrastructure.Authentication;

/// <summary>
/// <c>app.UseSessionValidation()</c> için extension.
/// Program.cs'den <c>app.UseAuthentication()</c>'dan SONRA, <c>app.UseAuthorization()</c>'dan ÖNCE çağrılmalı.
/// </summary>
public static class SessionValidationMiddlewareExtensions
{
    public static IApplicationBuilder UseSessionValidation(this IApplicationBuilder app)
    {
        return app.UseMiddleware<SessionValidationMiddleware>();
    }
}
